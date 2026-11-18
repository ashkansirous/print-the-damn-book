# Check: a .dino with a back cover and one page with more words than fit its band (Slice 23). The Check step names the page the fit moved
# to its own page (MovedTextNotice), the back cover is the last page (its tile is there, no "No Back cover file" warning) and the book
# opens without an alert. Wipes nothing: the sample .dino lives in %TEMP% and is removed; the app is started and stopped.
# The credits are drawn into the page picture, so their wording and position are not readable here (unit tests cover them);
# a PDF of this book in its own language and in Farsi is the human check listed in docs/plan/slices/23-fit-and-back-cover.md.
# Usage: back-cover-and-fit.ps1 -Exe <PrintTheDamnBook.exe>
param([Parameter(Mandatory)] [string] $Exe)
. "$PSScriptRoot/_lib.ps1"
$Exe = Resolve-AppExe $Exe

$script:MovedNoticeId = 'MovedTextNotice'
$script:NoBackCoverNote = 'No Back cover file'
$script:BackCoverLabel = 'back cover'
$script:LongSentence = 'Pancake Polly ran all the way along the sandy shore, past the lighthouse and the old wooden boats, waving the bottle over her head. '

function New-BackCoverDino {
    # Three pages (the second has far more words than a band holds, with empty lines between its sentences) and a back cover as the last
    # page, the way Dino Reader writes it. Returns the .dino path.
    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    $path = Join-Path ([IO.Path]::GetTempPath()) ("ptdb-backcover-" + [Guid]::NewGuid().ToString('N').Substring(0, 8) + '.dino')
    $zip = [System.IO.Compression.ZipFile]::Open($path, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        $long = (1..10 | ForEach-Object { $script:LongSentence }) -join "`n`n"
        $credits = [ordered]@{ storyBy = 'Aesop'; retold = $true; illustration = 'Ashkan Sirous'; month = '2026-11'; picture = 'back-cover.png' }
        $pages = @(
            [ordered]@{ Text = 'Short words.'; ImageFile = 'page-1.png' },
            [ordered]@{ Text = $long; ImageFile = 'page-2.png' },
            [ordered]@{ Text = 'The end.'; ImageFile = 'page-3.png' },
            [ordered]@{ Text = 'Original story by Aesop'; ImageFile = 'back-cover.png'; BackCover = $credits }
        )
        for ($i = 1; $i -le 3; $i++) { Add-ZipPng -Zip $zip -Name "page-$i.png" -Hex $script:SampleColours[$i - 1] }
        Add-ZipPng -Zip $zip -Name 'back-cover.png' -Hex $script:SampleColours[3]
        Add-ZipText -Zip $zip -Name 'package.json' -Text (@{ SchemaVersion = 1; Id = 'ptdb-backcover-test'; Title = 'Back cover test'; Language = 'en' } | ConvertTo-Json -Compress)
        Add-ZipText -Zip $zip -Name 'story.json' -Text (@{ Id = 'ptdb-backcover-test'; Title = 'Back cover test'; Language = 'en'; Pages = $pages } | ConvertTo-Json -Compress -Depth 6)
    } finally { $zip.Dispose() }
    return $path
}

Complete-Check -Name 'back-cover-and-fit' -Body {
    $dino = New-BackCoverDino
    try {
        $window = Get-MainWindow -Process (Start-App -Exe $Exe -Arguments @($dino))
        Wait-ForCheckStep -Window $window
        $notice = Wait-ById -Root $window -Id $script:MovedNoticeId
        Add-Report "moved-pages notice shown: '$($notice.Current.Name)'"
        $names = @(Get-AllNames -Root $window | Where-Object { $_ })
        Save-Screenshot -Name 'back-cover-and-fit'
        if (-not ($names | Where-Object { $_ -eq $script:BackCoverLabel })) { throw "No '$script:BackCoverLabel' tile on the Check step; names seen: $(($names | Select-Object -First 30) -join ' / ')" }
        if ($names | Where-Object { $_.Contains($script:NoBackCoverNote) }) { throw "The Check step still warns '$script:NoBackCoverNote' although the book has a back cover." }
    } finally { Remove-Item $dino -ErrorAction SilentlyContinue }
    'long page moved to its own page and is named; the back cover is the last page with no missing-back-cover warning'
}
