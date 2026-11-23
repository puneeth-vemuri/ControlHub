"""CRUD helpers for the ``audit_events`` table.

Audit records must NEVER contain token values, raw credentials, or any
secret material.  The *details* dict is sanitised before storage — only
safe fields (action type, reason, delay) are permitted.
"""

from __future__ import annotations

import json
import sqlite3
import uuid
from datetime import datetime, timezone
from typing import Any


_SAFE_DETAIL_KEYS = frozenset({"action", "reason", "delay_seconds", "platform"})


def _sanitise(details: dict[str, Any] | None) -> str | None:
    """Strip any potentially sensitive keys and serialise to JSON."""
    if not details:
        return None
    safe = {k: v for k, v in details.items() if k in _SAFE_DETAIL_KEYS}
    return json.dumps(safe)


def insert_audit(
    db: sqlite3.Connection,
    request_id: str,
    action: str,
    result: str,
    actor: str = "api-token",
    target: str | None = None,
    details: dict[str, Any] | None = None,
) -> None:
    """Append a row to audit_events.

    Token values are intentionally excluded — only sanitised detail keys are
    persisted (see ``_SAFE_DETAIL_KEYS``).
    """
    db.execute(
        """
        INSERT INTO audit_events
            (id, request_id, actor_device_id, action, target, result,
             redacted_details_json, created_at)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?)
        """,
        (
            str(uuid.uuid4()),
            request_id,
            actor,
            action,
            target,
            result,
            _sanitise(details),
            datetime.now(timezone.utc).isoformat(),
        ),
    )
    db.commit()
