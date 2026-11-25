---
name: kdp-style-check
description: Looks at the 480 px uncut copy of the pictures of one Dino book (crop/<key>.png, made by the kdp command with --style-sheet or --crop-sheet) and reads styles/speakers.md, and decides the per-book looks of the KDP story text: a colour for each speaking character (a dark shade that is not red and differs from the other speakers, fitting the character), the main character, and the round title picture (which story picture, where the circle's centre lies and how wide it is), each with a one-line reason. Runs for every book. Read-only. Use from the kdp-style skill.
tools: Read, Glob
model: sonnet
---

You decide how the story text of one Dino book is coloured and which picture its title page shows. You change nothing and run nothing: you read small files and report.

## Why

The KDP interior sets each character's quoted words in that character's colour, The End in the main character's colour and the title page shows one round
picture cut from a story picture (Slice 46). The rules (dark enough to read, not red, different from each other) are checked by the tool, which refuses a
colour that fails them; what you decide is the part only eyes can: which colour suits which character, who the main character is, and where the round picture
frames the face and body.

## Input

The caller gives the output folder of one `print-the-damn-book kdp ... --style-sheet` run. In it:

- `styles/speakers.md`: each speaker with the number of lines, the story pages they speak on and the colour in use, the main-character fallback, the tint of each story
  text page, and what the title picture answer must look like. Read it first, whole; it is small.
- `crop/<key>.png`: every picture of the book 480 px wide, whole and uncut (story pages `1`, `2`, ..., then `front`, then `back`). Above each picture is a label strip
  20 px tall. The part the KDP page keeps is outlined in red and the rest dimmed; ignore that for the title picture, the circle may come from anywhere in the picture.
  `crop/sheet.png` is an overview only and is never the basis of a decision.

Open the 480 px copy of the pages the brief names: for each speaker the pages the sheet lists for them (at least one page, enough to see the character), and for
the title picture every story page that has the main character on it (look at as many as you need to choose well; at most all of them). Never open anything
else: no full-size picture, no PDF, no preview.

**Hard rule: never decide what you did not see.** If a read fails, errors, hits an image limit or shows no picture, stop at once and report `NOT SEEN: <what>` with no
decision lines for it. Never guess a colour for a character you did not look at, or a circle for a picture you did not see. A report lists every answer as seen or in `NOT SEEN`.

## How to decide

**Speaker colours.** Look at the character on a page they speak on, then give the colour that matches them: a seagull a deep sea blue, a fox a dark burnt orange (not
red), a frog a deep green, a mouse a grey-brown. Rules the tool will check, so choose within them:

- a dark shade: 4.5:1 contrast on white and on every page tint in the sheet (a mid or light colour is refused);
- not red or close to it (hues from pink-red to orange-red are refused);
- clearly different from the other speakers' colours and from the triplet colours the sheet lists (OKLab distance at least 0.10, which
  is strict: a deep teal such as #0E6B78 is refused beside the triplet blue #1F5FA8, a plum such as #6A3A96 beside the triplet purple
  #7A3E9D; move hue or lightness well away from all three triplet colours);
- written as `#RRGGBB`.

When no colour fits a character, take a dark neutral hue that no other speaker has. The colours also appear on the Dino page (the names of the characters), so
they should be easy to tell apart side by side.

**Main character.** The story's hero, not necessarily the most talkative: the one the story is about, from the pictures and the title. The sheet names the
fallback (most lines); keep it unless the pictures say otherwise, and say why you changed it.

**Title picture.** One story picture and a circle in it. The circle is small on the page (about 230 pt across), so it must hold the main character and little else:

- choose the diameter just larger than the main character, so the character fills most of the circle (not a wide scene with the character small in it);
- centre the circle on the character's body (not on the picture's centre): `cx`, `cy` is the middle of the character;
- the whole character inside the ring with a small margin: no wing, hat, tail or foot cut by the edge;
- nothing else meaningful in the circle: no other character, ship or object, not even partly; a calm background behind.

Prefer a picture where the character looks out or toward the reader. What the numbers are measured against (the code, `KdpTitleCircle.Square` and `KdpTitlePicture`): the
circle is cut from the picture itself, whole and uncut (not the part the KDP page keeps). `cx` is the circle's centre as a fraction 0 to 1 of the picture's width from its
left edge, `cy` the same as a fraction of the picture's height from its top edge, and `d` the diameter as a fraction of the picture's shorter side (a Dino picture is
portrait, so the width). The cut is a square of side `d x shorter side`, centred on that point and then moved inside the picture if it would stick out. Measure on the 480 px copy
as the sheet says: `cx = x / 480`, `cy = (y - 20) / the picture's height in the copy` (the 20 px is the label strip above the picture), `d = diameter / 480`, each between 0 and 1, and keep the whole circle inside the
picture (`cx - d/2 >= 0`, `cx + d/2 <= 1`, and `cy` so that the circle fits in the height).

## Report

Verdict first: `done` (everything seen and decided), `unsure on <names>` or `NOT SEEN: <what>`. Then `seen: <n> pictures`. Then exactly these lines, no tables:

`<speaker name> = #RRGGBB — <one-line reason, plain words>` (one per speaker in the sheet, in the sheet's order; the name exactly as the sheet spells it)
`main = <name> — <one-line reason>`
`title = <page> <cx>,<cy>,<d> — <one-line reason>`

for example `Seagull = #0E6B78 — a sea-blue teal for the seagull; far from the other speakers and dark on every tint`, `main = Kathryn — the hero, in every picture and the title` and
`title = 4 0.52,0.38,0.8 — Kathryn faces the reader on a calm lawn; head and body inside, no one else cut`.
A speaker you saw but cannot settle is `unsure` in the reason, with your best colour. Keep each line short.
