# Not yet verified

Every open "Not yet verified" check, one block per slice, newest first. Remove a line when it passes on a device or the user confirms it.

### Slice 19: Open a book handed over by Dino Reader ([slice](slices/19-open-from-dino-reader.md))

- That `--language fa` shows the Farsi text on the Check step (only the step was checked).
- That Explorer's Open with menu lists the app.
- The fixed `handover-check.ps1` end to end.
- The `.alt-` fallback (needs a locked legacy folder).

### Slice 17: Right-to-left books (Farsi and Arabic) ([slice](slices/17-right-to-left-books.md))

- Farsi .dino: the language select offers Farsi, the switch reads Right to left, the cover is on the right, text is right-aligned in Vazirmatn. (Next run: the native Open dialog's file-name box has no AutomationId 1148; after Slice 19 a .dino can be opened by command line instead.)
- Print to PDF page order matches the RTL formula.
- Persian digits on a printed page.
- Paper print and fold of one Farsi book (user).
