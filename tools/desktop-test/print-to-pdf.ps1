# Check: print the sample book to "Microsoft Print to PDF", then compare each PDF page's colours with the Core formula.
# The sample pages are solid colours, so the colour at the centre of each half of a PDF page says which book page is there.
# "Microsoft Print to PDF" is not a validated two-sided printer, so the app prints in Manual mode: a fronts job and a backs job
# (two PDFs, with a reload prompt between them). Wipes nothing: PDFs and the sample folder live in %TEMP% and are removed.
# Needs Windows PowerShell 5.1 (Windows.Data.Pdf through WinRT). Usage: print-to-pdf.ps1 -Exe <PrintTheDamnBook.exe>
# -SaveDialogName is the title of the OS's "Save Print Output As" dialog; pass the localised title on a non-English Windows.
param([Parameter(Mandatory)] [string] $Exe, [string] $SaveDialogName = 'Save Print Output As')
. "$PSScriptRoot/_lib.ps1"
$Exe = Resolve-AppExe $Exe

$script:PdfPrinterName = 'Print to PDF'
$script:SaveDialogName = $SaveDialogName
$script:ReloadPromptTitle = 'Put the stack back'
$script:ReloadPromptButton = 'Print the backs'
$script:ReverseToggleName = 'Reverse order of backs'
$script:RotateToggleName = 'Rotate backs 180 degrees'
$script:PrintTimeoutSec = 120
$script:ColourTolerance = 60
$script:RenderWidth = 1200

