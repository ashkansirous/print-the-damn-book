### Slice 06 — Manual two-sided printing for printers without duplex

*In progress (wave 2026-10-22), draft PR.* Comes before Slice 4, because both edit `Printing/**`.

**Touches:** `src/PrintTheDamnBook.App/Printing/**`, `src/PrintTheDamnBook.App/MainWindow.xaml`,
`src/PrintTheDamnBook.Core/Layout/**`, `tests/PrintTheDamnBook.Core.Tests/Layout*`,
`tests/PrintTheDamnBook.Core.Tests/ManualDuplex*`

**Why.** The user's printer (Brother HL-L3220CW) can't print two-sided, so Slice 2 refuses it
and the app can't print a booklet at all. With manual two-sided printing, the user feeds the
paper through twice. The app prints every sheet's front, tells the user how to put the stack
back, then prints every back, in the order and rotation that line up with the fronts.

**Decisions** (user, 2026-10-22: "print the even pages, then ask me to put the papers back
again and print the odd ones"; details are recommended defaults, veto any):

- A **Two-sided** choice next to the printer picker: *Automatic (printer)* or *Manual (flip the
  stack yourself)*. When the chosen printer doesn't report short-edge duplex, the app selects
  Manual and says why. The user can pick Manual on any printer.
- Manual is two simplex jobs: pass 1 is every sheet's front (sheet 1..N); pass 2 is every
  sheet's back. Between the two passes, a dialog shows the reload instructions with
  *Print the backs* / *Cancel*. On cancel, the status says the fronts printed and the backs
  didn't. Nothing else ever prints single-sided: the CLAUDE.md rule becomes "never silently
  simplex; offer Manual".
- How the stack goes back in differs per printer, so two options control pass 2:
  *Reverse order of backs* and *Rotate backs 180°*. The defaults fit a face-down-output laser
  like the user's Brother: the user takes the stack, turns it printed-side up, and puts it in
  the tray as it is, so the order isn't reversed and rotation is off.
- In Manual mode, *Print test sheet* runs the same two passes for sheet 1 only. Its front
  carries a small "this edge feeds first" arrow and the user checks whether the back comes out
  upright. If it doesn't, they tick *Rotate backs 180°*. The options live in memory for the
  session; Slice 4 persists them with the remembered printer.
- *Separate cover job* still works: the cover sheet runs its own two passes first, then the
  "load the inner paper" prompt, then the inner sheets' two passes.
- The maths lives in Core: `ManualDuplexPlan.Build(sheets, options)` returns pass 1 and pass 2
  as ordered face lists, each with a rotate flag. The App only renders and sends them. Both
  passes keep the landscape ticket and the chosen paper size, with `Duplexing.OneSided`.

**Work.**

- [ ] Core `ManualDuplexPlan` with tests: 1, 2 and 3 sheets; reverse and rotate on/off; the
      cover split into its own run.
- [ ] Printing: a OneSided ticket path, a two-pass runner on the existing STA print thread,
      the reload dialog between the passes, and a cancel message.
- [ ] The Two-sided choice in the print panel: auto-select Manual (with a reason line) when
      duplex isn't supported. This replaces Slice 2's dead-end refusal.
- [ ] Options *Reverse order of backs* and *Rotate backs 180°*, shown in Manual mode only.
- [ ] Test sheet in Manual mode: sheet 1 over two passes, with the feed-edge arrow on the front.
- [ ] CLAUDE.md printing rule updated (via the index pass after merge).

**Verification.**

- `dotnet test`: `ManualDuplexPlan` cases are green.
- Desktop, on a virtual printer (Microsoft Print to PDF): Manual gets selected automatically.
  Pass 1 produces the fronts only (3 pages for the 3-sheet sample), the reload
  dialog appears, and pass 2 produces the backs in the expected order and rotation (check the
  PDFs).
- On the user's Brother (needs paper, so ask first): run the test sheet once, set the
  rotation option if needed, then print `Book sample` and fold it. Pages must read Cover, 1..10,
  Back cover.
