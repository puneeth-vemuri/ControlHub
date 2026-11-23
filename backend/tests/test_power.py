"""Power management endpoint tests."""

from unittest.mock import MagicMock, patch
import pytest

pytestmark = pytest.mark.anyio


@pytest.fixture
def mock_windows_power():
    with patch("controlhub_api.services.power_service._is_windows", return_value=True):
        with patch("controlhub_api.services.ipc_service.send_shutdown_command", return_value=True):
            with patch("controlhub_api.services.ipc_service.send_restart_command", return_value=True):
                with patch("controlhub_api.services.ipc_service.send_cancel_command", return_value=True):
                    with patch("controlhub_api.services.ipc_service.send_sleep_command", return_value=True):
                        with patch("controlhub_api.services.ipc_service.send_hibernate_command", return_value=True):
                            yield


async def test_shutdown_requires_confirm(client, auth_headers, mock_windows_power):
    r = await client.post(
        "/api/v1/power/shutdown",
        json={"confirm": False, "delay_seconds": 30},
        headers=auth_headers,
    )
    assert r.status_code == 409
    assert r.json()["error"]["code"] == "CONFIRMATION_REQUIRED"


async def test_shutdown_accepted_with_confirm(client, auth_headers, mock_windows_power):
    r = await client.post(
        "/api/v1/power/shutdown",
        json={"confirm": True, "delay_seconds": 30, "reason": "test"},
        headers=auth_headers,
    )
    assert r.status_code == 200
    body = r.json()
    assert body["status"] == "accepted"
    assert body["data"]["action"] == "shutdown"
    assert body["data"]["cancellable"] is True
    assert "operation_id" in body["data"]


async def test_restart_accepted(client, auth_headers, mock_windows_power):
    r = await client.post(
        "/api/v1/power/restart",
        json={"confirm": True, "delay_seconds": 30},
        headers=auth_headers,
    )
    assert r.status_code == 200
    assert r.json()["data"]["cancellable"] is True


async def test_sleep_no_confirm_needed(client, auth_headers, mock_windows_power):
    r = await client.post(
        "/api/v1/power/sleep",
        json={"confirm": False},
        headers=auth_headers,
    )
    assert r.status_code == 200
    body = r.json()
    assert body["data"]["cancellable"] is False


async def test_sleep_service_unavailable(client, auth_headers):
    with patch("controlhub_api.services.ipc_service.send_sleep_command", return_value=False):
        with patch("controlhub_api.services.power_service._is_windows", return_value=True):
            r = await client.post(
                "/api/v1/power/sleep",
                json={"confirm": False},
                headers=auth_headers,
            )
            assert r.status_code == 503
            assert r.json()["error"]["code"] == "SERVICE_UNAVAILABLE"


async def test_hibernate_no_confirm_needed(client, auth_headers, mock_windows_power):
    r = await client.post(
        "/api/v1/power/hibernate",
        json={"confirm": False},
        headers=auth_headers,
    )
    assert r.status_code == 200
    body = r.json()
    assert body["data"]["cancellable"] is False


async def test_hibernate_service_unavailable(client, auth_headers):
    with patch("controlhub_api.services.ipc_service.send_hibernate_command", return_value=False):
        with patch("controlhub_api.services.power_service._is_windows", return_value=True):
            r = await client.post(
                "/api/v1/power/hibernate",
                json={"confirm": False},
                headers=auth_headers,
            )
            assert r.status_code == 503
            assert r.json()["error"]["code"] == "SERVICE_UNAVAILABLE"


async def test_lock_companion_unavailable(client, auth_headers):
    with patch("controlhub_api.services.ipc_service.send_lock_command", return_value=False):
        with patch("controlhub_api.services.power_service._is_windows", return_value=True):
            r = await client.post(
                "/api/v1/power/lock",
                json={"confirm": False},
                headers=auth_headers,
            )
            assert r.status_code == 503
            assert r.json()["error"]["code"] == "COMPANION_UNAVAILABLE"


async def test_lock_companion_success_unit_dispatch(client, auth_headers):
    """Unit test of API-side lock dispatch via ipc_service stub. Real E2E is verified in manual tests."""
    with patch("controlhub_api.services.ipc_service.send_lock_command", return_value=True):
        with patch("controlhub_api.services.power_service._is_windows", return_value=True):
            r = await client.post(
                "/api/v1/power/lock",
                json={"confirm": False},
                headers=auth_headers,
            )
            assert r.status_code == 200
            assert r.json()["data"]["action"] == "lock"


async def test_pending_shows_active_operation(client, auth_headers, mock_windows_power):
    await client.post(
        "/api/v1/power/shutdown",
        json={"confirm": True, "delay_seconds": 30},
        headers=auth_headers,
    )
    r = await client.get("/api/v1/power/pending", headers=auth_headers)
    assert r.status_code == 200
    assert r.json()["data"] is not None
    assert r.json()["data"]["action"] == "shutdown"


async def test_cancel_shutdown(client, auth_headers, mock_windows_power):
    await client.post(
        "/api/v1/power/shutdown",
        json={"confirm": True, "delay_seconds": 30},
        headers=auth_headers,
    )
    r = await client.post("/api/v1/power/cancel", headers=auth_headers)
    assert r.status_code == 200
    assert r.json()["data"]["state"] == "cancelled"


