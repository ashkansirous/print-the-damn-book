# Check: open a folder of page images. The app has no folder command line (only .dino), so this drives the native folder picker.
# Wipes nothing: the sample folder is created in %TEMP% and removed. Usage: open-folder.ps1 -Exe <PrintTheDamnBook.exe>
param([Parameter(Mandatory)] [string] $Exe)
. "$PSScriptRoot/_lib.ps1"

Complete-Check -Name 'open-folder' -Body {
    $Exe = Resolve-AppExe $Exe
    $folder = New-SampleBookFolder
    try {
        $window = Get-MainWindow -Process (Start-App -Exe $Exe)
        Open-FolderThroughDialog -Window $window -Folder $folder
        Wait-ForCheckStep -Window $window
        $tiles = Wait-Until -Failure "Fewer than $script:SamplePageCount page tiles appeared" -Condition {
            $names = Get-PageTileNames -Window $window
            if ($names.Count -ge $script:SamplePageCount) { $names }
        }
    } finally { Remove-Item $folder -Recurse -Force -ErrorAction SilentlyContinue }
    $expected = 1..$script:SamplePageCount | ForEach-Object { "page-$_, page $_ of $script:SamplePageCount" }
    $got = @($tiles | Select-Object -First $script:SamplePageCount)
    Add-Report ("tiles: " + ($got -join ' | '))
    if (($got -join '|') -ne ($expected -join '|')) { throw "Tiles are not page-1 .. page-$script:SamplePageCount in order." }
    "folder opened; $script:SamplePageCount pages in file-name order"
}
