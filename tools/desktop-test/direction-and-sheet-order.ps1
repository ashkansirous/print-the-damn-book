# Check: the reading-direction switch and the "As printed" sheet order against the Core formula (Slice 17).
# Wipes nothing. Usage: direction-and-sheet-order.ps1 -Exe <PrintTheDamnBook.exe>
param([Parameter(Mandatory)] [string] $Exe)
. "$PSScriptRoot/_lib.ps1"

function Read-FaceOrder {
    # The page numbers on the printed faces in document order, from the label text under each tile in the As printed view ("page-3").
    param($Window)
    $names = Get-SheetTileNames -Window $Window
    Add-Report ("as printed tile names: " + ($names -join ' | '))
    return @($names | ForEach-Object { if ($_ -match '^page-(\d+)$') { [int]$Matches[1] } })
}

Complete-Check -Name 'direction-and-sheet-order' -Body {
    $Exe = Resolve-AppExe $Exe
    $folder = New-SampleBookFolder
    try {
        $window = Get-MainWindow -Process (Start-App -Exe $Exe)
        Open-FolderThroughDialog -Window $window -Folder $folder
        Wait-ForCheckStep -Window $window
        Invoke-Control (Wait-ById -Root $window -Id 'ViewAsPrinted')
        Start-Sleep -Seconds 1
        Save-Screenshot -Name 'as-printed-ltr'
        $ltr = Read-FaceOrder -Window $window
        Invoke-Control (Wait-ById -Root $window -Id 'DirectionRightToLeft')
        Wait-Until -Failure 'The Right to left switch did not stick' -Condition { Select-IsOn (Find-ById -Root $window -Id 'DirectionRightToLeft') } | Out-Null
        Start-Sleep -Seconds 1
        $rtl = Read-FaceOrder -Window $window
        Invoke-Control (Wait-ById -Root $window -Id 'DirectionLeftToRight')
        Wait-Until -Failure 'Left to right did not come back' -Condition { Select-IsOn (Find-ById -Root $window -Id 'DirectionLeftToRight') } | Out-Null
    } finally { Remove-Item $folder -Recurse -Force -ErrorAction SilentlyContinue }
    $wantLtr = Get-ExpectedFaceOrder -Count $script:SamplePageCount
    $wantRtl = Get-ExpectedFaceOrder -Count $script:SamplePageCount -RightToLeft
    Add-Report "left to right: $($ltr -join ',') (want $($wantLtr -join ','))"
    Add-Report "right to left: $($rtl -join ',') (want $($wantRtl -join ','))"
    if (($ltr -join ',') -ne ($wantLtr -join ',')) { throw "Left-to-right face order is $($ltr -join ','), the formula says $($wantLtr -join ',')." }
    if (($rtl -join ',') -ne ($wantRtl -join ',')) { throw "Right-to-left face order is $($rtl -join ','), the formula says $($wantRtl -join ',')." }
    'As printed order matches the Core formula in both directions'
}
