# ControlHub — Windows Components

This directory contains the .NET 8 Windows components for ControlHub M1.

## Architecture

```
FastAPI agent
     │  \\.\pipe\ControlHubService  (JSON/newline over named pipe)
     ▼
ControlHub.Service   (Windows Service, Session 0, NT AUTHORITY\LocalService)
     │  \\.\pipe\ControlHubCompanion  (JSON/newline over named pipe)
     ▼
ControlHub.Companion (console app, interactive user session, started at logon via Task Scheduler)
     │
     └─ user32.dll!LockWorkStation()
```

## Projects

| Project | Description |
|---------|-------------|
| `ipc-contracts/ControlHub.IpcContracts` | Shared message types and serializer (class library) |
| `service/ControlHub.Service` | Windows Service — hosts both named pipes, routes commands |
| `companion/ControlHub.Companion` | User-session companion — executes `LockWorkStation()` |

## IPC Protocol

All messages are UTF-8 JSON followed by `\n` (newline-delimited).

### Message types

```json
// Command  (API → Service, or Service → Companion)
{"type": "cmd", "cmd": "lock|ping|get_status", "req_id": "<uuid>"}

// Response (Service → API, or Companion → Service)
{"type": "resp", "req_id": "<uuid>", "ok": true, "error": null, "data": null}

// Status push (Companion → Service, on connect + every 10 s)
{"type": "status", "companion_online": true, "session_user": "username", "ts": "<ISO8601>"}
```

### Supported commands

| Command | Description | Requires companion |
|---------|-------------|:-----------------:|
| `ping` | Health check — service responds directly | No |
| `get_status` | Returns `{"companion_online": bool}` | No |
| `lock` | Lock the workstation screen | **Yes** |

### Error codes

| Code | Meaning |
|------|---------|
| `COMPANION_OFFLINE` | Companion not connected or heartbeat expired |
| `COMPANION_NO_RESPONSE` | Companion connected but did not reply within 5 s |
| `COMPANION_BAD_RESPONSE` | Companion replied with unparseable data |
| `COMPANION_ERROR` | Pipe I/O error forwarding to companion |
| `LOCK_FAILED_<n>` | `LockWorkStation()` returned false; `<n>` is the Win32 error code |
| `UNKNOWN_COMMAND` | Unrecognised command string |

## Pipe security

Named pipes on Windows are inherently local — they cannot be accessed over the network without explicit SMB pipe sharing, which is not configured here. No additional network filtering is required for the pipe endpoints.

The service runs as `NT AUTHORITY\LocalService` (not LocalSystem). The pipes inherit the service's default DACL. For production hardening (M10), explicit ACLs using `PipeSecurity` should restrict `ControlHubCompanion` to `NT AUTHORITY\LocalService` + current user SID.

## Known limitations (M1)

> [!IMPORTANT]
> **Pre-login machine operations are unavailable.** The companion starts only after a user signs in (Task Scheduler logon trigger). Commands like `lock` will return `COMPANION_OFFLINE` if no user is signed in.

- Only one companion session is supported at a time (single-user machines).
- The companion is a console (`Exe`) application for M1 debugging. M10 will switch to `WinExe` with a system tray icon.
- Command forwarding holds the companion's pipe stream synchronously (acceptable for M1 single-user). M10 hardening will use a request queue.

## Setup

### Prerequisites

- Windows 10/11 or Windows Server 2019+
- .NET 8 SDK

### Install the service (requires Administrator)

```powershell
.\service\install.ps1
```

This builds, installs, and starts `ControlHubService` as `NT AUTHORITY\LocalService` with automatic restart on failure.

### Install the companion (current user, no elevation needed)

```powershell
.\companion\install-companion.ps1
```

This builds the companion and registers it in Task Scheduler to run at logon for the current user.

To start it immediately for testing:

```powershell
Start-ScheduledTask -TaskName 'ControlHub Companion'
```

### Uninstall

```powershell
# Remove service (Administrator)
.\service\uninstall.ps1

# Remove companion task
.\companion\uninstall-companion.ps1
```

## Build (all projects)

```powershell
dotnet build .\ControlHub.Windows.sln --nologo
```
