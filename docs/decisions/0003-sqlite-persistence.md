# ADR 0003: SQLite for power operation and audit persistence

**Status:** Accepted

## Context
The initial prototype used an in-memory dictionary for pending power operations and flat text appending for audit records. This had several critical shortcomings:
1. In-memory pending operations were lost on process restarts, violating the restart-recovery acceptance requirement.
2. Flat text files are non-transactional, difficult to query, and do not provide structured query capabilities for future reporting or inspection.

## Decision
Use embedded SQLite with WAL (Write-Ahead Logging) mode, foreign key enforcement, and busy timeout handlers for all persistent state in the agent. Initial tables:
- `power_operations`: tracks power actions, scheduled time, cancellable flags, and transition states (`accepted`, `cancelled`, `completed`).
- `audit_events`: immutable structured audit trail capturing request ID, actor, action, result, sanitized details, and ISO-8601 timestamps.

## Consequences
- Pending power state survives agent process restarts.
- Audit records are structured, indexed by timestamp, and queryable.
- Sensitive values (tokens, credentials) are stripped before serialization into `redacted_details_json`.
- Database file is stored at `backend/data/controlhub.db` and excluded from git version control.
