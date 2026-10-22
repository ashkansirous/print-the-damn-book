### Slice 12 — Android app (parked)

*Parked.* Lowest priority (user, 2026-10-22). Plan in detail before starting.

**Touches:** `src/PrintTheDamnBook.Mobile/**`, `PrintTheDamnBook.slnx`, `.github/workflows/**`

**Why.** The same booklet printing from a phone or tablet.

**Direction** (user chose .NET MAUI, 2026-10-22):

- A .NET MAUI app that **reuses `PrintTheDamnBook.Core`** unchanged (imposition, page order, layout, manual
  two-sided plan). Pages are picked from the photo library or a folder.
- Printing goes through Android's print framework (`PrintManager`) with a PDF the app renders from the same
  sheet layout. Two-sided printing depends on the printer and the print service, so the manual two-sided flow
  (Slice 6) carries over: fronts PDF, reload prompt, backs PDF.
- The Nocturne look, adapted to MAUI; Google Play distribution.

**Open before planning:** whether page picking needs folder access or the photo picker is enough; and the Play
developer account.
