# Helpers for the desktop check scripts (Slice 21). Dot-source it; every check ends with Complete-Check.
# They run on a Windows desktop (the test VM, see tools/desktop-vm) under Windows PowerShell 5.1 or pwsh 7, against a
# self-contained PrintTheDamnBook.exe. They are meant for the VM only: they launch the app, take focus and Invoke-Control falls back to
# moving the REAL cursor and clicking. The one exception is store-dialog, which Invoke-DesktopTest runs on the host only with
# -HostStore -AcknowledgeHostDesktop (the VM has no network).

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, WindowsBase, System.Drawing, System.Windows.Forms

$script:Uia = [System.Windows.Automation.AutomationElement]
$script:Scope = [System.Windows.Automation.TreeScope]
$script:LaunchTimeoutSec = 30
# Per-step wait; run-all / Invoke-DesktopTest pass -StepTimeoutSec down through PTDB_STEP_TIMEOUT_SEC (60 s in the VM).
$script:StepTimeoutSec = 20
$parsedStepTimeout = 0
if ([int]::TryParse($env:PTDB_STEP_TIMEOUT_SEC, [ref]$parsedStepTimeout) -and $parsedStepTimeout -gt 0) { $script:StepTimeoutSec = $parsedStepTimeout }
$script:ResultLines = New-Object System.Collections.Generic.List[string]
$script:StartedProcesses = New-Object System.Collections.Generic.List[int]
$script:SamplePageCount = 8
$script:SampleColours = @('E53935', '8E24AA', '3949AB', '039BE5', '00897B', '7CB342', 'FDD835', 'FB8C00')

