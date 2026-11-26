---
name: kdp-listing
description: Write the Amazon KDP paperback listing for one Dino book and language (title, subtitle, series, contributors, description, 7 keywords, 3 categories, reading age, AI disclosure, price) into kdp/listings/<book-id>/<lang>.md, and check it against KDP's field limits. Use when the user says "KDP listing", "write the Amazon listing for <book>", or a slice asks for a listing pack.
---

# kdp-listing

Output: `kdp/listings/<book-id>/<lang>.md`, one `## ` heading per KDP form field in the form's order, pasted top to bottom
(model: `kdp/listings/freddy-and-the-slipper/en.md`). The ISBN is logged in `kdp/listings/isbn-log.md` (owner fills it after KDP assigns
the free ISBN; add a row per book, language and format).

## Steps

1. **Read the book.** Title, ages, credits, page count and id from the store catalog (`GET https://dinoreader.sirous.uk/api/stories`, no
   identifiers) and the story text from the `.dino` (through `DinoBookReader`) or, for a paid book that answers 402, from the source repo
   `content/dino/<stem>/story.md`. Say which one was used at the top of the file.
2. **Contributors** come from the book's `DinoCredits` (`StoryBy` names are authors; the maker, Core `DinoCredits.Maker`, is the illustrator).
   A book whose credits are missing gets the maker only; never invent a name.
3. **Page count** from the `kdp` command's `check-report.md` (`print-the-damn-book kdp <book.dino> --language <code> --out <dir>`), else Core
   `KdpPagePlan.TotalPages(pictureCount)`. Say which.
4. **Limits are read live, never from memory.** Open each page below in the run, cite URL and date in the listing's price section, and update
   `listing-limits.mjs` if a figure changed:
   - Metadata (title + subtitle under 200 characters, no HTML in title, subtitle, contributors, keywords; no spoilers, reviews, prices in the
     description): https://kdp.amazon.com/en_US/help/topic/G201097560 (read 2026-11-19)
   - Description, 4,000 characters with HTML tags counted: https://kdp.amazon.com/en_US/help/topic/G201189630 (read 2026-11-19)
   - Keywords, up to seven, no words from title/contributors/categories, no quotation marks, no spelling or plural variants:
     https://kdp.amazon.com/en_US/help/topic/G201298500 (2026-11-19). The page names a character limit in the form field but gives no number:
     read it off the form and write the number in the slice file.
   - Categories, three: https://kdp.amazon.com/en_US/help/topic/G200652170 (2026-11-19). The category tree is visible only after login:
     give the closest names and say so.
   - Reading age bands 0-2, 3-5, 6-8, 9-12, 13-17: https://kdp.amazon.com/en_US/help/topic/G201506310 (read 2026-11-19)
   - AI disclosure: AI-generated text, images and translations are disclosed even after edits; AI-assisted is not asked:
     https://kdp.amazon.com/en_US/help/topic/G200672390 (read 2026-11-19). This repo's decision (slice 31) answers Yes for all three.
   - Printing cost, page limits and maximum price: https://kdp.amazon.com/en_US/help/topic/G201834340 (read 2026-11-19): premium colour,
     Amazon.com, large trim (over 6.12 in wide or 9 in tall; 8.5 x 8.5 in counts) 24-40 pages 4.20 fixed, 42-828 pages 1.00 + 0.08 per
     page; no cost is given for 41 pages; pages 24 to 828; list price at most 250 USD.
   - Royalty: https://kdp.amazon.com/en_US/help/topic/G201834330 (read 2026-11-19): 60% at 9.99 USD or more, 50% at 9.98 or less.
   - Page limits by trim: https://kdp.amazon.com/en_US/help/topic/GVBQ3CMEQW3W2VL6 (read 2026-11-19): 24 to 828 for premium colour on white
     paper; some larger trims have lower maximums and the page does not state premium colour for 8.5 x 8.5 in (unconfirmed).
   - Unconfirmed, not used as a limit: that the page count must be even (Core rounds to even; no KDP page was read saying so), the
     keyword character limit (read it off the form), the supported description HTML tags beyond those on G201189630.
5. **Price**: the lowest price at or above 9.99 with a positive royalty; show the sum
   (`rate x price - printing cost`, 60% from 9.99). `proposePrice(pages)` in `listing-limits.mjs` does it.
6. **Write the file**, then run `node .claude/skills/kdp-listing/check-listing.mjs kdp/listings/<book-id>/<lang>.md` (exit 0 needed).
   Tests (quoted glob only; a bare directory runs no tests on Node 24): `node --test ".claude/skills/kdp-listing/*.test.mjs"`.

## Rules

- Description: no spoilers, no app advertising, no price, no reviews. Plain paragraphs (the supported HTML tag list was not read).
- Keywords never repeat a word (or its plural or stem) of the title, subtitle, contributors or categories; the checker enforces it.
- A keyword must describe the paper book. Before writing any keyword, read Amazon's book search suggestions for it
  (`https://completion.amazon.com/api/2017/suggestions?alias=stripbooks&prefix=<keyword>`) and drop it when they show another kind of
  product ("read aloud" suggests "books that read aloud to kids", i.e. sound books, read 2026-11-20). The checker refuses read aloud,
  audio, sound book, talking book and listening words. A keyword the owner gave in his own words stays even when it repeats a title
  word: mark it "(owner override)" (exempt from the repeated-words rule only; the robot never types the mark). (Owner, 2026-11-20; Slice 43)
- The series line is the series name and number from `kdp/catalog.json` ("Little Seagull’s Big Adventures (book 2)"), or "None" when the
  catalog has none; never a name the owner has not given.
- AI disclosure is answered per field (owner, 2026-11-21; Slice 43 K): Text and Images "Yes, Entire work, with minimal or no editing";
  Translations "None" in English, "Yes, Entire work, with minimal or no editing" in every other language. The checker requires exactly these.
  **Exception (owner, 2026-11-26):** a book whose catalog entry has `ownerWrittenText: true` (Love from Seagull Island, X Marks the Treasure!,
  Freddy and the Slipper: "I wrote the story, you helped me rewrite and edit it") answers Text "None": KDP calls text the author wrote and
  refined with AI "AI-assisted", which needs no disclosure (G200672390, read 2026-11-26). `check-listing.mjs` reads the catalog and requires it.
- Reading age is minimum 1, maximum 5 for every book, not the store catalog's ages (owner, 2026-11-20: "it can be from 1 years old");
  the description and keywords say "ages 1 to 5" too.
- The price is the lowest paying one from `proposePrice`; keeping another needs the words "owner override" in the price section.
- Anything the form needs that no page confirms goes in the report, not guessed into the file as fact.
