# Implementation plan

## M0 — Foundation

- Establish the mobile, API, Windows, infrastructure, and docs boundaries.
- Document the security model and local setup.
- Keep the API loopback-only and require an explicit bearer credential for protected routes.

## M1 — Core device and power

- Provide device status and capabilities.
- Implement confirmation-gated shutdown/restart, sleep, hibernate, lock, pending operation and cancellation.
- Record redacted power audit entries. Distinguish OS acceptance from confirmed device state.
- The service/companion split remains the target architecture; desktop-session actions are unavailable until a user-session companion exists.

## Initial app work

- Build the iOS-first dashboard shell with status, health summaries, quick actions, and five-tab navigation.
- Keep demonstration metrics visibly marked until API-backed measurements exist.
- Pairing and remote connectivity are later M2 work.

## Deferred

Tailscale ingress, QR pairing, mobile API integration, screen sharing, files, monitoring history, processes, automations, notifications, and utilities remain out of scope until the relevant milestone.
