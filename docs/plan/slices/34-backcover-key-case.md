### Slice 34 — Dino back cover: read the store's PascalCase keys

*Planned 2026-11-19 by run-all-waves night 2026-11-19-2055, wave 1, from a finding while building Slice 27. Built as part of Slice 36, merged as PR #85 (5b3eaf7).*

**Touches:** `src/PrintTheDamnBook.Core/Dino/DinoBackCover.cs`, `tests/PrintTheDamnBook.Core.Tests/DinoBackCoverReaderTests.cs`,
`tests/PrintTheDamnBook.App.Tests/StoryFitAndBackCoverTests.cs`

**Migrations:** none

**Depends on:** nothing.

#### Why

Real store `.dino` files write the back cover with PascalCase keys. The Lion and the Mouse's `story.json` (store HD
download, extracted 2026-11-19) has
`"BackCover": { "StoryBy": "Aesop", "Retold": true, "Illustration": "Ashkan Sirous", "Month": "2026-10", "Picture": "back-cover.jpg", "NamesByLanguage": {...} }`.
`DinoBackCoverReader` reads the inner keys with exact-case `JsonElement.TryGetProperty` on the camelCase literals
`picture`, `storyBy`, `month`, `retold`, `illustration`, `namesByLanguage`. `DinoBookReader.cs:43` sets
`PropertyNameCaseInsensitive = true`, but `BackCover` is a raw `JsonElement?`, so that option never reaches the inner
keys. Result for every real book: `BackCover.Credits` is null, so the App's Dino back cover and the KDP copyright page
lose their credit lines; a `Picture` other than the fallback name `back-cover.jpg` is dropped. Every test fixture uses
camelCase (`StoryFitAndBackCoverTests.cs:359`, `:487`), so no test catches it. (Checked by `ground-in-code` on
origin/main ed0e03c.)

#### Decisions (desk, 2026-11-19)

- Keys are matched case-insensitively (camelCase and PascalCase both read), in one helper inside
  `DinoBackCover.cs`; the same caps and type checks as today apply. Nothing else about the reader changes.

#### Work

- [x] **A — Reader.** (built in Slice 36) Case-insensitive lookup of every back-cover key, `NamesByLanguage` included.
- [x] **B — Tests.** (built in Slice 36) A Core test with the PascalCase shape above (credits, retold, illustration, month, a picture
  name other than `back-cover.jpg`, names by language) and the existing camelCase shape; one App test through the
  Dino open path with PascalCase keys showing the credit lines on the back cover.

#### Verification

- `dotnet test PrintTheDamnBook.slnx` green.
- `[session]` Open The Lion and the Mouse from the Dino store in the App and see the back cover's "story by" and
  illustration lines.