async def test_cancel_no_pending_operation(client, auth_headers):
    with patch("controlhub_api.services.power_service._is_windows", return_value=True):
        r = await client.post("/api/v1/power/cancel", headers=auth_headers)
        assert r.status_code == 409
        assert r.json()["error"]["code"] == "NO_CANCELLABLE_OPERATION"


async def test_delay_bounds(client, auth_headers, mock_windows_power):
    r = await client.post(
        "/api/v1/power/shutdown",
        json={"confirm": True, "delay_seconds": 9999},
        headers=auth_headers,
    )
    assert r.status_code == 422


async def test_non_windows_returns_501(client, auth_headers):
    with patch("controlhub_api.services.power_service._is_windows", return_value=False):
        r = await client.post(
            "/api/v1/power/shutdown",
            json={"confirm": True},
            headers=auth_headers,
        )
        assert r.status_code == 501
        assert r.json()["error"]["code"] == "UNSUPPORTED_PLATFORM"


async def test_completed_lock_is_not_returned_as_pending(client, auth_headers):
    """Zero-delay operations like lock complete immediately and must not appear in /power/pending."""
    with patch("controlhub_api.services.ipc_service.send_lock_command", return_value=True):
        with patch("controlhub_api.services.power_service._is_windows", return_value=True):
            r = await client.post("/api/v1/power/lock", json={"confirm": False}, headers=auth_headers)
            assert r.status_code == 200

            pending_res = await client.get("/api/v1/power/pending", headers=auth_headers)
            assert pending_res.status_code == 200
            assert pending_res.json()["data"] is None


async def test_failed_executor_call_writes_no_pending_operation(client, auth_headers):
    """When the machine service returns failure, no pending operation row is stored."""
    with patch("controlhub_api.services.ipc_service.send_shutdown_command", return_value=False):
        with patch("controlhub_api.services.power_service._is_windows", return_value=True):
            r = await client.post(
                "/api/v1/power/shutdown",
                json={"confirm": True, "delay_seconds": 30},
                headers=auth_headers,
            )
            assert r.status_code == 500
            assert r.json()["error"]["code"] == "OS_REQUEST_FAILED"

            pending_res = await client.get("/api/v1/power/pending", headers=auth_headers)
            assert pending_res.status_code == 200
            assert pending_res.json()["data"] is None


async def test_failed_lock_writes_no_pending_operation_and_logs_audit(client, auth_headers, tmp_db):
    """When lock fails (companion offline/error), it returns 503 and writes no pending operation."""
    import sqlite3

    with patch("controlhub_api.services.ipc_service.send_lock_command", return_value=False):
        with patch("controlhub_api.services.power_service._is_windows", return_value=True):
            r = await client.post("/api/v1/power/lock", json={"confirm": False}, headers=auth_headers)
            assert r.status_code == 503
            assert r.json()["error"]["code"] == "COMPANION_UNAVAILABLE"

            pending_res = await client.get("/api/v1/power/pending", headers=auth_headers)
            assert pending_res.status_code == 200
            assert pending_res.json()["data"] is None

            conn = sqlite3.connect(tmp_db)
            rows = conn.execute("SELECT result FROM audit_events WHERE action = 'power.lock'").fetchall()
            conn.close()
            assert len(rows) == 1
            assert rows[0][0] == "failed"


def test_api_codebase_has_no_win32_power_or_privilege_calls():
    """Static assertion that backend/controlhub_api contains zero executable Win32 power calls."""
    from pathlib import Path
    import re

    banned_patterns = [
        r"\.InitiateSystemShutdown",
        r"\.AbortSystemShutdown",
        r"\.SetSuspendState",
        r"\.AdjustTokenPrivileges",
        r"\.ExitWindowsEx",
    ]
    pkg_dir = Path(__file__).parent.parent / "controlhub_api"
    for py_file in pkg_dir.rglob("*.py"):
        lines = py_file.read_text(encoding="utf-8").splitlines()
        for idx, line in enumerate(lines, 1):
            stripped = line.strip()
            if stripped.startswith("#") or stripped.startswith('"""') or stripped.startswith("*"):
                continue
            for pattern in banned_patterns:
                matches = re.findall(pattern, line)
                assert not matches, f"Found banned Win32 power call '{pattern}' at {py_file}:{idx}: {line}"


async def test_service_failure_strips_win32_codes_from_client_response(client, auth_headers):
    """When machine service fails with an error and data, API client response contains only OS_REQUEST_FAILED and generic message."""
    with patch(
        "controlhub_api.services.ipc_service.send_ipc_command",
        return_value={"ok": False, "error": "OS_REQUEST_FAILED", "data": {"win32_error": 1314}},
    ):
        with patch("controlhub_api.services.power_service._is_windows", return_value=True):
            r = await client.post(
                "/api/v1/power/shutdown",
                json={"confirm": True, "delay_seconds": 30},
                headers=auth_headers,
            )
            assert r.status_code == 500
            body = r.json()
            assert body["status"] == "error"
            assert body["error"]["code"] == "OS_REQUEST_FAILED"
            assert body["error"]["message"] == "Windows did not accept the requested operation."
            # Assert no numeric error codes or raw service data leak to client
            assert "1314" not in r.text
            assert "win32" not in r.text.lower()


