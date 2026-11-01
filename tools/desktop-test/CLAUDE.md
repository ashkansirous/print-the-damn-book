# CLAUDE.md: tools/desktop-test

Rules for the scripted desktop checks.

- Device-test agents run these scripts first (in the VM, `tools/desktop-vm/Invoke-DesktopTest.ps1`). A check that is missing is added here as a new script that exits 0/1 and is listed in `run-all.ps1`, never written as a throw-away in a temp folder. (Slice 21: docs/plan/slices/21-desktop-test-harness.md)
- Find controls by `AutomationProperties.AutomationId` (`_lib.ps1` `Find-ById`), never by text or position. A control a check needs and lacks an id gets one in XAML (`AutomationProperties.AutomationId` only).
- Never run these on the host desktop; the VM runs them. Parse every script after editing (`[System.Management.Automation.Language.Parser]::ParseFile`), and check the bytes for a stray CR or control character: a backslash-r sequence written by a tool can become a literal CR.
- A script that wipes state says what in a comment. These only start and stop the app and use `%TEMP%` folders they remove.
