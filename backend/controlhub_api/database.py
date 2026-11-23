"""SQLite database initialisation and connection helpers.

WAL mode is enabled for better concurrent read performance.
Foreign-key enforcement and a busy timeout guard against locking conflicts.
"""

from __future__ import annotations

import sqlite3
from contextlib import contextmanager
from pathlib import Path

from .config import settings


def _migration_sql() -> str:
    sql_path = Path(__file__).parent.parent / "migrations" / "001_initial.sql"
    return sql_path.read_text(encoding="utf-8")


def init_db() -> None:
    """Create data directory and initialise the SQLite schema.

    Safe to call multiple times — all DDL uses IF NOT EXISTS.
    """
    db_path = Path(settings.db_path)
    db_path.parent.mkdir(parents=True, exist_ok=True)
    conn = sqlite3.connect(str(db_path))
    try:
        conn.execute("PRAGMA journal_mode=WAL")
        conn.execute("PRAGMA foreign_keys=ON")
        conn.execute("PRAGMA busy_timeout=5000")
        conn.executescript(_migration_sql())
        conn.commit()
    finally:
        conn.close()


@contextmanager
def get_db():
    """Yield an open sqlite3.Connection, configured for safe concurrent use.

    The caller is responsible for committing; the connection is always closed
    on exit — even when an exception is raised.
    """
    db_path = Path(settings.db_path)
    conn = sqlite3.connect(str(db_path), timeout=5)
    conn.row_factory = sqlite3.Row
    try:
        conn.execute("PRAGMA journal_mode=WAL")
        conn.execute("PRAGMA foreign_keys=ON")
        conn.execute("PRAGMA busy_timeout=5000")
        yield conn
    finally:
        conn.close()
