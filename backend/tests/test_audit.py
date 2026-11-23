"""Audit logging tests."""

import json
import sqlite3
from unittest.mock import MagicMock, patch
import pytest

pytestmark = pytest.mark.anyio


async def test_power_action_creates_audit_entry(client, auth_headers, tmp_db):
    with patch("controlhub_api.services.ipc_service.send_shutdown_command", return_value=True):
        with patch("controlhub_api.services.power_service._is_windows", return_value=True):
            r = await client.post(
                "/api/v1/power/shutdown",
                json={"confirm": True, "delay_seconds": 30, "reason": "test audit"},
                headers=auth_headers,
            )
            assert r.status_code == 200

    conn = sqlite3.connect(tmp_db)
    rows = conn.execute("SELECT * FROM audit_events WHERE action = 'power.shutdown'").fetchall()
    conn.close()
    assert len(rows) == 1

    row_dict = dict(
        zip(
            [
                "id",
                "request_id",
                "actor_device_id",
                "action",
                "target",
                "result",
                "redacted_details_json",
                "created_at",
            ],
            rows[0],
        )
    )
    assert row_dict["result"] == "accepted"
    if row_dict["redacted_details_json"]:
        details = json.loads(row_dict["redacted_details_json"])
        assert "token" not in str(details).lower()
        assert "authorization" not in str(details).lower()


async def test_failed_action_creates_audit_entry(client, auth_headers, tmp_db):
    with patch("controlhub_api.services.power_service._is_windows", return_value=False):
        await client.post(
            "/api/v1/power/shutdown",
            json={"confirm": True},
            headers=auth_headers,
        )

    conn = sqlite3.connect(tmp_db)
    rows = conn.execute("SELECT * FROM audit_events WHERE result = 'failed'").fetchall()
    conn.close()
    assert len(rows) >= 1
