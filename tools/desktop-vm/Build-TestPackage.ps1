<#
.SYNOPSIS
  Builds the self-contained single-file PrintTheDamnBook.exe the VM tests (Slice 21). Prints the exe path as its last line.
.DESCRIPTION
  The same command as .github/workflows/release.yml's "Publish single-file exe" step (Release, win-x64, self-contained),
  with a fixed test version. The package under test is the exe, not an MSIX, so no signing or install step is needed.
  Builds into -OutDir (default: tmp/ptdb-test-package inside this worktree, so sessions never share it). An existing -OutDir is deleted first, but only when it is empty or holds the marker file this script wrote when it created it; any other folder is refused. -WhatIf prints the command and builds nothing.
.EXAMPLE
  $exe = .\Build-TestPackage.ps1 | Select-Object -Last 1
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $OutDir,
    [string] $Version = '0.0.0-desktop-test'
)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path "$PSScriptRoot/../..").Path
$script:MarkerName = '.ptdb-test-package'
if (-not $OutDir) { $OutDir = Join-Path $repoRoot 'tmp/ptdb-test-package' }
$project = Join-Path $repoRoot 'src/PrintTheDamnBook.App/PrintTheDamnBook.App.csproj'
$publishArgs = @('publish', $project, '--configuration', 'Release', '--runtime', 'win-x64', '--self-contained', 'true', "-p:Version=$Version", '--output', $OutDir)

function Remove-OutDirSafely {
    # Deletes -OutDir only when it is empty or carries the marker file this script wrote when it created it; anything else is refused.
    param([string] $Path)
    $full = [IO.Path]::GetFullPath($Path)
    if (Test-Path $full) {
        $hasMarker = Test-Path (Join-Path $full $script:MarkerName)
        $isEmpty = -not (Get-ChildItem -LiteralPath $full -Force | Select-Object -First 1)
        if (-not $hasMarker -and -not $isEmpty) { throw "Refusing to delete -OutDir '$full': it is not empty and has no $($script:MarkerName) marker, so this script did not create it. Pass a new or empty folder." }
        Remove-Item -LiteralPath $full -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $full | Out-Null
    Set-Content -Path (Join-Path $full $script:MarkerName) -Value 'Created by Build-TestPackage.ps1; safe to delete.'
}

if (-not $PSCmdlet.ShouldProcess($OutDir, "dotnet $($publishArgs -join ' ')")) { return }
Remove-OutDirSafely -Path $OutDir
& dotnet @publishArgs | Out-Host
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)." }
$exe = Join-Path $OutDir 'PrintTheDamnBook.exe'
if (-not (Test-Path $exe)) { throw "dotnet publish wrote no PrintTheDamnBook.exe under $OutDir." }
Write-Output $exe
