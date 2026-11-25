# KDP typography spec (Slice 46)

Every value the KDP interior uses for styled story text and the redesigned front and back pages. Source: the
owner's spec of 2026-11-25 and the "Story Text Experiments" Design canvas
(https://claude.ai/code/artifact/79577946-e48e-4087-a83b-dbba0476196f), copied to `docs/design/kdp-story-text/`.
Agreed rows are marked ★ there: `Asked.dc.html` (it renders `StoryPages.dc.html` with `rules="asked"`),
`Matter.dc.html` and `DinoLink.dc.html`. Rows 1 to 4 and `MatterToday.dc.html` are not agreed (today's look and
experiments). Pages are drawn at true size, 1 px = 1 pt, on the 8.5 in (612 pt) square page.

KDP only. The home-print App layout is untouched.

Values marked *(desk)* were not in the owner's spec or the mock and were chosen when planning; the owner can change them.

## Page and ink

| What | Value |
| --- | --- |
| Page | 612 × 612 pt, safe margin 61 pt (as today) |
| Ink | `#2B2540` (`KdpInteriorDocument.Ink`) |
| Muted ink | `#6E6880` |
| Story text block | centred, line height 1.35 (`StoryTextLayout.PageLineHeight`) |
| Story text page background | the page's tint (`TextPageTint`, as today) |

## Story text sizes

| Name | Size |
| --- | --- |
| Body | 24 pt (`KdpTrim.StoryTextPt`), regular |
| +1 size | 30 pt |
| −1 size | 19 pt |
| Song | 28 pt bold |

## Story text rules

Precedence when several rules hit one word (later wins for the property it sets; read from the mock's `style()`):
base ink → R5 voice colour → R2 size words → R7 refrain, R8 shout, R9 stretch, R10 song → R3 colour words →
R4 triplet colour and bold → R6 shapes (size and baseline, rotation).

| Rule | What | Values |
| --- | --- | --- |
| R1 Paragraph gap | A newline in the source starts a paragraph | 14 pt between paragraphs, all languages |
| R2 Big words | big, huge, enormous, great big, giant, … (per-language list) | 30 pt, bold |
| R2 Small words | little, small, tiny, … | 19 pt |
| R2 Quiet words | softly, gently, quietly, whisper, … | 19 pt, italic, `#6E6880` |
| R3 Colour words | colour word + the next word | bold, in the colour, darkened until 4.5:1 on that page's tint |
| R4 Triplets | the same word three times in a row, or three words with the same start and end that differ in the middle (splish, splash, splosh) | bold, colours in order `#C2410C`, `#1F5FA8`, `#7A3E9D` |
| R5 Voices | quoted speech in its speaker's colour; narration stays ink | speaker colours are per-book choices (below) |
| R6 Shapes | only on R4 triplets whose word is in a per-language meaning list | see the table below |
| R7 Refrain | a sentence or clause of 3+ words found 2+ times in the book | 30 pt bold (R3 still applies inside) |
| R8 Shout | 1–3 words at the start of a quote, ending in `!` | 30 pt bold |
| R9 Stretched words | long, slow, slowly, sleepy, … | letter spacing 0.22 em |
| R10 Song | a quote attributed with sang/sings | 28 pt bold, singer's colour, words bounce +2 / −2 pt alternately |

R6 shapes (word 1 / word 2 / word 3; a positive baseline is up):

| Class | Words (English, per-language list) | Move |
| --- | --- | --- |
| downward | down, fall, drop, … | baseline 0 / −3 / −6 pt |
| upward | up, high, climb, … | baseline 0 / +3 / +6 pt |
| loud sound | calls, bangs, animal noises (squawk, moo, …) | size 24 / 27 / 30 pt |
| quiet sound | a triplet with an R2 quiet word in the same sentence or its attribution | size 24 / 21 / 19 pt |
| shake | rumble, wobble, shiver, … | rotate −4° / +3° / −3°, 2 pt side margin |
| bounce | splash, hop, bounce, jump, … | baseline +3 / −2 / +2 pt |

A shaped word's line grows to make room for the move (padding equal to the shift, or the rotated box's extra
height); nothing may overlap a neighbouring line.

### R3 colour table *(desk)*

Base colours before darkening; the mock fixes only red. Black, white and grey are left out (black is ink, white
and grey can't be shown as themselves on a pale page).

| Word (en) | Base |
| --- | --- |
| red | `#C62828` (mock) |
| blue | `#1565C0` |
| green | `#2E7D32` |
| yellow, gold | `#B8860B` |
| orange | `#E65100` |
| purple | `#6A1B9A` |
| pink | `#C2185B` |
| brown | `#6D4C41` |

## Per-book choices (speakers, main character, title picture)

| What | Value |
| --- | --- |
| Speaker colour | dark shade, ≥ 4.5:1 on white and on every page tint of the book; not red; differs from the other speakers and from the R4 colours |
| Not red *(desk)* | OKLCH hue outside 345°–45° when chroma > 0.08 |
| Differs clearly *(desk)* | OKLab distance ≥ 0.10 |
| Fallback palette | `#1F4FA0`, `#0E6B78`, `#6A3A96`, `#2F6B34`, `#8A4B12`, then *(desk)* `#5B3A8C`, `#1D5C5C`, `#7A4A2A`, `#3A4F8A`, `#4A6B1F` |
| Main character | the speaker with the most lines unless the agent overrides it with a reason |

## Front and back pages (★ `Matter.dc.html`, `DinoLink.dc.html`)

### Title page

| What | Value |
| --- | --- |
| Background | tint of the first story text page |
| Picture | round, 230 pt across, 6 pt white border, soft shadow (0 2 6, 12% black), cut from a story picture |
| Gap picture to title | 28 pt |
| Title | last word on its own line, 72 pt bold; the rest 36 pt regular; a one-word title is all 72 pt; line height 1.05; R3 applies (red X) |

### Credits page

| What | Value |
| --- | --- |
| Background | white |
| Block | bottom of the page, 61 pt from the bottom, centred, 18 pt between groups |
| Role label | 13 pt `#6E6880` ("Story by", "Illustrations by" in English) |
| Name | 20 pt bold ink, 2 pt under its label |
| Date and © line | 12 pt `#6E6880`, line height 1.5 ("October 2026" / "© 2026 Elinsoft Ltd. All rights reserved."; the rights holder is the company, the credits above stay the author's) |

### This book belongs to

| What | Value |
| --- | --- |
| Frame | 3 pt dashed `#8A4B12`, corner radius 20 pt, 36 pt in from the trim, 32 pt inner padding |
| Items | centred, 22 pt apart |
| Heading | 28 pt bold ink |
| Name line | 85% of the frame width, 3 pt dotted ink, 36 pt tall |
| Draw box | 220 × 190 pt, 2 pt dashed `#6E6880`, radius 14 pt; label "Draw yourself here!" 15 pt `#6E6880` at the bottom, 12 pt in |

### The End

72 pt bold, in the main character's colour, centred, on the tint of the last story text page.

### More books page (owner, 2026-11-25)

The Slice 41 grid ("More books by Ashkan Sirous", up to 15 covers) moves to its own page just before the Dino page;
its look is unchanged. A book with no other covers has no such page.

### Dino page (last page)

| What | Value |
| --- | --- |
| Background | white, 61 pt side padding, items centred, 18 pt apart, line height 1.35 |
| QR code | 150 pt, points at `https://dinoreader.sirous.uk/` |
| Under the QR | "Scan the code, or type" 14 pt `#6E6880`; the short address `dinoreader.sirous.uk` 26 pt bold, letter spacing 0.02 em; "Google Play · App Store · Microsoft Store" 13 pt `#6E6880`; 2 pt apart |
| Then | "Listen to this story in Dino: Kids Books & Stories." 20 pt |
| Then | "Want to join in? Pick a character and **speak their lines** yourself!" 24 pt, "speak their lines" bold |
| Then | the speaking characters' names, each in its R5 colour, 20 pt bold, wrapped, 6 pt row gap, 16 pt column gap |

The short address is one Core constant for all books: `dinoreader.sirous.uk` (owner, 2026-11-25; the mock's
`dino.sirous.uk` does not exist). The back cover keeps `KdpFrontMatterWording.DinoLine` as it is.

## Fonts

Andika Regular, Bold and Italic; Vazirmatn Regular and Bold for Arabic script. Files in
`src/PrintTheDamnBook.App/Fonts/`, linked as Kdp embedded resources, licences in `Services/FontLicences`.
