### Slice 33 — Amazon KDP: other languages, right-to-left and Farsi/Arabic Kindle

*Planned 2026-11-19 from the Scope Inbox doc
[2026-11-18-amazon-kdp-export.md](https://drive.google.com/file/d/16B6KoIGXtHRXaRS8VvQcopP5RyJhcUIM/view?usp=drivesdk)
(milestone 7). Same gate as Slice 32: starts only when the owner says the first two paperbacks sold enough (doc's kill
rule, 2026-11-18).*

**Touches:** `src/PrintTheDamnBook.Core/Kdp/KdpFrontMatterWording.cs`, `src/PrintTheDamnBook.Core/Kdp/KdpPagePlan.cs`,
`src/PrintTheDamnBook.Kdp/**`, `.claude/skills/kdp-listing/**`, `tests/PrintTheDamnBook.Core.Tests/Kdp/**`,
`tests/PrintTheDamnBook.Kdp.Tests/**`

**Depends on:** Slices 27–31 (32 for Farsi/Arabic, which are Kindle only).

#### Why

The Dino books exist in several languages. KDP prints paperbacks in many of them, but not in Farsi or Arabic (Kindle only;
KDP's Arabic page says paperback publishing for Arabic isn't currently available).

#### Decisions

- Approved (doc): KDP-supported languages first (es, fr, de, pt, ...); Farsi/Arabic as Kindle only; a bilingual
  English–Farsi print via IngramSpark is later and not in this slice.
- Decided by this planning session, 2026-11-19: text comes from the `.dino` `Translations` through the same path as
  Slice 25's language switch; front and back matter wording per language in Core `KdpFrontMatterWording`; right-to-left
  books mirror the spread through Core `TextDirection` and use Vazirmatn via `StoryFont.For`, with Farsi digits from
  `PageNumbering.DigitsFor`. Arabic shaping in QuestPDF is checked with Vazirmatn first (QuestPDF supports content
  direction; shaping quality unconfirmed on 2026-11-19); if it fails, shaping moves to `SkiaSharp.HarfBuzz`.

#### Work

- [ ] Per-language front/back matter wording, unit tested.
- [ ] Right-to-left page plan and rendering; a Farsi Freddy Kindle file.
- [ ] `kdp-listing` writes listings per language.

#### Verification

- Unit tests on both platforms; a Spanish Freddy paperback PDF and a Farsi Freddy Kindle file built, with the contact
  sheet checked by `kdp-visual-check` `[session]`.
- `[owner]` Upload one non-English paperback draft and the Farsi Kindle draft; KDP's warnings are written here.
