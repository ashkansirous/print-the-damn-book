### Slice 05 — Release build on GitHub

*Built 2026-10-22 (awaiting merge), not yet verified on GitHub: `release.yml` has never run.*

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

- [x] `release.yml` with publish and release steps; action versions checked via `library-docs`.
- [x] csproj publish properties (`PublishSingleFile`, `SelfContained`, `RuntimeIdentifier`,
      `IncludeNativeLibrariesForSelfExtract`).
- [ ] App icon (left open: no icon asset in the repo).
- [x] README download and build sections.

**Verification.**

- Push tag `v0.1.0`; the release appears with the exe attached; the exe runs on a machine
  without the .NET SDK and opens the sample folder.

*Changed while building:*

- Built as `release.yml` on tags `vX.Y.Z[-pre]`. The build job (read-only) validates the tag, requires the tagged commit to be on `main`, runs format and test, publishes win-x64 self-contained single-file, and uploads the artifact. The release job (`contents: write`) creates the release or re-uploads with `--clobber`, passes `--prerelease` for `-` tags, and uses generated notes.
- csproj adds `AssemblyName` `PrintTheDamnBook` (the exe name), `PublishSingleFile`, `IncludeNativeLibrariesForSelfExtract` and `EnableCompressionInSingleFile`.
- Compression is on (about 65 MB against 140 MB uncompressed, ~62 MiB published); "honest size" in the plan meant ReadyToRun off, which is kept.
- App icon not done: no icon asset in the repo.
- README has a Download section with a SmartScreen note.

*From review:* two passes, 11 findings applied. Skipped: duplicate CI gating, an exe smoke test.

#### For CLAUDE.md

- The assembly and exe name is `PrintTheDamnBook`, not `PrintTheDamnBook.App`; use it in pack URIs and settings paths. (Slice 05: docs/plan/slices/05-release-pipeline.md)
- Releases are cut by pushing a `vX.Y.Z` tag on a `main` commit; `release.yml` does the rest. (Slice 05: docs/plan/slices/05-release-pipeline.md)

#### Not yet verified

- `release.yml` has never run on GitHub (needs a first tag, e.g. `v0.1.0`).
- App icon is missing.
