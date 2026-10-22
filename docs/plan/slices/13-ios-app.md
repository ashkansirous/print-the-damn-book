### Slice 13 — iOS app (parked)

*Parked.* Lowest priority (user, 2026-10-22). Build after Slice 12, sharing its MAUI project.

**Touches:** `src/PrintTheDamnBook.Mobile/**` (the iOS head, `Platforms/iOS/**`, and iOS branches of shared views), `.github/workflows/**`

**Why.** The same booklet printing from an iPhone or iPad.

**Direction** (user chose .NET MAUI, 2026-10-22):

- The iOS head of the Slice 12 MAUI app, reusing `PrintTheDamnBook.Core`.
- Printing goes through `UIPrintInteractionController` with the rendered PDF. AirPrint duplex depends on the
  printer, so the manual two-sided flow carries over.
- Building needs a Mac (or a macOS CI runner) and an Apple Developer account; TestFlight comes before the App Store.

**Open before planning:** the Apple Developer account, and the Mac/CI signing setup.

#### Design (Redesign Directions mock, section t4 (frames 4a-4g), added 2026-10-23)

Seven frames; four are iOS (4a Open, 4b Check with a warned page selected, 4c Print with More options open, 4d AirPrint sheet for pass 1). The Android frames (4e-4g) share layout, copy and logic, so the full per-element record (shell, Open, Check, reorder, Print, More options, reload prompt, Done, all copy) is in the Design subsection of `docs/plan/slices/12-android-app.md`; it is not repeated here. Prototype: `docs/design/MobileWizard.dc.html` with `platform="ios"`. The iOS bezel, status bar and home indicator (`ios-frame.jsx`) are mock chrome; the AirPrint sheet in 4d is the system's UI, drawn to show what the app hands over. The mock agrees with this slice except for the open points below.

iOS differences from Android:

- **Top bar (4a, 4b).** 54px status spacer, then a 44px bar: `ph ph-caret-left` + `Back` on the left (16px, `--color-accent-300`, 44px min height) on Check and Print; title centred, 15px weight 500 (`Print the damn book` on Open, then `Check`, `Print`, `Done`); `ph ph-plus` "Add pages" on the right on Check. Android has an arrow and the app icon instead.
- **Open (4a).** Primary `Choose from Photos` (`ph ph-images`, 48px), secondary `Choose a folder in Files` (`ph ph-folder-open`). Folder source line `Files › On My iPhone › Downloads`; Photos `12 photos` over `Photos · picked in order`. Same rules: pick order for Photos, name order for a folder. 4a uses the `issues` sample, folder name `good enough draft`.
- **Bottom bar (4b).** A home-indicator spacer sits under the main button. 4b shows `IMG_2041.jpg` selected: `Name not recognised, so it went to the end. Tap where it should go.`, with the 44px trash and x buttons.
- **Print (4c).** Same fields as Android. More options open on the Manual default: `Reverse order of backs`, `Rotate backs 180°`, `Scaling`, `Margin` (`5` mm), `Page numbers`; `Flip edge` is hidden.
- **AirPrint sheet (4d).** Bottom sheet `#1c1c1e`, 14px top radius. Header row 56px: `Cancel` and `Print` in `#0a84ff` 17px (Print weight 600), title `Print Options` 17px weight 600. Grouped list (`#2c2c2e`, 10px radius, 44px rows, 17px text, values `rgba(235,235,245,.6)`, `ph ph-caret-right` on chevron rows): `Printer` `Brother HL-L3220CW` (chevron), `Copies` `1`, `Paper Size` `A4` (chevron), `Double-sided` (`Off` in Manual, `On` in Automatic), `Range` `All {n} pages` (n = faces in this pass). Below, a horizontal strip of 150x106 white page previews captioned `Sheet N front` / `Sheet N back` (12px). Manual pass 1 shows only the fronts.
- **Hand-off.** `UIPrintInteractionController` with the pass PDF; the shared reload dialog (frame 4g) appears between passes.

**New strings:** `Choose from Photos`, `Choose a folder in Files`, `Files › On My iPhone › Downloads`; the rest are in Slice 12.

**Open point: printer name on iOS.** The mock shows `Brother HL-L3220CW` in hints and status lines before printing; `UIPrintInteractionController` reveals the printer only after the user picks it in the sheet. **Default:** until a printer is known, say `Your printer` in the hint and status lines; afterwards use the name the controller returns.

**Open point: Manual default on iOS.** Same as Slice 12: the mock starts on `I flip the stack`; Core's `PrinterSettings` default is Automatic (`src/PrintTheDamnBook.Core/Layout/PrintSettings.cs:7`). **Default:** the Mobile view model sets Manual; Core unchanged.

**Open point: Double-sided row.** The real AirPrint sheet lets the user switch Double-sided on, which would fight Manual's two one-sided jobs; the mock draws it as a read-only `Off`. **Default:** set `UIPrintInfo.Duplex` to none for Manual jobs and test on a real device whether the user can still change it; keep the `I flip the stack` hint visible either way.

#### Sub-slices (ordered, vertical; everything shared is built in Slice 12 and only wired up here)

Slice 12 owns, and Slice 13 does **not** redo: the MAUI project, Nocturne resource dictionaries and controls, the Open/Check/Print/Done pages and view models, Core position-based classification, the PDF renderer, the manual two-sided sequence and settings. Slice 13 adds the iOS head and the iOS-only parts below.

- [ ] **13.1 iOS head builds and runs.** Enable the iOS target in `PrintTheDamnBook.Mobile`, bundle id, Info.plist photo-library usage text, simulator run of the shared Check screen. A macOS CI job (build only, unsigned). Needs the Mac/CI decision (still open).
- [ ] **13.2 Open on iOS (4a).** The Photos picker (multi-select, pick order kept) and the Files document picker for a folder, holding security-scoped access while pages load; labels `Choose from Photos` and `Choose a folder in Files`; source line `Files › On My iPhone › Downloads`.
- [ ] **13.3 Check and Print on iOS (4b, 4c).** Check the shared screens against the iOS frames: `Back` button, home-indicator spacer, safe areas, Dynamic Type at the default and one larger size, VoiceOver names on tiles, trash, x and the segmented controls.
- [ ] **13.4 AirPrint hand-off (4d).** `UIPrintInteractionController` with the pass PDF and `UIPrintInfo` (job name from the shared job names, landscape, `Duplex` per the open point); a sheet on iPhone, a popover on iPad. The completion handler maps cancelled / failed / done to the shared status lines.
- [ ] **13.5 Manual two-sided on iOS (4d, then the dialog).** Run the shared sequence: fronts, `Fronts are done` dialog, backs; cover-sheet split and test sheet. Hands-on check on a real iPhone with an AirPrint printer.
- [ ] **13.6 Done step and iPad.** Fold steps, `Print another copy`, `Make another book`; wider Check grid on iPad; dark appearance only.
- [ ] **13.7 Distribution.** Signing in CI, TestFlight, then the App Store. Needs the Apple Developer account (still open).
