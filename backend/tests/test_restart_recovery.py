"""Restart recovery and mid-operation reconciliation tests."""

from unittest.mock import MagicMock, patch
from httpx import ASGITransport, AsyncClient
import pytest
import sqlite3

pytestmark = pytest.mark.anyio


async def test_pending_survives_restart(tmp_db, auth_headers):
    """Simulate a process restart by creating two separate client instances against the same DB."""
    with patch("controlhub_api.services.ipc_service.send_shutdown_command", return_value=True):
        with patch("controlhub_api.services.power_service._is_windows", return_value=True):
            from controlhub_api.main import app

            # Instance 1: submit shutdown
            transport1 = ASGITransport(app=app)
            async with AsyncClient(transport=transport1, base_url="http://127.0.0.1") as c1:
                r1 = await c1.post(
                    "/api/v1/power/shutdown",
                    json={"confirm": True, "delay_seconds": 30},
                    headers=auth_headers,
                )
                assert r1.status_code == 200
                op_id = r1.json()["data"]["operation_id"]

            # Instance 2 (simulating fresh process start querying persisted SQLite state):
            transport2 = ASGITransport(app=app)
            async with AsyncClient(transport=transport2, base_url="http://127.0.0.1") as c2:
                r2 = await c2.get("/api/v1/power/pending", headers=auth_headers)
                assert r2.status_code == 200
                pending = r2.json()["data"]
                assert pending is not None
                assert pending["id"] == op_id
                assert pending["action"] == "shutdown"
                assert pending["state"] == "accepted"


async def test_cancelled_operation_not_returned_as_pending_after_restart(tmp_db, auth_headers):
    """Verify that an operation cancelled before restart is not returned as pending."""
    with patch("controlhub_api.services.ipc_service.send_shutdown_command", return_value=True):
        with patch("controlhub_api.services.ipc_service.send_cancel_command", return_value=True):
            with patch("controlhub_api.services.power_service._is_windows", return_value=True):
                from controlhub_api.main import app

            transport1 = ASGITransport(app=app)
            async with AsyncClient(transport=transport1, base_url="http://127.0.0.1") as c1:
                await c1.post(
                    "/api/v1/power/shutdown",
                    json={"confirm": True, "delay_seconds": 30},
                    headers=auth_headers,
                )
                cancel_resp = await c1.post("/api/v1/power/cancel", headers=auth_headers)
                assert cancel_resp.status_code == 200

            # Fresh instance after restart
            transport2 = ASGITransport(app=app)
            async with AsyncClient(transport=transport2, base_url="http://127.0.0.1") as c2:
                r2 = await c2.get("/api/v1/power/pending", headers=auth_headers)
                assert r2.status_code == 200
                assert r2.json()["data"] is None
