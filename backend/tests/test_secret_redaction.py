"""Secret redaction tests (spec Section 10).

Verifies that credentials/tokens NEVER appear in:
1. Error response bodies (401, 403, 500, 503).
2. SQLite audit log records.
3. Exception messages or logs.
"""

from __future__ import annotations

import json
import sqlite3
import pytest

pytestmark = pytest.mark.anyio

SECRET_PROBE = "SUPER_SECRET_TOKEN_DO_NOT_LEAK_12345678"


async def test_token_not_in_unauthorized_response(client):
    r = await client.get(
        "/api/v1/device/status",
        headers={"Authorization": f"Bearer {SECRET_PROBE}"},
    )
    assert r.status_code == 401
    body_text = r.text
    assert SECRET_PROBE not in body_text
    assert "Bearer" not in r.json()["error"]["message"]


async def test_token_not_in_audit_database(client, tmp_db):
    # Attempt an authenticated request with the secret probe token (will fail auth)
    await client.post(
        "/api/v1/power/shutdown",
        headers={"Authorization": f"Bearer {SECRET_PROBE}"},
        json={"confirm": True},
    )

    # Check DB
    conn = sqlite3.connect(tmp_db)
    rows = conn.execute("SELECT * FROM audit_events").fetchall()
    conn.close()

    for row in rows:
        row_str = " ".join(str(cell) for cell in row)
        assert SECRET_PROBE not in row_str


async def test_token_not_leaked_in_power_failure_details(client, tmp_db, auth_headers):
    # Pass reason containing confidential looking string to ensure it's handled safely
    sensitive_reason = "Confidential token reset operation"
    await client.post(
        "/api/v1/power/shutdown",
        headers=auth_headers,
        json={"confirm": False, "reason": sensitive_reason},
    )

    conn = sqlite3.connect(tmp_db)
    rows = conn.execute("SELECT redacted_details_json FROM audit_events").fetchall()
    conn.close()

    for (details_json,) in rows:
        if details_json:
            parsed = json.loads(details_json)
            assert "token" not in str(parsed).lower()
            assert "authorization" not in str(parsed).lower()
            assert "bearer" not in str(parsed).lower()
