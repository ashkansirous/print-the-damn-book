### Slice 05 — Release build on GitHub

*Planned.*

**Touches:** `.github/workflows/release.yml`, `README.md`, `src/PrintTheDamnBook.App/PrintTheDamnBook.App.csproj`

**Why.** The user wants an exe to run, on this machine and the next, without a dev setup.

**Decisions.**

- Tag `v*` triggers `release.yml` on `windows-latest`: `dotnet publish` the App project
  self-contained for `win-x64`, single file, ReadyToRun off (keeps the exe honest in size),
  then `gh release create` with `PrintTheDamnBook.exe` attached.
- Version comes from the tag (`-p:Version=<tag without v>`); no commit-message versioning.
- `ci.yml` (slice 1) keeps building and testing on every PR; this workflow only publishes.
- README gains a "Download" section pointing at the Releases page and a "Build from source"
  section with the two commands.

**Work.**

- [ ] `release.yml` with publish and release steps; action versions checked via `library-docs`.
- [ ] csproj publish properties (`PublishSingleFile`, `SelfContained`, `RuntimeIdentifier`,
      `IncludeNativeLibrariesForSelfExtract`), app icon.
- [ ] README download and build sections.

**Verification.**

- Push tag `v0.1.0`; the release appears with the exe attached; the exe runs on a machine
  without the .NET SDK and opens the sample folder.
