<#
.SYNOPSIS
  Captures the real app window for the Store screenshots into captures/*.png (committed inputs of render.mjs).
.DESCRIPTION
  Launches the exe, loads the sample folder through the folder dialog (UI Automation, nothing in the app is changed),
  sizes the window to 1540x1065 px (the 2f frame's 616x426 aspect; every page row of the sample book fits) and screenshots
  it on the Check (Reading order), Check (As printed) and Print steps. Needs a 100% scaled display with the window
  on screen. Re-run after the UI changes, then run `npm run render`.
.PARAMETER ExePath
  Built PrintTheDamnBook.exe (Release publish or build output).
.PARAMETER SampleDir
  Folder with the sample pages (Cover.jpg, 1.jpg .. 10.jpg, Back cover.jpg).
.PARAMETER PrinterName
  Printer to select on the Print step (so no private printer name ends up in a Store image).
#>
param(
  [Parameter(Mandatory)] [string] $ExePath,
  [Parameter(Mandatory)] [string] $SampleDir,
  [string] $PrinterName = 'HP Smart Printing',
  [int] $WindowWidth = 1540,
  [int] $WindowHeight = 1065
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class Native {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int cx, uint flags);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr wParam, string lParam);
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr wParam, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
'@
[void][Native]::SetProcessDPIAware()

function Assert-CaptureEnvironment {
  $installed = [System.Drawing.Printing.PrinterSettings]::InstalledPrinters
  if ($installed -notcontains $PrinterName) { throw "Printer '$PrinterName' is not installed (pass -PrinterName; installed: $($installed -join ', '))." }
  $graphics = [System.Drawing.Graphics]::FromHwnd([IntPtr]::Zero)
  try { $dpi = $graphics.DpiX } finally { $graphics.Dispose() }
  if ($dpi -ne 96) { throw "The display is scaled to $([int]($dpi / 96 * 100))%; set it to 100% so the captures match the artboards." }
}
Assert-CaptureEnvironment


# The folder dialog's "Folder" edit box and its confirm button ("Select Folder").
$DialogFolderEditId = '1152'; $DialogConfirmButtonId = '1'
$WM_SETTEXT = 0x000C; $BM_CLICK = 0x00F5
$outDir = Join-Path $PSScriptRoot 'captures'
New-Item -ItemType Directory -Force $outDir | Out-Null
$UIA = [Windows.Automation.AutomationElement]
$Scope = [Windows.Automation.TreeScope]

function Find-Named($root, [string] $name, $controlType = $null, [int] $timeoutSec = 20) {
  $conditions = @((New-Object Windows.Automation.PropertyCondition($UIA::NameProperty, $name)))
  if ($controlType) { $conditions += New-Object Windows.Automation.PropertyCondition($UIA::ControlTypeProperty, $controlType) }
  $condition = if ($conditions.Count -gt 1) { New-Object Windows.Automation.AndCondition($conditions) } else { $conditions[0] }
  $deadline = (Get-Date).AddSeconds($timeoutSec)
  do {
    $hit = $root.FindFirst($Scope::Descendants, $condition)
    if ($hit) { return $hit }
    Start-Sleep -Milliseconds 300
  } while ((Get-Date) -lt $deadline)
  throw "UI element '$name' not found."
}

function Find-ById($root, [string] $automationId, [int] $timeoutSec = 20) {
  $condition = New-Object Windows.Automation.PropertyCondition($UIA::AutomationIdProperty, $automationId)
  $deadline = (Get-Date).AddSeconds($timeoutSec)
  do {
    $hit = $root.FindFirst($Scope::Descendants, $condition)
    if ($hit) { return $hit }
    Start-Sleep -Milliseconds 300
  } while ((Get-Date) -lt $deadline)
  throw "UI element with AutomationId '$automationId' not found."
}

function Invoke-Element($element) {
  for ($i = 0; $i -lt 100 -and -not $element.Current.IsEnabled; $i++) { Start-Sleep -Milliseconds 300 }
  $pattern = $null
  if ($element.TryGetCurrentPattern([Windows.Automation.InvokePattern]::Pattern, [ref] $pattern)) { $pattern.Invoke(); return }
  if ($element.TryGetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern, [ref] $pattern)) { $pattern.Select(); return }
  throw "Element '$($element.Current.Name)' cannot be invoked."
}

# The shot is a fixed-size crop at the window's top-left: refuse anything that is not exactly the window.
function Assert-CaptureBounds($hwnd, $rect) {
  $width = $rect.Right - $rect.Left; $height = $rect.Bottom - $rect.Top
  if ($width -ne $WindowWidth -or $height -ne $WindowHeight) {
    throw "Window is ${width}x${height}, expected ${WindowWidth}x${WindowHeight} (display scaling not 100%, or the window was clamped)."
  }
  $screen = [Windows.Forms.SystemInformation]::VirtualScreen
  if ($rect.Left -lt $screen.Left -or $rect.Top -lt $screen.Top -or $rect.Right -gt $screen.Right -or $rect.Bottom -gt $screen.Bottom) {
    throw 'Window is not fully on screen.'
  }
  # The app is topmost, so nothing normal can cover it; try to make it foreground too, and say so if Windows refuses.
  for ($i = 0; $i -lt 10 -and [Native]::GetForegroundWindow() -ne $hwnd; $i++) { [void][Native]::SetForegroundWindow($hwnd); Start-Sleep -Milliseconds 300 }
  if ([Native]::GetForegroundWindow() -ne $hwnd) { Write-Warning 'The app window is topmost but not the foreground window; check the shot for other topmost windows.' }
}

function Save-WindowShot($hwnd, [string] $file) {
  $rect = New-Object Native+RECT
  [void][Native]::GetWindowRect($hwnd, [ref] $rect)
  Assert-CaptureBounds $hwnd $rect
  $bitmap = New-Object Drawing.Bitmap($WindowWidth, $WindowHeight)
  $graphics = [Drawing.Graphics]::FromImage($bitmap)
  $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)
  $bitmap.Save((Join-Path $outDir $file), [Drawing.Imaging.ImageFormat]::Png)
  $graphics.Dispose(); $bitmap.Dispose()
  Write-Host "captured $file"
}

function Open-SampleFolder($window) {
  Invoke-Element (Find-Named $window 'Open folder' ([Windows.Automation.ControlType]::Button))
  $dialog = Find-Named ([Windows.Automation.AutomationElement]::RootElement) 'Choose the folder with the page images' ([Windows.Automation.ControlType]::Window)
  # The dialog's controls are legacy windows without UIA patterns: find each by AutomationId, then message its window
  # (WM_SETTEXT, BM_CLICK). No keystrokes, no clipboard.
  $edit = Find-ById $dialog $DialogFolderEditId
  [void][Native]::SendMessage([IntPtr] $edit.Current.NativeWindowHandle, $WM_SETTEXT, [IntPtr]::Zero, $bookDir)
  $confirm = Find-ById $dialog $DialogConfirmButtonId
  [void][Native]::SendMessage([IntPtr] $confirm.Current.NativeWindowHandle, $BM_CLICK, [IntPtr]::Zero, [IntPtr]::Zero)
  # Loading a folder moves on to the Check step by itself.
  [void](Find-Named $window 'Reading order' ([Windows.Automation.ControlType]::RadioButton) 60)
}

# Automatic two-sided must be on (no manual-mode warning), so "Print backs again" must not be showing.
function Assert-AutomaticMode($window) {
  Start-Sleep -Seconds 1
  $name = New-Object Windows.Automation.PropertyCondition($UIA::NameProperty, 'Print backs again')
  $button = $window.FindFirst($Scope::Descendants, $name)
  if ($button -and -not $button.Current.IsOffscreen) { throw "'Print backs again' is visible with '$PrinterName' selected: manual mode is on (printer lacks duplex?) or the app shows it in Automatic mode." }
}

function Select-Printer($window) {
  $combo = Find-Named $window 'Printer' ([Windows.Automation.ControlType]::ComboBox)
  $combo.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
  Start-Sleep -Seconds 1
  # The items are named after their view-model's ToString(): "PrinterChoice { Name = <printer>, ... }".
  $isListItem = New-Object Windows.Automation.PropertyCondition($UIA::ControlTypeProperty, [Windows.Automation.ControlType]::ListItem)
  $item = $combo.FindAll($Scope::Descendants, $isListItem) | Where-Object { $_.Current.Name -like "*Name = $PrinterName,*" } | Select-Object -First 1
  if (-not $item) { $item = [Windows.Automation.AutomationElement]::RootElement.FindAll($Scope::Children, [Windows.Automation.Condition]::TrueCondition) | Where-Object { $_.Current.ClassName -eq 'Popup' } | ForEach-Object { $_.FindAll($Scope::Descendants, $isListItem) } | Where-Object { $_.Current.Name -like "*Name = $PrinterName,*" } | Select-Object -First 1 }
  if (-not $item) { throw "Printer '$PrinterName' not in the list." }
  Invoke-Element $item
  $combo.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
}

# A neutral folder name, so the title bar and path in the shots show "Picture book".
$bookDir = Join-Path ([IO.Path]::GetTempPath()) 'ptdb-capture\Picture book'
New-Item -ItemType Directory -Force $bookDir | Out-Null
Copy-Item (Join-Path $SampleDir '*.jpg') $bookDir
$process = Start-Process $ExePath -PassThru
try {
  $deadline = (Get-Date).AddSeconds(30)
  while ($process.MainWindowHandle -eq 0 -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 300; $process.Refresh() }
  $hwnd = $process.MainWindowHandle
  [void][Native]::SetWindowPos($hwnd, [IntPtr]::new(-1), 100, 100, $WindowWidth, $WindowHeight, 0x0040)
  [void][Native]::SetForegroundWindow($hwnd)
  Start-Sleep -Milliseconds 500
  $window = $UIA::FromHandle($hwnd)
  Start-Sleep -Seconds 1

  Open-SampleFolder $window
  Start-Sleep -Seconds 2
  Save-WindowShot $hwnd 'app-check-reading-order.png'

  Invoke-Element (Find-Named $window 'As printed' ([Windows.Automation.ControlType]::RadioButton))
  Start-Sleep -Seconds 2
  Save-WindowShot $hwnd 'app-check-as-printed.png'

  Invoke-Element (Find-Named $window 'Next: printing' ([Windows.Automation.ControlType]::Button))
  Select-Printer $window
  Assert-AutomaticMode $window
  Start-Sleep -Seconds 2
  Save-WindowShot $hwnd 'app-print.png'
} finally {
  if (-not $process.HasExited) { $process.Kill() }
  Remove-Item (Split-Path $bookDir) -Recurse -Force
}
