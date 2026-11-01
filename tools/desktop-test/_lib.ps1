# Helpers for the desktop check scripts (Slice 21). Dot-source it; every check ends with Complete-Check.
# They run on a Windows desktop (the test VM, see tools/desktop-vm) under Windows PowerShell 5.1 or pwsh 7, against a
# self-contained PrintTheDamnBook.exe. They are never run on the developer's own desktop.

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, WindowsBase, System.Drawing, System.Windows.Forms

$script:Uia = [System.Windows.Automation.AutomationElement]
$script:Scope = [System.Windows.Automation.TreeScope]
$script:LaunchTimeoutSec = 30
$script:StepTimeoutSec = 20
$script:ResultLines = New-Object System.Collections.Generic.List[string]
$script:StartedProcesses = New-Object System.Collections.Generic.List[int]
$script:SamplePageCount = 8
$script:SampleColours = @('E53935', '8E24AA', '3949AB', '039BE5', '00897B', '7CB342', 'FDD835', 'FB8C00')

Add-Type -Namespace Ptdb -Name Native -MemberDefinition @'
[DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
'@ -ErrorAction SilentlyContinue

function Add-Report {
    param([Parameter(Mandatory)] [string] $Line)
    $script:ResultLines.Add($Line)
    Write-Host "  $Line"
}

function Complete-Check {
    # Runs the body. Any throw is a failure; otherwise the string it returns is the pass message. Exits 0 or 1.
    param([Parameter(Mandatory)] [string] $Name, [Parameter(Mandatory)] [scriptblock] $Body)
    $pass = $false
    $message = ''
    try { $message = [string](& $Body); $pass = $true } catch { $message = $_.Exception.Message }
    finally { Stop-StartedApps }
    $mark = if ($pass) { 'PASS' } else { 'FAIL' }
    Write-Host ("{0}  {1}: {2}" -f $mark, $Name, $message)
    if ($pass) { exit 0 } else { exit 1 }
}

function Resolve-AppExe {
    param([Parameter(Mandatory)] [string] $Exe)
    if (-not (Test-Path $Exe)) { throw "The app exe was not found: $Exe" }
    return (Resolve-Path $Exe).Path
}

function Start-App {
    # Starts the exe with the given arguments and returns the process; remembers it so Complete-Check can stop it.
    param([Parameter(Mandatory)] [string] $Exe, [string[]] $Arguments = @())
    $quoted = @($Arguments | ForEach-Object { '"' + $_ + '"' })
    $p = if ($quoted.Count) { Start-Process -FilePath $Exe -ArgumentList $quoted -PassThru } else { Start-Process -FilePath $Exe -PassThru }
    $script:StartedProcesses.Add($p.Id)
    return $p
}

function Stop-StartedApps {
    foreach ($id in $script:StartedProcesses) { Stop-Process -Id $id -Force -ErrorAction SilentlyContinue }
    $script:StartedProcesses.Clear()
}

function Find-ByName {
    # Descendant with this exact Name, or $null.
    param([Parameter(Mandatory)] $Root, [Parameter(Mandatory)] [string] $Name)
    $cond = New-Object System.Windows.Automation.PropertyCondition($script:Uia::NameProperty, $Name)
    return $Root.FindFirst($script:Scope::Descendants, $cond)
}

function Set-ToggleState {
    # Puts a switch (TogglePattern) on or off; does nothing when it already is.
    param([Parameter(Mandatory)] $Element, [Parameter(Mandatory)] [bool] $On)
    $pattern = $null
    if (-not $Element.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$pattern)) { throw "'$($Element.Current.Name)' is not a switch." }
    $isOn = $pattern.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On
    if ($isOn -ne $On) { $pattern.Toggle() }
}

function Wait-Until {
    param([Parameter(Mandatory)] [scriptblock] $Condition, [int] $TimeoutSec = $script:StepTimeoutSec, [Parameter(Mandatory)] [string] $Failure)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        $value = & $Condition
        if ($value) { return $value }
        Start-Sleep -Milliseconds 400
    }
    throw "$Failure (waited $TimeoutSec s)"
}

function Get-MainWindow {
    param([Parameter(Mandatory)] $Process)
    return Wait-Until -TimeoutSec $script:LaunchTimeoutSec -Failure 'The app window did not appear' -Condition {
        $Process.Refresh()
        if ($Process.HasExited) { throw "The app exited early (code $($Process.ExitCode))." }
        if ($Process.MainWindowHandle -ne [IntPtr]::Zero) { $script:Uia::FromHandle($Process.MainWindowHandle) }
    }
}