Add-Type -Namespace Ptdb -Name Native -MemberDefinition @'
[DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
'@ -ErrorAction SilentlyContinue

Add-Type -Namespace Ptdb -Name Win -ErrorAction SilentlyContinue -MemberDefinition @'
public delegate bool EnumProc(IntPtr h, IntPtr l);
[DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumProc proc, IntPtr l);
[DllImport("user32.dll")] static extern int GetDlgCtrlID(IntPtr h);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int max);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, string l);
[DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
[DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
public static bool IsOpen(IntPtr h) { return IsWindow(h); }
public static int GetId(IntPtr h) { return GetDlgCtrlID(h); }
public static IntPtr SendText(IntPtr h, string text) { return SendMessage(h, 0x000C, IntPtr.Zero, text); }
public static bool Post(IntPtr h, uint msg, IntPtr w, IntPtr l) { return PostMessage(h, msg, w, l); }
static string ClassOf(IntPtr h) { var sb = new System.Text.StringBuilder(256); GetClassName(h, sb, 256); return sb.ToString(); }
public static IntPtr FindButton(IntPtr dialog, int id) {
  IntPtr found = IntPtr.Zero;
  if (dialog == IntPtr.Zero) return found;
  EnumChildWindows(dialog, delegate(IntPtr h, IntPtr l) { if (GetDlgCtrlID(h) == id && ClassOf(h) == "Button") { found = h; return false; } return true; }, IntPtr.Zero);
  return found;
}
public static IntPtr FindEdit(IntPtr dialog) {
  if (dialog == IntPtr.Zero) return IntPtr.Zero;
  foreach (int id in new int[] { 1152, 1148, 1001 }) {
    IntPtr found = IntPtr.Zero;
    EnumChildWindows(dialog, delegate(IntPtr h, IntPtr l) { if (GetDlgCtrlID(h) == id && ClassOf(h) == "Edit") { found = h; return false; } return true; }, IntPtr.Zero);
    if (found != IntPtr.Zero) return found;
  }
  return IntPtr.Zero;
}
'@

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

function Set-DialogPath {
    # Puts the path into a native file or folder dialog with Win32 messages (the dialog's controls are legacy windows with no UIA
    # patterns, and SetFocus/SendKeys do not work in the VM): finds the dialog's Edit window by control id 1152 / 1148 / 1001, sends
    # it WM_SETTEXT, then clicks the confirm button (class Button, control id 1 = IDOK) with BM_CLICK, falling back to WM_COMMAND
    # IDOK on the dialog, and waits up to 10 s for the dialog to close. Refuses a zero handle and a dialog of another process.
    # Returns 'keyboard' (the dialog is confirmed already), so callers do not press the button again.
    param([Parameter(Mandatory)] $Dialog, [Parameter(Mandatory)] [string] $Path)
    $dialogHandle = [IntPtr]$Dialog.Current.NativeWindowHandle
    if ($dialogHandle -eq [IntPtr]::Zero) { throw 'The dialog has no window handle; refusing to send messages.' }
    if ($script:StartedProcesses -notcontains $Dialog.Current.ProcessId) { throw "The dialog belongs to process $($Dialog.Current.ProcessId), not to the app this check started; refusing to send messages." }
    $edit = Wait-Until -TimeoutSec 10 -Failure 'The dialog has no file-name Edit window' -Condition { $h = [Ptdb.Win]::FindEdit($dialogHandle); if ($h -ne [IntPtr]::Zero) { $h } }
    Add-Report ("file-name Edit window found: handle {0}, control id {1}" -f $edit, [Ptdb.Win]::GetId($edit))
    [void][Ptdb.Win]::SendText($edit, $Path)
    Start-Sleep -Milliseconds 300
    $button = [Ptdb.Win]::FindButton($dialogHandle, 1)
    if ($button -ne [IntPtr]::Zero) { Add-Report 'confirm: BM_CLICK on Button control id 1'; [void][Ptdb.Win]::Post($button, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) }
    else { Add-Report 'confirm: WM_COMMAND IDOK to the dialog'; [void][Ptdb.Win]::Post($dialogHandle, 0x0111, [IntPtr]1, [IntPtr]::Zero) }
    [void](Wait-Until -TimeoutSec 10 -Failure 'The dialog did not close after the confirm button was pressed' -Condition { if (-not [Ptdb.Win]::IsOpen($dialogHandle)) { $true } })
    return 'keyboard'
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
    [void](Set-DialogPath -Dialog $dialog -Path $Folder)
}

function New-SampleDino {
    # A .dino (zip: package.json, story.json, page-1.png .. page-8.png) of the same eight solid-colour pages as New-SampleBookFolder,
    # with no page text, so a check can open the book by command line instead of the folder picker. Returns the .dino path.
    # Extracted pictures are named 1.png .. 8.png, so the page tiles are named "1, page 1 of 8" and so on.
    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    $path = Join-Path ([IO.Path]::GetTempPath()) ("ptdb-sample-" + [Guid]::NewGuid().ToString('N').Substring(0, 8) + '.dino')
    $zip = [System.IO.Compression.ZipFile]::Open($path, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        $pages = @()
        for ($i = 1; $i -le $script:SamplePageCount; $i++) {
            $pages += [ordered]@{ Text = ''; ImageFile = "page-$i.png" }
            Add-ZipPng -Zip $zip -Name "page-$i.png" -Hex $script:SampleColours[$i - 1]
        }
        Add-ZipText -Zip $zip -Name 'package.json' -Text (@{ SchemaVersion = 1; Id = 'ptdb-desktop-test'; Title = 'Desktop test book'; Language = 'en' } | ConvertTo-Json -Compress)
        Add-ZipText -Zip $zip -Name 'story.json' -Text (@{ Id = 'ptdb-desktop-test'; Title = 'Desktop test book'; Language = 'en'; Pages = $pages } | ConvertTo-Json -Compress -Depth 4)
    } finally { $zip.Dispose() }
    return $path
}

function Add-ZipText {
    param($Zip, [string] $Name, [string] $Text)
    $entry = $Zip.CreateEntry($Name)
    $stream = $entry.Open()
    try { $bytes = (New-Object System.Text.UTF8Encoding($false)).GetBytes($Text); $stream.Write($bytes, 0, $bytes.Length) } finally { $stream.Dispose() }
}

function Add-ZipPng {
    param($Zip, [string] $Name, [string] $Hex)
    $bmp = New-Object System.Drawing.Bitmap(600, 800)
    $memory = New-Object System.IO.MemoryStream
    try {
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.Clear([System.Drawing.ColorTranslator]::FromHtml('#' + $Hex))
        $g.Dispose()
        $bmp.Save($memory, [System.Drawing.Imaging.ImageFormat]::Png)
        $entry = $Zip.CreateEntry($Name)
        $stream = $entry.Open()
        try { $bytes = $memory.ToArray(); $stream.Write($bytes, 0, $bytes.Length) } finally { $stream.Dispose() }
    } finally { $bmp.Dispose(); $memory.Dispose() }
}

function Wait-ForCheckStep {
    param([Parameter(Mandatory)] $Window)
    Wait-Until -Failure 'The Check step did not appear' -Condition { Find-ById -Root $Window -Id 'DirectionLeftToRight' } | Out-Null
}

function Get-PageTileNames {
    # The page tiles of the Reading order view in document order, by the label text under each ("page-3"). The tile's own
    # AutomationProperties.Name ("page-3, page 3 of 8") is not exposed in the UIA tree (VM run 2026-11-01); its label TextBlock is.
    param([Parameter(Mandatory)] $Window)
    $all = $Window.FindAll($script:Scope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    $names = foreach ($e in $all) {
        $n = $e.Current.Name
        if ($n -match '^page-\d+$' -and $e.Current.ControlType -eq [System.Windows.Automation.ControlType]::Text) { $n }
    }
    return @($names | Select-Object -Unique)
}

function Save-Screenshot {
    # Saves the whole screen as <name>.png in the results folder (or temp) so a failed run shows what the app really drew.
    param([Parameter(Mandatory)] [string] $Name)
    $dir = Join-Path $PSScriptRoot '..\results'
    if (-not (Test-Path $dir)) { $dir = [IO.Path]::GetTempPath() }
    $screen = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $bmp = New-Object System.Drawing.Bitmap($screen.Width, $screen.Height)
    try {
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen($screen.Left, $screen.Top, 0, 0, $bmp.Size)
        $g.Dispose()
        $file = Join-Path $dir ($Name + '.png')
        $bmp.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
        Add-Report "screenshot ${Name}: $file (screen $($screen.Width)x$($screen.Height))"
    } finally { $bmp.Dispose() }
}

function Write-ElementDump {
    # Logs every named element or Image with its type and rectangle (one line each), to find out what the app exposes.
    param([Parameter(Mandatory)] $Window, [Parameter(Mandatory)] [string] $Title)
    $lines = foreach ($e in $Window.FindAll($script:Scope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
        $c = $e.Current
        if ($c.Name -or $c.ControlType -eq [System.Windows.Automation.ControlType]::Image) {
            $nm = if ($c.Name.Length -gt 40) { $c.Name.Substring(0, 40) } else { $c.Name }
            '{0} [{1}] @{2},{3} {4}x{5} off={6}' -f $nm, $c.ControlType.ProgrammaticName.Replace('ControlType.', ''), [int]$c.BoundingRectangle.X, [int]$c.BoundingRectangle.Y, [int]$c.BoundingRectangle.Width, [int]$c.BoundingRectangle.Height, $c.IsOffscreen
        }
    }
    Add-Report ("element dump ${Title}: " + (@($lines) -join ' || '))
}

function Get-SheetTileNames {
    # The tiles of the As printed view, by the label text under each ("page-3"; the tile's own automation name is not exposed). The UIA
    # tree order is not trusted: they are sorted by where they are, top to bottom by row (tiles whose vertical extents overlap are one
    # row), then left to right. Elements with an empty rectangle are skipped; a repeated label keeps its first tile. Every match is
    # logged with its position.
    param([Parameter(Mandatory)] $Window)
    $all = $Window.FindAll($script:Scope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    $tiles = foreach ($e in $all) {
        $n = $e.Current.Name
        $r = $e.Current.BoundingRectangle
        if ($n -match '^page-\d+$' -and -not $r.IsEmpty) { [pscustomobject]@{ Name = $n; X = [int]$r.X; Y = [int]$r.Y; Bottom = [int]($r.Y + $r.Height) } }
    }
    Add-Report ("sheet tiles (tree order): " + (@($tiles | ForEach-Object { '{0}@{1},{2}' -f $_.Name, $_.X, $_.Y }) -join ' | '))
    $unique = @($tiles | Group-Object Name | ForEach-Object { $_.Group[0] })
    return @(Group-TilesIntoRows $unique | ForEach-Object { $_.Name })
}

function Group-TilesIntoRows {
    # Sorts by top edge and starts a new row when a tile begins at or below the bottom of the current row; each row is left to right.
    param($Tiles)
    $rows = New-Object System.Collections.Generic.List[object]
    $rowBottom = [int]::MinValue
    foreach ($t in ($Tiles | Sort-Object Y)) {
        if ($rows.Count -eq 0 -or $t.Y -ge $rowBottom) { $rows.Add((New-Object System.Collections.Generic.List[object])); $rowBottom = $t.Bottom }
        $rows[$rows.Count - 1].Add($t)
        if ($t.Bottom -gt $rowBottom) { $rowBottom = $t.Bottom }
    }
    foreach ($row in $rows) { $row | Sort-Object X }
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
