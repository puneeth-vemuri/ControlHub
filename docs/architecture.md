# ControlHub Architecture and Compact Repository Map

**Generated for Milestone 0 / Milestone 1 Foundation.**
This document is a compact, machine-readable map of repository boundaries, key entry points, contracts, and invariants. Future coding agents should read this first before scanning source trees.

---

## 1. Module Boundaries and Key Entry Points

| Component | Directory | Technology / Runtime | Entry Point | Primary Role |
|---|---|---|---|---|
| **API Host** | `backend/controlhub_api/` | Python 3.13 / FastAPI | `main.py:app` | Loopback-bound REST API (`127.0.0.1:8765`), auth, device capabilities, power orchestration, audit trail. |
| **Windows Service** | `windows/service/ControlHub.Service/` | C# .NET 8 Worker | `Program.cs` | Machine-level service (Session 0). Hosts dual named pipes, routes commands, manages companion state, executes machine power (sleep/hibernate via powrprof.dll). |
| **Service Core** | `windows/service/ControlHub.Service.Core/` | C# .NET 8 Class Library | `ServicePipeWorker.cs`, `CompanionPipeWorker.cs` | Core workers and IPC abstractions (`IPowerExecutor`, `CompanionState`) reusable across service and integration tests. |
| **User Companion** | `windows/companion/ControlHub.Companion/` | C# .NET 8 Console | `Program.cs` | Interactive session process (User Session). Started via Task Scheduler at logon; executes `LockWorkStation()`. |
| **IPC Contracts** | `windows/ipc-contracts/ControlHub.IpcContracts/` | C# .NET 8 Class Library | `Messages.cs`, `PipeNames.cs` | Shared message definitions (JSON-lines) and pipe names for service-companion communication. |
| **Mobile App** | `apps/mobile/` | Flutter 3.44 / Dart 3.12 | `lib/main.dart` | iPhone controller UI (iOS-first). Dashboard, quick power actions, Keychain credential storage. Built via `xtool` on Windows. |

---

## 2. Dependency Graph

```
[iPhone Flutter App]
        │
        ▼ (HTTPS via Tailscale private tailnet - M2)
[FastAPI API Host] (127.0.0.1:8765 loopback only)
        │
        ├─► [SQLite: backend/data/controlhub.db] (WAL mode, FKs, audit + power_operations)
        │
        ├─► (Win32 OS APIs: advapi32.dll InitiateSystemShutdownExW, AbortSystemShutdownW)
        │   [Windows OS Kernel] (Shutdown, Restart with 30s delay & cancellation)
        │
        ▼ (Named Pipe: \\.\pipe\ControlHubService)
[Windows Machine Service: ControlHub.Service] (Session 0)
        │
        ├─► (Win32: powrprof.dll SetSuspendState(bForce: false)) -> [Sleep / Hibernate]
        │
        ▼ (Named Pipe: \\.\pipe\ControlHubCompanion)
[User-Session Companion: ControlHub.Companion] (Interactive Session)
        │
        ▼ (Win32: user32.dll LockWorkStation())
[Windows Desktop Session Lock]
```

---

## 3. Public API Contract (M0/M1 Scope)

- **Base URL:** `http://127.0.0.1:8765`
- **Protocol:** HTTP/1.1 JSON UTF-8. Timestamps ISO-8601 UTC.
- **Authentication:** `Authorization: Bearer <32+ char token>` (`CONTROLHUB_API_TOKEN`). Unauthenticated requests return 401. Short token returns 503.
- **Tracing:** `X-Request-ID` header accepted or generated (UUIDv4) and returned on all responses.

### Implemented Endpoints

