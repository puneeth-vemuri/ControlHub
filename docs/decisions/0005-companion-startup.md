# ADR 0005: Task Scheduler logon trigger for companion startup

**Status:** Accepted

## Context
The companion process must execute in the user's interactive logon session to access the active desktop window station and handle screen capture and locking. Session 0 machine services cannot launch interactive processes directly without elevated token duplication and complex session handling.

## Decision
Register `ControlHub.Companion.exe` in Windows Task Scheduler with an `AtLogOn` trigger for the current user. Provide a clean PowerShell installation script (`windows/companion/install-companion.ps1`) to register the task with limited privileges.

## Limitations
- Pre-login desktop features are unavailable; the companion starts only after the user logs in.
- Locking the workstation before any user has logged in is not supported and correctly reports `503 COMPANION_UNAVAILABLE`.