function Find-ById {
    # Descendant with this AutomationId, or $null.
    param([Parameter(Mandatory)] $Root, [Parameter(Mandatory)] [string] $Id)
    $cond = New-Object System.Windows.Automation.PropertyCondition($script:Uia::AutomationIdProperty, $Id)
    return $Root.FindFirst($script:Scope::Descendants, $cond)
}

function Wait-ById {
    param([Parameter(Mandatory)] $Root, [Parameter(Mandatory)] [string] $Id, [int] $TimeoutSec = $script:StepTimeoutSec)
    return Wait-Until -TimeoutSec $TimeoutSec -Failure "Control '$Id' did not appear" -Condition { Find-ById -Root $Root -Id $Id }
}

function Get-AllNames {
    param([Parameter(Mandatory)] $Root)
    return @($Root.FindAll($script:Scope::Descendants, [System.Windows.Automation.Condition]::TrueCondition) | ForEach-Object { $_.Current.Name })
}

function Invoke-Control {
    # Invoke, Toggle or Select the element; a RadioButton selects. Falls back to a mouse click on its centre.
    param([Parameter(Mandatory)] $Element)
    $pattern = $null
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) { $pattern.Invoke(); return }
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) { $pattern.Select(); return }
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$pattern)) { $pattern.Toggle(); return }
    $rect = $Element.Current.BoundingRectangle
    if ($rect.IsEmpty) { throw "Control '$($Element.Current.AutomationId)' has no pattern and no bounds." }
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point([int]($rect.X + $rect.Width / 2), [int]($rect.Y + $rect.Height / 2))
    Start-Sleep -Milliseconds 200
    [Ptdb.Native]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    [Ptdb.Native]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
}

function Select-IsOn {
    param([Parameter(Mandatory)] $Element)
    $pattern = $null
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) { return $pattern.Current.IsSelected }
    return $false
}

function Set-ElementText {
    param([Parameter(Mandatory)] $Element, [Parameter(Mandatory)] [string] $Text)
    $pattern = $null
    if (-not $Element.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$pattern)) { throw 'The field does not accept text (no ValuePattern).' }
    $pattern.SetValue($Text)
}

function New-SampleBookFolder {
    # Eight solid-colour pages named page-1.png .. page-8.png in a fresh temp folder; returns the folder path.
    # Eight pages = two sheets with no blanks, and every page has its own colour so a PDF can be checked by pixel.
    $dir = Join-Path ([IO.Path]::GetTempPath()) ("ptdb-sample-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    for ($i = 1; $i -le $script:SamplePageCount; $i++) {
        $bmp = New-Object System.Drawing.Bitmap(600, 800)
        try {
            $g = [System.Drawing.Graphics]::FromImage($bmp)
            $g.Clear([System.Drawing.ColorTranslator]::FromHtml('#' + $script:SampleColours[$i - 1]))
            $g.Dispose()
            $bmp.Save((Join-Path $dir "page-$i.png"), [System.Drawing.Imaging.ImageFormat]::Png)
        } finally { $bmp.Dispose() }
    }
    return $dir
}

function Find-DialogWindow {
    # A native dialog's top-level window. UIA may list it under the app window or on the desktop, so try both and say which matched
    # (the first VM run settles which is real): 1) a window child of the app window, 2) a desktop window of the same process,
    # 3) when a name is given, a desktop window with that name (the print spooler's Save dialog belongs to another process).
    # With -RequireName only a window with exactly that name counts (the app's own prompts are windows too).
    param([Parameter(Mandatory)] $AppWindow, [string] $Name, [switch] $RequireName)
    $windowCond = New-Object System.Windows.Automation.PropertyCondition($script:Uia::ControlTypeProperty, [System.Windows.Automation.ControlType]::Window)
    foreach ($own in $AppWindow.FindAll($script:Scope::Children, $windowCond)) {
        if (-not $RequireName -or $own.Current.Name -eq $Name) { return [pscustomobject]@{ Element = $own; Where = 'child of the app window' } }
    }
    $appHandle = $AppWindow.Current.NativeWindowHandle
    $appPid = $AppWindow.Current.ProcessId
    foreach ($w in $script:Uia::RootElement.FindAll($script:Scope::Children, $windowCond)) {
        if ($w.Current.NativeWindowHandle -eq $appHandle) { continue }
        if ($Name -and $w.Current.Name -eq $Name) { return [pscustomobject]@{ Element = $w; Where = "desktop window named '$Name'" } }
        if (-not $RequireName -and $w.Current.ProcessId -eq $appPid) { return [pscustomobject]@{ Element = $w; Where = 'desktop window of the app process' } }
    }
    return $null
}