| Method | Path | Auth Required | Purpose | Response Behavior |
|---|---|---|---|---|
| `GET` | `/api/v1/health` | No | Minimal liveness check | `{"status": "ok", "timestamp": "..."}` |
| `GET` | `/api/v1/device/status` | Yes | Host, OS, uptime, agent/companion state | Dynamic companion check via named pipe |
| `GET` | `/api/v1/device/capabilities` | Yes | Hardware and power capabilities | `power.{shutdown, restart, sleep, hibernate, cancel, lock}` |
| `POST` | `/api/v1/power/shutdown` | Yes | Initiate system shutdown (30s delay default) | Requires `{"confirm": true}`. Returns operation ID & cancellable flag. |
| `POST` | `/api/v1/power/restart` | Yes | Initiate system restart (30s delay default) | Requires `{"confirm": true}`. Returns operation ID & cancellable flag. |
| `POST` | `/api/v1/power/sleep` | Yes | Put laptop to sleep via `SetSuspendState` | Non-cancellable. |
| `POST` | `/api/v1/power/hibernate` | Yes | Hibernate laptop via `SetSuspendState` | Non-cancellable. |
| `POST` | `/api/v1/power/lock` | Yes | Lock current interactive session | Routes to companion via named pipe. Returns 503 if offline. |
| `GET` | `/api/v1/power/pending` | Yes | Current pending power operation | Reads latest accepted operation from SQLite. |
| `POST` | `/api/v1/power/cancel` | Yes | Abort pending shutdown or restart | Calls `AbortSystemShutdownW`. Updates SQLite state to cancelled. |

---

## 4. Local IPC Protocol (Service ↔ Companion)

- **Transport:** Windows Named Pipes (local-only, never network exposed).
- **Format:** Newline-delimited JSON (`\n` terminated UTF-8).
- **Service Pipe:** `\\.\pipe\ControlHubService`
- **Companion Pipe:** `\\.\pipe\ControlHubCompanion`

### Message Schema

1. **Command (`cmd`):**
   ```json
   {"type": "cmd", "cmd": "lock", "req_id": "uuid"}
   ```
2. **Response (`resp`):**
   ```json
   {"type": "resp", "req_id": "uuid", "ok": true, "error": null, "data": null}
   ```
3. **Status (`status`):**
   ```json
   {"type": "status", "companion_online": true, "session_user": "username", "ts": "2026-11-15T22:00:00Z"}
   ```

---

## 5. Database Schema (SQLite: `backend/data/controlhub.db`)

- **`power_operations`**: `id` (PK, UUID), `action`, `requested_by`, `request_id`, `requested_at`, `scheduled_for`, `delay_seconds`, `state` (`accepted` | `cancelled` | `completed`), `result_message`, `reason`, `cancellable` (0/1).
- **`audit_events`**: `id` (PK, UUID), `request_id`, `actor_device_id`, `action`, `target`, `result` (`accepted` | `failed`), `redacted_details_json`, `created_at`.

---

## 6. Security and Architectural Invariants

1. **Loopback Only:** API binds strictly to `127.0.0.1`. Inbound requests from non-loopback IPs are rejected with HTTP 403 `LOOPBACK_REQUIRED`.
2. **Session 0 Separation:** The machine service runs in Session 0 and NEVER interacts with the desktop or calls GUI/user32 APIs. All user-session actions (like `LockWorkStation()`) are delegated to the companion in the interactive user session.
3. **No Arbitrary Execution:** No shell, PowerShell, cmd, or arbitrary process execution endpoints exist.
4. **Credential Redaction:** Tokens are compared in constant time (`hmac.compare_digest`), never stored in ordinary tables, never logged, and stripped from audit JSON.
5. **Durable State Machine:** Power operation requests survive process restarts via SQLite persistence.
6. **Explicit Acceptance vs. Completion:** Responses return `"accepted"`, stating explicitly: `"The operating system accepted the request; this does not confirm the state transition."`

---

## 7. Build and iOS Toolchain Note

- **Windows-based iOS Toolchain:** This project targets iOS controllers without requiring a Mac or Xcode on the host. Packaging and signing are performed on Windows using [xtool](https://github.com/xtool-org/xtool).
