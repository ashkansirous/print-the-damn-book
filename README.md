# print-the-damn-book

Print a home-made picture book without working out the page order yourself.

Give it a folder of page images named `Cover`, `1`, `2`, ..., `Back cover`. It arranges them as
a saddle-stitch booklet (two pages per landscape sheet, double-sided), shows you every sheet
front and back, and sends the whole thing to your duplex printer in one job. Fold the stack
in the middle and the book reads in order.

Windows only. Input: JPG and PNG. Paper: A4 (default) or Letter.

## Use

The app is a three-step wizard (Open, Check, Print) ending with a fold guide. Website: https://print-the-damn-book.sirous.uk

1. Open the folder with your pages.
2. Check the sheet preview (drag pages in the strip to fix the order).
3. Pick your printer, click **Print test sheet** the first time to confirm the flip edge,
   then **Print**.

Options (scaling, flip edge, margin, paper, printer, two-sided) are remembered between runs. Page numbers (Off / Below / Above) are under More options.

Story text: type words for each page, on the picture or on its own page; saved in `book.json` in the folder.

Open a Dino Reader book (`.dino`): pictures, words and the title on the cover, and choose a language.

Printers without two-sided printing: choose Manual; print the test sheet once to check the backs' rotation.

## Download

Get `PrintTheDamnBook.exe` from the [Releases page](https://github.com/ashkansirous/print-the-damn-book/releases/latest). It is a single
self-contained file: no .NET install needed, just run it.
The exe is unsigned, so Windows SmartScreen may warn about an unknown publisher: click **More info**, then **Run anyway**.

## Licences

The Inter and Phosphor fonts are bundled under their own licences. The release is a single exe, so the texts are embedded in it:
click **Licences** at the bottom of the left rail to read them. They are also online at https://print-the-damn-book.sirous.uk/licenses
and live in `src/PrintTheDamnBook.App/Fonts/`.

## Build from source

```powershell
dotnet build PrintTheDamnBook.slnx
dotnet run --project src/PrintTheDamnBook.App
```

Status and the implementation plan: [plan.md](plan.md).