function Wait-DialogWindow {
    param([Parameter(Mandatory)] $AppWindow, [string] $Name, [Parameter(Mandatory)] [string] $Failure)
    $found = Wait-Until -Failure $Failure -Condition { Find-DialogWindow -AppWindow $AppWindow -Name $Name }
    Add-Report "dialog '$($found.Element.Current.Name)' found as: $($found.Where)"
    return $found.Element
}

function Find-DialogEdit {
    # The file-name box: try the known ids in order (1001 in the Vista item dialogs, 1152 in the classic one), then the first Edit
    # control. Logs which one matched.
    param([Parameter(Mandatory)] $Dialog, [string[]] $Ids = @('1001', '1152'))
    return Wait-Until -Failure 'The dialog has no file-name box' -Condition {
        foreach ($id in $Ids) {
            $e = Find-ById -Root $Dialog -Id $id
            if ($e) { Add-Report "file-name box matched by id $id"; return $e }
        }
        $editCond = New-Object System.Windows.Automation.PropertyCondition($script:Uia::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)
        $e = $Dialog.FindFirst($script:Scope::Descendants, $editCond)
        if ($e) { Add-Report "file-name box matched as the first Edit (ids $($Ids -join ', ') not found)"; return $e }
    }
}

function Find-DialogButton {
    # The confirm button: id 1 (OK / Save / Select Folder), else a Button with one of the given names. Logs a name match.
    param([Parameter(Mandatory)] $Dialog, [string[]] $Names = @('Select Folder', 'Save', 'Open'))
    return Wait-Until -Failure 'The dialog has no confirm button' -Condition {
        $byId = Find-ById -Root $Dialog -Id '1'
        if ($byId) { return $byId }
        $buttonCond = New-Object System.Windows.Automation.PropertyCondition($script:Uia::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
        foreach ($b in $Dialog.FindAll($script:Scope::Descendants, $buttonCond)) { if ($Names -contains $b.Current.Name) { Add-Report "confirm button matched by name '$($b.Current.Name)'"; return $b } }
    }
}

function Open-FolderThroughDialog {
    # Clicks "Open folder", types the path into the native folder picker's name box and confirms it.
    param([Parameter(Mandatory)] $Window, [Parameter(Mandatory)] [string] $Folder)
    Invoke-Control (Wait-ById -Root $Window -Id 'OpenFolderButton')
    $dialog = Wait-DialogWindow -AppWindow $Window -Failure 'The folder picker did not open'
    Set-ElementText -Element (Find-DialogEdit -Dialog $dialog) -Text $Folder
    Invoke-Control (Find-DialogButton -Dialog $dialog)
}

function Wait-ForCheckStep {
    param([Parameter(Mandatory)] $Window)
    Wait-Until -Failure 'The Check step did not appear' -Condition { Find-ById -Root $Window -Id 'DirectionLeftToRight' } | Out-Null
}

function Get-PageTileNames {
    # The page tiles in document order, as "<label>, page N of M" names.
    param([Parameter(Mandatory)] $Window)
    $all = $Window.FindAll($script:Scope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    # Tiles scrolled or clipped out of view are still real tiles (IsOffscreen is true for them at small window sizes): keep them, once each.
    $names = foreach ($e in $all) {
        $n = $e.Current.Name
        if ($n -match ', page \d+ of \d+$') { $n }
    }
    return @($names | Select-Object -Unique)
}

function Get-ExpectedFaceOrder {
    # The Core imposition formula (CLAUDE.md, Slice 17) for N pages, as 1-based page numbers in face order:
    # sheet 0 front left, front right, back left, back right, then sheet 1, and so on. N must be a multiple of 4.
    param([Parameter(Mandatory)] [int] $Count, [switch] $RightToLeft)
    $order = @()
    for ($k = 0; $k -lt ($Count / 4); $k++) {
        if ($RightToLeft) { $face = @(($k * 2), ($Count - 1 - 2 * $k), ($Count - 2 - 2 * $k), (2 * $k + 1)) }
        else { $face = @(($Count - 1 - 2 * $k), (2 * $k), (2 * $k + 1), ($Count - 2 - 2 * $k)) }
        $order += @($face | ForEach-Object { $_ + 1 })
    }
    return $order
}
