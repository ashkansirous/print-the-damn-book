### Slice 16 — Top and middle bands, and an empty-space suggestion

*Planned.* Comes after Slice 14.

**Touches:** `src/PrintTheDamnBook.Core/Text/**`, `src/PrintTheDamnBook.Core/Layout/**`,
`tests/PrintTheDamnBook.Core.Tests/**`, `src/PrintTheDamnBook.App/Services/**`,
`src/PrintTheDamnBook.App/Views/CheckStepView.xaml*`, `src/PrintTheDamnBook.App/ViewModels/**`

**Why.** Some pictures have their empty space at the top or in the middle. The user picks the band, but the app should
suggest the emptiest one so most pages need no thought.

**Decisions** (locked with the user via RefineScope, 2026-10-26):

- *On the picture* gains a band choice: **Top / Middle / Bottom** (Bottom stays the default). Each band is at most
  30% of the picture's height.
- **Suggestion:** when a page gets text and the user hasn't picked a band, the app scores each band by how plain it is
  (low edge density and colour variance on a downscaled copy) and picks the plainest. Shown as a "suggested" mark;
  the user can change it, and their choice is saved in `book.json` and never re-suggested over.
- No fully automatic placement that the user can't see, and no free-drawn text box.
- The scoring maths is in Core, on a plain pixel buffer, unit-tested with synthetic images; the App only decodes
  (through `ImageDecoder`) and hands over the pixels, off the UI thread.

**Work.**

- [ ] Core: top and middle band rectangles; tests.
- [ ] Core: `BandSuggestion` scoring on a pixel buffer; tests with synthetic busy/plain bands.
- [ ] App: band picker in the text editor, the suggested mark, decoding off the UI thread.

**Verification.**

- `dotnet test` is green.
- On a Dino book with a picture whose sky is empty at the top, the top band is suggested; overriding it sticks after
  reopening.
