### Slice 16 — Top and middle bands, and an empty-space suggestion

*Built 2026-10-26 (PR #27), not yet verified on large folders, Letter paper or real noisy photos.* Slice 14 merged (PR #26).

**Touches:** `src/PrintTheDamnBook.Core/Text/**`, `src/PrintTheDamnBook.Core/Layout/**`,
`tests/PrintTheDamnBook.Core.Tests/**`, `src/PrintTheDamnBook.App/Services/**`,
`src/PrintTheDamnBook.App/Views/CheckStepView.xaml*`, `src/PrintTheDamnBook.App/ViewModels/**`, `src/PrintTheDamnBook.App/Printing/**`, `src/PrintTheDamnBook.App/Views/PrintStepView.xaml*`

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

- [x] Core: top and middle band rectangles; tests.
- [x] Core: `BandSuggestion` scoring on a pixel buffer; tests with synthetic busy/plain bands.
- [x] Core: panel opacity from the band's plainness score, clamped; tests with synthetic images.
- [x] App: band picker in the text editor, the suggested mark, decoding off the UI thread.

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

*Changed while building:*

- Sparkle sits before the label, in `SegOption`'s leading icon slot (`Themes/Controls.xaml`, outside Touches).
- Scoring runs on the 360 px thumbnail via `Services/BandAnalyzer`, off the UI thread, and judges the visible (Fill-cropped) window (Core `PictureWindow`).
- Bottom is kept unless another band is plainer by more than 0.05.
- Plainness lives on `StoryText` (`BookPage.cs`, outside Touches) and is not saved.
- Clicking the already-suggested band does not pin it (as in the mock).
- The band pick of an emptied page is kept in memory only until reopen.
- Touches widened to `Printing/**` and `PrintStepView.xaml*`: a "Cancel reading pictures" button for a pre-print wait over 500 ms.

*From review:* high pass 9 findings, applied 1, 2, 3, 4, 6; skipped 5 (design), 7, 8 (style), 9 (no tuning data). Medium pass 11 findings, applied 1, 2, 3, 9, 11; skipped 4, 5 (small buffers), 6, 7, 8, 10 (style). 264 tests pass. Desktop acceptance: 8 of 9 pass; item 7 (pre-print wait status/Cancel) partly verified: print right after typing printed the right band, status and Cancel not observed.

#### For CLAUDE.md (lifted 2026-10-26)

- Band rectangles, `BandSuggestion` scoring and panel opacity live in Core (`Text/`, `Layout/`); the App only decodes via `Services/ImageDecoder` and hands over pixels off the UI thread. A user's band pick is never re-suggested over. (Slice 16: docs/plan/slices/16-text-band-suggestion.md)

#### Not yet verified

- Pre-print status and Cancel on a large folder (wait over 500 ms).
- Letter paper.
- Suggestion quality on real noisy photos (thresholds tuned only on synthetic images).
