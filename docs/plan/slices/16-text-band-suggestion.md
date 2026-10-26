### Slice 16 — Top and middle bands, and an empty-space suggestion

*In progress (wave 2026-10-26), draft PR.* Slice 14 merged (PR #26).

**Touches:** `src/PrintTheDamnBook.Core/Text/**`, `src/PrintTheDamnBook.Core/Layout/**`,
`tests/PrintTheDamnBook.Core.Tests/**`, `src/PrintTheDamnBook.App/Services/**`,
`src/PrintTheDamnBook.App/Views/CheckStepView.xaml*`, `src/PrintTheDamnBook.App/ViewModels/**`, `src/PrintTheDamnBook.App/Printing/**`

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
- **Panel opacity follows plainness:** a busier band gets about 97%, a plain band about 85%, clamped, in Core, tested
  with synthetic images. (Slice 14's fixed 92% sits inside that range.)

**Work.**

- [ ] Core: top and middle band rectangles; tests.
- [ ] Core: `BandSuggestion` scoring on a pixel buffer; tests with synthetic busy/plain bands.
- [ ] Core: panel opacity from the band's plainness score, clamped; tests with synthetic images.
- [ ] App: band picker in the text editor, the suggested mark, decoding off the UI thread.

#### Design (Story Text mock, section 1d and 1f, added 2026-10-26)

Two frames (1d band picker with a suggestion, 1f a Dino book with bands on), live renders of `StoryCheck.dc.html`. The
mock agrees with the Decisions above.

- **Band control.** A "Band" segmented control, "Top" / "Middle" / "Bottom", under the placement switch, shown only for
  *On the picture*.
- **Suggestion.** A sparkle (Phosphor `sparkle`) marks the suggested band. Hint: "Suggested: the top is the plainest
  part of this picture. Pick another and it sticks." (the band name follows the suggestion).
- **After a pick.** "Your pick. I'll never suggest over it." with a pin icon.
- **Simplified in the mock:** the suggestion is a fixed table per placeholder picture, not a real score.

**New strings:** "Band", "Top", "Middle", "Bottom", and the two hints above.

**Verification.**

- `dotnet test` is green.
- On `docs/design/reference/blackberry-path.png` the Bottom band is suggested.
- On a Dino book with a picture whose sky is empty at the top, the top band is suggested; overriding it sticks after
  reopening.
