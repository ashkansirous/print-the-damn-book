<#
.SYNOPSIS
  Builds an unsigned .msix from the published PrintTheDamnBook.exe (Windows SDK MakePri + MakeAppx).
.PARAMETER Version
  Package version, four parts (X.Y.Z.0).
.PARAMETER SdkBin
  Folder holding makeappx.exe and makepri.exe. Default: the newest x64 folder under the Windows 10 SDK.
#>
param(
  [Parameter(Mandatory)] [string] $ExePath,
  [Parameter(Mandatory)] [string] $Version,
  [Parameter(Mandatory)] [string] $OutFile,
  [string] $IdentityName = $(if ($env:MSIX_IDENTITY_NAME) { $env:MSIX_IDENTITY_NAME } else { 'PrintTheDamnBook.Local' }),
  [string] $Publisher = $(if ($env:MSIX_PUBLISHER) { $env:MSIX_PUBLISHER } else { 'CN=PrintTheDamnBook Local' }),
  [string] $PublisherDisplayName = $(if ($env:MSIX_PUBLISHER_DISPLAY_NAME) { $env:MSIX_PUBLISHER_DISPLAY_NAME } else { 'Print the damn book (local build)' }),
  [string] $SdkBin
)

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$repo = Resolve-Path (Join-Path $here '..\..')
$assetsDir = Join-Path $repo 'src\PrintTheDamnBook.App\Assets'
$pngAssets = @('Square44x44Logo', 'StoreLogo', 'Square71x71Logo', 'Square150x150Logo', 'Wide310x150Logo', 'Square310x310Logo', 'SplashScreen')
$assetScales = @(100, 125, 150, 200, 400)
$iconTargetSizes = @(16, 24, 32, 48, 256)

function Find-SdkBin {
  $roots = @("${env:ProgramFiles(x86)}\Windows Kits\10\bin")
  foreach ($root in $roots) {
    if (-not (Test-Path $root)) { continue }
    $hit = Get-ChildItem $root -Directory | Where-Object { $_.Name -like '10.*' } | Sort-Object { [version]$_.Name } -Descending |
      ForEach-Object { Join-Path $_.FullName 'x64' } | Where-Object { Test-Path (Join-Path $_ 'makeappx.exe') } | Select-Object -First 1
    if ($hit) { return $hit }
  }
  throw 'Windows 10/11 SDK (makeappx.exe) not found; pass -SdkBin.'
}

function Set-ManifestTokens([string] $source, [string] $destination) {
  $text = Get-Content $source -Raw
  $values = @{
    '@IDENTITY_NAME@' = $IdentityName; '@PUBLISHER@' = $Publisher; '@VERSION@' = $Version
    '@PUBLISHER_DISPLAY_NAME@' = $PublisherDisplayName
  }
  foreach ($token in $values.Keys) { $text = $text.Replace($token, [Security.SecurityElement]::Escape($values[$token])) }
  if ($text -match '@[A-Z_]+@') { throw "Unfilled manifest token: $($Matches[0])" }
  [IO.File]::WriteAllText($destination, $text, (New-Object Text.UTF8Encoding($false)))
}

function Get-ExpectedAssetFiles {
  foreach ($name in $pngAssets) { foreach ($scale in $assetScales) { "$name.scale-$scale.png" } }
  foreach ($size in $iconTargetSizes) { "Square44x44Logo.targetsize-$size.png"; "Square44x44Logo.targetsize-${size}_altform-unplated.png" }
}

function Copy-VisualAssets([string] $stage) {
  $target = Join-Path $stage 'Assets'
  New-Item -ItemType Directory -Force $target | Out-Null
  foreach ($file in Get-ExpectedAssetFiles) {
    $source = Join-Path $assetsDir $file
    if (-not (Test-Path $source)) { throw "Missing visual asset: $source" }
    Copy-Item $source $target
  }
}

if ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw "Version must be X.Y.Z.W, got '$Version'." }
if (-not (Test-Path $ExePath)) { throw "Exe not found: $ExePath" }
if (-not $SdkBin) { $SdkBin = Find-SdkBin }
$makeAppx = Join-Path $SdkBin 'makeappx.exe'
$makePri = Join-Path $SdkBin 'makepri.exe'

$stage = Join-Path ([IO.Path]::GetTempPath()) "ptdb-msix-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory $stage | Out-Null
try {
  Copy-Item $ExePath (Join-Path $stage 'PrintTheDamnBook.exe')
  Copy-VisualAssets $stage
  Set-ManifestTokens (Join-Path $here 'AppxManifest.xml') (Join-Path $stage 'AppxManifest.xml')

  & $makePri createconfig /cf (Join-Path $stage 'priconfig.xml') /dq en-US /o
  if ($LASTEXITCODE) { throw 'makepri createconfig failed.' }
  & $makePri new /pr $stage /cf (Join-Path $stage 'priconfig.xml') /mn (Join-Path $stage 'AppxManifest.xml') /of (Join-Path $stage 'resources.pri') /o
  if ($LASTEXITCODE) { throw 'makepri new failed.' }
  Remove-Item (Join-Path $stage 'priconfig.xml')

  New-Item -ItemType Directory -Force (Split-Path -Parent ([IO.Path]::GetFullPath($OutFile))) | Out-Null
  & $makeAppx pack /d $stage /p ([IO.Path]::GetFullPath($OutFile)) /o
  if ($LASTEXITCODE) { throw 'makeappx pack failed.' }
  Write-Host "Built $OutFile ($Version, $IdentityName)"
} finally {
  Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
}
