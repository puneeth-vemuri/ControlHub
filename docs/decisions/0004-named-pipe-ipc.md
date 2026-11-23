# ADR 0004: ACL-protected named pipes for service-companion IPC

**Status:** Accepted

## Context
The Windows machine service runs in Session 0 as a background service and cannot interact with the desktop or run user-session APIs (such as `user32!LockWorkStation`). The user-session companion runs inside the interactive desktop session. A low-overhead, local-only IPC channel is required to communicate between them.

## Decision
Use Windows Named Pipes with newline-delimited JSON messages (`\n` terminated UTF-8):
- `\\.\pipe\ControlHubService`: Hosted by the service; the local FastAPI agent connects as a client to submit commands.
- `\\.\pipe\ControlHubCompanion`: Hosted by the service; the user-session companion connects as a client to register availability, send periodic heartbeats, and receive forwarded desktop actions.

## Consequences
- Windows Named Pipes are strictly local by default and cannot be addressed over the network unless explicitly published via SMB.
- No network ports are opened on loopback for IPC.
- The companion transparently reconnects if the machine service restarts.
- For M10 hardening, explicit Security Descriptors (DACLs) will be applied to permit access only to the service account and interactive user token.
