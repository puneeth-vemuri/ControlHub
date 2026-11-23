# ADR 0006: Windows-based iOS compilation and packaging via xtool

**Status:** Accepted

## Context
ControlHub features an iOS Flutter client for iPhone control. However, development and execution occur on a Windows 11 host. Traditional Xcode tooling requires macOS hardware.

## Decision
Adopt [xtool](https://github.com/xtool-org/xtool) as the iOS build and packaging toolchain on Windows.
- Dart and Flutter code are written, analyzed, and tested natively on Windows using the standard Flutter SDK (`flutter analyze`, `flutter test`).
- Packaging and signing the iOS `.ipa` bundle without macOS will be orchestrated using `xtool`.

## Consequences
- No requirement for a secondary macOS workstation or CI runner for development builds.
- Testing for M0/M1 is validated via `flutter analyze` and `flutter test` on the Windows host.
- Specific native iOS simulator execution is replaced with device side-loading via `xtool` or direct physical testing.