function Select-PdfPrinter {
    param($Window)
    $combo = Wait-ById -Root $Window -Id 'PrinterCombo'
    $pattern = $null
    if (-not $combo.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$pattern)) { throw 'The printer box cannot be expanded.' }
    $pattern.Expand()
    $item = Wait-Until -Failure "No printer named '$script:PdfPrinterName' in the list" -Condition {
        foreach ($e in $combo.FindAll($script:Scope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
            if ($e.Current.Name -like "*$script:PdfPrinterName*" -and $e.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem) { return $e }
        }
        foreach ($e in $combo.FindAll($script:Scope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
            if ($e.Current.Name -like "*$script:PdfPrinterName*") { return $e }
        }
    }
    Invoke-Control $item
    $pattern.Collapse()
}

function Save-OnePdf {
    # Answers the native "Save Print Output As" dialog if it is open; returns the path saved, or $null when none is open.
    param($Window, [string] $OutDir, [int] $Index)
    $found = Find-DialogWindow -AppWindow $Window -Name $script:SaveDialogName -RequireName
    if (-not $found) { return $null }
    $dialog = $found.Element
    Add-Report "save dialog '$($dialog.Current.Name)' found as: $($found.Where)"
    $path = Join-Path $OutDir "run-$Index.pdf"
    Set-ElementText -Element (Find-DialogEdit -Dialog $dialog) -Text $path
    Invoke-Control (Find-DialogButton -Dialog $dialog)
    return $path
}

function Confirm-AppPrompt {
    # Presses "Print the backs" on the reload prompt (a child window of the app). Any other child window of the app (an error
    # dialog, say) fails the check with its name and text instead of being clicked away.
    param($Window)
    $cond = New-Object System.Windows.Automation.PropertyCondition($script:Uia::ControlTypeProperty, [System.Windows.Automation.ControlType]::Window)
    $prompt = $Window.FindFirst($script:Scope::Children, $cond)
    if (-not $prompt) { return $false }
    if ($prompt.Current.Name -eq $script:SaveDialogName) { return $false }   # the Save dialog still closing after Save-OnePdf
    $buttonCond = New-Object System.Windows.Automation.PropertyCondition($script:Uia::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
    $title = $prompt.Current.Name
    if ($title -ne $script:ReloadPromptTitle) {
        $texts = (Get-AllNames -Root $prompt | Where-Object { $_ }) -join ' / '
        throw "An unexpected window opened over the app: '$title' ($texts)."
    }
    foreach ($b in $prompt.FindAll($script:Scope::Descendants, $buttonCond)) {
        if ($b.Current.Name -eq $script:ReloadPromptButton) { Invoke-Control $b; return $true }
    }
    throw "The reload prompt has no '$script:ReloadPromptButton' button."
}

function Set-BackOptions {
    # Manual mode shows two switches. Set both explicitly (reverse off, rotate on) so the expected order below never depends on a saved setting.
    param($Window)
    Set-ToggleState -Element (Wait-Until -Failure 'The reverse-backs switch did not appear' -Condition { Find-ByName -Root $Window -Name $script:ReverseToggleName }) -On $false
    Set-ToggleState -Element (Wait-Until -Failure 'The rotate-backs switch did not appear' -Condition { Find-ByName -Root $Window -Name $script:RotateToggleName }) -On $true
}

function Get-ExpectedBackFaces {
    # Back faces in print order as "left+right" for the settings Set-BackOptions chose. Core: back = (left p[2k+1], right p[N-2-2k]),
    # which Get-ExpectedFaceOrder lists as entries 2 and 3 of each sheet. RotateBacks turns the face 180 degrees, so what the PDF
    # shows on its left half is the face's right page and vice versa. ReverseBacks (off here) would list the sheets last to first.
    param([Parameter(Mandatory)] [int] $Count, [bool] $Rotate = $true, [bool] $Reverse = $false)
    $want = Get-ExpectedFaceOrder -Count $Count
    $backs = @()
    for ($k = 0; $k -lt ($Count / 4); $k++) {
        $left = $want[4 * $k + 2]; $right = $want[4 * $k + 3]
        if ($Rotate) { $backs += "$right+$left" } else { $backs += "$left+$right" }
    }
    if ($Reverse) { [array]::Reverse($backs) }
    return $backs
}

function Wait-ForPrintDone {
    param($Window, [string] $OutDir)
    $saved = @()
    $deadline = (Get-Date).AddSeconds($script:PrintTimeoutSec)
    while ((Get-Date) -lt $deadline) {
        $path = Save-OnePdf -Window $Window -OutDir $OutDir -Index ($saved.Count + 1)
        if ($path) { $saved += $path; Start-Sleep -Seconds 2; continue }
        if (Confirm-AppPrompt -Window $Window) { Start-Sleep -Seconds 1; continue }
        $done = $Window.FindFirst($script:Scope::Descendants, (New-Object System.Windows.Automation.PropertyCondition($script:Uia::NameProperty, 'Print another copy')))
        if ($done) { return $saved }
        Start-Sleep -Milliseconds 500
    }
    throw "Printing did not reach the Done step within $script:PrintTimeoutSec s (PDFs saved so far: $($saved.Count))."
}

function Initialize-PdfRenderer {
    Add-Type -AssemblyName System.Runtime.WindowsRuntime
    [void][Windows.Data.Pdf.PdfDocument, Windows.Data.Pdf, ContentType = WindowsRuntime]
    [void][Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]
    [void][Windows.Storage.Streams.InMemoryRandomAccessStream, Windows.Storage.Streams, ContentType = WindowsRuntime]
    [void][Windows.Data.Pdf.PdfPageRenderOptions, Windows.Data.Pdf, ContentType = WindowsRuntime]
    [void][Windows.Foundation.IAsyncAction, Windows.Foundation, ContentType = WindowsRuntime]
}

function Wait-WinRt {
    # Blocks on a WinRT async operation (PowerShell has no await).
    param($Operation, [Type] $ResultType)
    $asTask = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
        $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' } | Select-Object -First 1
    $task = $asTask.MakeGenericMethod($ResultType).Invoke($null, @($Operation))
    $task.Wait()
    return $task.Result
}

function Get-PdfCentreColours {
    # For each page of the PDF: the two colours (left half centre, right half centre) as hex, nearest to a sample colour.
    param([string] $Pdf)
    Initialize-PdfRenderer
    $file = Wait-WinRt ([Windows.Storage.StorageFile]::GetFileFromPathAsync($Pdf)) ([Windows.Storage.StorageFile])
    $doc = Wait-WinRt ([Windows.Data.Pdf.PdfDocument]::LoadFromFileAsync($file)) ([Windows.Data.Pdf.PdfDocument])
    $faces = @()
    for ($i = 0; $i -lt $doc.PageCount; $i++) {
        $page = $doc.GetPage($i)
        $stream = New-Object Windows.Storage.Streams.InMemoryRandomAccessStream
        $options = New-Object Windows.Data.Pdf.PdfPageRenderOptions
        $options.DestinationWidth = $script:RenderWidth
        $renderTask = [System.WindowsRuntimeSystemExtensions].GetMethod('AsTask', [Type[]]@([Windows.Foundation.IAsyncAction])).Invoke($null, @($page.RenderToStreamAsync($stream, $options)))
        $renderTask.Wait()
        $bmp = New-Object System.Drawing.Bitmap([System.IO.WindowsRuntimeStreamExtensions]::AsStreamForRead($stream))
        try {
            $y = [int]($bmp.Height / 2)
            $faces += , @((Get-NearestColour $bmp.GetPixel([int]($bmp.Width / 4), $y)), (Get-NearestColour $bmp.GetPixel([int]($bmp.Width * 3 / 4), $y)))
        } finally { $bmp.Dispose(); $stream.Dispose(); $page.Dispose() }
    }
    return $faces
}

function Get-NearestColour {
    # The 1-based sample page whose colour is nearest to the pixel (0 when none is within the tolerance: a blank or text page).
    param([System.Drawing.Color] $Pixel)
    $best = 0
    $bestDistance = [double]::MaxValue
    for ($i = 0; $i -lt $script:SamplePageCount; $i++) {
        $c = [System.Drawing.ColorTranslator]::FromHtml('#' + $script:SampleColours[$i])
        $d = [Math]::Sqrt([Math]::Pow($c.R - $Pixel.R, 2) + [Math]::Pow($c.G - $Pixel.G, 2) + [Math]::Pow($c.B - $Pixel.B, 2))
        if ($d -lt $bestDistance) { $bestDistance = $d; $best = $i + 1 }
    }
    if ($bestDistance -gt $script:ColourTolerance) { return 0 }
    return $best
}

Complete-Check -Name 'print-to-pdf' -Body {
    $folder = New-SampleBookFolder
    $outDir = Join-Path ([IO.Path]::GetTempPath()) ("ptdb-pdf-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
    try {
        $window = Get-MainWindow -Process (Start-App -Exe $Exe)
        Open-FolderThroughDialog -Window $window -Folder $folder
        Wait-ForCheckStep -Window $window
        Invoke-Control (Wait-ById -Root $window -Id 'NextButton')
        Select-PdfPrinter -Window $window
        Set-BackOptions -Window $window
        Invoke-Control (Wait-ById -Root $window -Id 'NextButton')
        $pdfs = @(Wait-ForPrintDone -Window $window -OutDir $outDir)
        if ($pdfs.Count -ne 2) { throw "Expected 2 PDFs (fronts and backs, Manual duplex), the app saved $($pdfs.Count)." }
        $faces = @()
        foreach ($pdf in $pdfs) { $faces += , @(Get-PdfCentreColours -Pdf $pdf) }
    } finally {
        Remove-Item $folder -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item $outDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    # Manual duplex: file 1 = the fronts (sheet order), file 2 = the backs.
    $want = Get-ExpectedFaceOrder -Count $script:SamplePageCount
    $wantFronts = @(); $wantBacks = @()
    for ($k = 0; $k -lt ($script:SamplePageCount / 4); $k++) { $wantFronts += , @($want[4 * $k], $want[4 * $k + 1]); $wantBacks += , @($want[4 * $k + 2], $want[4 * $k + 3]) }
    $got = @(); foreach ($file in $faces) { foreach ($face in $file) { $got += ($face -join '+') } }
    Add-Report ("PDF pages (left+right): " + ($got -join ' | '))
    $fronts = @($faces[0] | ForEach-Object { $_ -join '+' })
    $wantFrontText = @($wantFronts | ForEach-Object { $_ -join '+' })
    if (($fronts -join '|') -ne ($wantFrontText -join '|')) { throw "Fronts PDF pages are $($fronts -join ' | '), the formula says $($wantFrontText -join ' | ')." }
    # Manual duplex with Set-BackOptions' settings (rotate on, reverse off): compare every back face exactly, in order.
    $backs = @($faces[1] | ForEach-Object { $_ -join '+' })
    $wantBackText = @(Get-ExpectedBackFaces -Count $script:SamplePageCount -Rotate $true -Reverse $false)
    if (($backs -join '|') -ne ($wantBackText -join '|')) { throw "Backs PDF pages are $($backs -join ' | '), the formula says $($wantBackText -join ' | ') (rotated 180 degrees, sheet order)." }
    'PDF page order matches the Core formula (fronts file and backs file)'
}
