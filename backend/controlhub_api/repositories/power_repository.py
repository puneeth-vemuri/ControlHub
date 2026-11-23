"""CRUD helpers for the ``power_operations`` table."""

from __future__ import annotations

import sqlite3
from typing import Any


def insert_operation(db: sqlite3.Connection, op: dict[str, Any]) -> None:
    """Insert a new power_operations row."""
    db.execute(
        """
        INSERT INTO power_operations
            (id, action, requested_by, request_id, requested_at,
             scheduled_for, delay_seconds, state, reason, cancellable)
        VALUES
            (:id, :action, :requested_by, :request_id, :requested_at,
             :scheduled_for, :delay_seconds, :state, :reason, :cancellable)
        """,
        op,
    )
    db.commit()


def get_latest_pending(db: sqlite3.Connection) -> dict[str, Any] | None:
    """Return the most recent operation whose state is 'accepted', or None."""
    row = db.execute(
        "SELECT * FROM power_operations WHERE state = 'accepted' ORDER BY requested_at DESC LIMIT 1"
    ).fetchone()
    return dict(row) if row else None


def update_operation_state(
    db: sqlite3.Connection,
    op_id: str,
    state: str,
    result_message: str | None = None,
) -> None:
    """Update the state (and optional result message) of a power operation."""
    db.execute(
        "UPDATE power_operations SET state = ?, result_message = ? WHERE id = ?",
        (state, result_message, op_id),
    )
    db.commit()


def get_operation_by_id(db: sqlite3.Connection, op_id: str) -> dict[str, Any] | None:
    """Return a single power_operations row by primary key, or None."""
    row = db.execute(
        "SELECT * FROM power_operations WHERE id = ?", (op_id,)
    ).fetchone()
    return dict(row) if row else None
