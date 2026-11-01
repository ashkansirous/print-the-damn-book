<#
.SYNOPSIS
  Builds the self-contained single-file PrintTheDamnBook.exe the VM tests (Slice 21). Prints the exe path as its last line.
.DESCRIPTION
  The same command as .github/workflows/release.yml's "Publish single-file exe" step (Release, win-x64, self-contained),
  with a fixed test version. The package under test is the exe, not an MSIX, so no signing or install step is needed.
  Builds into -OutDir (default: a folder under the temp directory); an existing -OutDir is deleted first, but only the default folder or one inside this worktree. -WhatIf prints the command and builds nothing.
.EXAMPLE
  $exe = .\Build-TestPackage.ps1 | Select-Object -Last 1
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $OutDir = (Join-Path ([IO.Path]::GetTempPath()) 'ptdb-test-package'),
    [string] $Version = '0.0.0-desktop-test'
)
$ErrorActionPreference = 'Stop'
$script:DefaultOutDir = Join-Path ([IO.Path]::GetTempPath()) 'ptdb-test-package'
$repoRoot = (Resolve-Path "$PSScriptRoot/../..").Path
$project = Join-Path $repoRoot 'src/PrintTheDamnBook.App/PrintTheDamnBook.App.csproj'
$publishArgs = @('publish', $project, '--configuration', 'Release', '--runtime', 'win-x64', '--self-contained', 'true', "-p:Version=$Version", '--output', $OutDir)

function Remove-OutDirSafely {
    # Deletes -OutDir only when this repo's worktree contains it or it is the default folder this script creates; anything else is refused.
    param([string] $Path)
    $separators = [char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $full = [IO.Path]::GetFullPath($Path).TrimEnd($separators)
    $ownDefault = [IO.Path]::GetFullPath($script:DefaultOutDir).TrimEnd($separators)
    $insideRepo = $full.StartsWith($repoRoot.TrimEnd($separators) +[IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
    if ($full -ne $ownDefault -and -not $insideRepo) { throw "Refusing to delete -OutDir '$full': it is neither inside $repoRoot nor the script's own folder ($ownDefault). Pass an empty folder inside the worktree." }
    if (Test-Path $full) { Remove-Item $full -Recurse -Force }
}

if (-not $PSCmdlet.ShouldProcess($OutDir, "dotnet $($publishArgs -join ' ')")) { return }
Remove-OutDirSafely -Path $OutDir
& dotnet @publishArgs | Out-Host
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)." }
$exe = Join-Path $OutDir 'PrintTheDamnBook.exe'
if (-not (Test-Path $exe)) { throw "dotnet publish wrote no PrintTheDamnBook.exe under $OutDir." }
Write-Output $exe
