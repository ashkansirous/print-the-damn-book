# CLAUDE.md: tools/desktop-vm

Rules for the runner over the shared test VM.

- The VM `rtst-desktop-test` is shared with ReadTheStupidText and Dino Reader. Take its lock (`Global\rtst-desktop-test-vm`), restore `clean` before and after every run, leave it Saved, and never create, change or delete a VM or checkpoint, or write to `D:/secrets`. (Slice 21: docs/plan/slices/21-desktop-test-harness.md)
- The password is RTST's `DESKTOP_VM_PASSWORD` in `D:/secrets/readthestupidtext/secrets.env`; read it, never print it or ask for it.
- The package under test is the self-contained exe from `Build-TestPackage.ps1` (same command as `release.yml`), not an MSIX.
- A restored `clean` cold-boots: wait for the guest desktop (`Wait-Desktop`) before registering the interactive task.
- Scripts written with the Write tool can get a backslash-r sequence turned into a literal CR byte; check the bytes of any `.ps1` you write, and parse it (`[System.Management.Automation.Language.Parser]::ParseFile`) before reporting.
- Test the scripts with `-WhatIf`; run nothing against the VM from a build session.
