# ADR 0007: Move Sleep and Hibernate Execution into Windows Machine Service

**Status:** Accepted

## Context
In the initial prototype, `powrprof.dll!SetSuspendState` was called directly from the Python API process via ctypes. This violated the architecture specification (Section 3 and Section 7), which mandates that machine-level lifecycle transitions belong strictly to the privileged Windows Machine Service, not the Web API layer. Calling kernel-level suspension functions directly from the API process prevents uniform authorization, bypasses service-level state tracking, and couples the web runtime to Windows DLL entry points.

## Decision
1. Move `SetSuspendState` execution into `ControlHub.Service` behind the `\\.\pipe\ControlHubService` named pipe.
2. The API process sends `{"type": "cmd", "cmd": "sleep", "req_id": "..."}` or `{"type": "cmd", "cmd": "hibernate", "req_id": "..."}` over the named pipe.
3. The machine service enforces that `SetSuspendState` is called with `bForce = false` so applications have the opportunity to save state or prompt.
4. Named pipe servers are protected with `PipeSecurity` ACLs permitting only the service identity, administrators, and authorized local callers.

## Consequences
- The FastAPI Python codebase contains zero imports or calls to `powrprof.dll` or `SetSuspendState`.
- If the machine service is offline, the API returns a structured `503 SERVICE_UNAVAILABLE` rather than attempting local suspension.
- Machine-level and desktop-level power actions are cleanly separated across the machine service and user companion.
