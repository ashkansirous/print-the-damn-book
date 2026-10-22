### Slice 13 — iOS app (parked)

*Parked.* Lowest priority (user, 2026-10-22). Build after Slice 12, sharing its MAUI project.

**Touches:** `src/PrintTheDamnBook.Mobile/**`, `.github/workflows/**`

**Why.** The same booklet printing from an iPhone or iPad.

**Direction** (user chose .NET MAUI, 2026-10-22):

- The iOS head of the Slice 12 MAUI app, reusing `PrintTheDamnBook.Core`.
- Printing goes through `UIPrintInteractionController` with the rendered PDF. AirPrint duplex depends on the
  printer, so the manual two-sided flow carries over.
- Building needs a Mac (or a macOS CI runner) and an Apple Developer account; TestFlight comes before the App Store.

**Open before planning:** the Apple Developer account, and the Mac/CI signing setup.
