# Check: the Dino store dialog against the live store (read-only plain GETs): the story list loads, a story starts downloading,
# and Cancel stops the download. Needs the internet. Wipes nothing: a cancelled download leaves nothing in the book cache.
# Usage: store-dialog.ps1 -Exe <PrintTheDamnBook.exe>
param([Parameter(Mandatory)] [string] $Exe)
. "$PSScriptRoot/_lib.ps1"
$Exe = Resolve-AppExe $Exe

$script:ListTimeoutSec = 40

Complete-Check -Name 'store-dialog' -Body {
    $window = Get-MainWindow -Process (Start-App -Exe $Exe)
    Invoke-Control (Wait-ById -Root $window -Id 'GetStoryButton')
    $dialog = Wait-Until -Failure 'The store dialog did not open' -Condition {
        $cond = New-Object System.Windows.Automation.PropertyCondition($script:Uia::NameProperty, 'Stories from Dino')
        $script:Uia::RootElement.FindFirst($script:Scope::Descendants, $cond)
    }
    $list = Wait-ById -Root $dialog -Id 'StoreList' -TimeoutSec $script:ListTimeoutSec
    $buttonCond = New-Object System.Windows.Automation.PropertyCondition($script:Uia::AutomationIdProperty, 'StoreCardButton')
    $cards = @($list.FindAll($script:Scope::Descendants, $buttonCond))
    Add-Report "the store listed $($cards.Count) stor(ies)"
    if ($cards.Count -lt 1) { throw 'The store list is empty.' }
    $enabled = @($cards | Where-Object { $_.Current.IsEnabled })
    if ($enabled.Count -lt 1) { throw 'No story in the list can be opened.' }
    $title = $enabled[0].Current.Name
    Invoke-Control $enabled[0]
    $outcome = Wait-Until -Failure "Downloading '$title' showed neither a progress bar nor a result" -Condition {
        # A finished download closes the dialog and UIA throws on a closed window's element: main window first, dialog guarded.
        if (Find-ById -Root $window -Id 'DirectionLeftToRight') { return 'finished' }
        try { if (Find-ById -Root $dialog -Id 'StoreCancelDownloadButton') { 'downloading' } } catch [System.Windows.Automation.ElementNotAvailableException] { }
    }
    if ($outcome -eq 'finished') { return "list loaded ($($cards.Count) stories); '$title' downloaded before Cancel could be pressed (small book), and opened" }
    Invoke-Control (Find-ById -Root $dialog -Id 'StoreCancelDownloadButton')
    Wait-Until -Failure 'Cancel did not stop the download' -Condition {
        try { -not (Find-ById -Root $dialog -Id 'StoreCancelDownloadButton') -and (Find-ById -Root $dialog -Id 'StoreList') } catch [System.Windows.Automation.ElementNotAvailableException] { $false }
    } | Out-Null
    if (Find-ById -Root $window -Id 'DirectionLeftToRight') { throw 'A cancelled download still opened the book.' }
    Invoke-Control (Wait-ById -Root $dialog -Id 'StoreCloseButton')
    "list loaded ($($cards.Count) stories); download of '$title' started and Cancel returned to the list"
}
