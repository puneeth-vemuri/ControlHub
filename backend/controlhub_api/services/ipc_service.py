"""Named-pipe IPC client for the ControlHub Windows Service.

The Windows Machine Service hosts \\\\.\\pipe\\ControlHubService.
All machine-level power operations (sleep, hibernate) and desktop operations (lock)
are sent over this pipe so that:
1. The API never calls powrprof.dll or SetSuspendState directly.
2. The service enforces caller credentials and ACLs.
3. Lock is forwarded by the machine service to the user-session companion.
"""

from __future__ import annotations

import json
import os
import platform
import threading

PIPE_PATH = r"\\.\pipe\ControlHubService"
_lock = threading.Lock()


def service_pipe_online() -> bool:
    """Return True if the ControlHubService named pipe is accessible."""
    if platform.system() != "Windows":
        return False
    return os.path.exists(PIPE_PATH)


def companion_online() -> bool:
    """Check if the user-session companion is currently online via the service."""
    if platform.system() != "Windows":
        return False
    resp = send_ipc_command("get_status", "status-probe")
    if resp and resp.get("ok"):
        data = resp.get("data")
        if isinstance(data, dict):
            return bool(data.get("companion_online", False))
    return False


def _send_windows_pipe(cmd: str, request_id: str, **kwargs) -> dict | None:
    """Connect to Windows named pipe using Win32 API, send JSON command, and read response."""
    import ctypes
    from ctypes import wintypes

    # GENERIC_READ | GENERIC_WRITE = 0xC0000000
    # OPEN_EXISTING = 3
    handle = ctypes.windll.kernel32.CreateFileW(
        PIPE_PATH,
        0xC0000000,
        0,
        None,
        3,
        0,
        None,
    )
    if handle == -1 or handle == 0xFFFFFFFFFFFFFFFF:
        return None

    try:
        payload = {"type": "cmd", "cmd": cmd, "req_id": request_id}
        payload.update(kwargs)
        msg = (json.dumps(payload) + "\n").encode("utf-8")
        written = wintypes.DWORD()
        if not ctypes.windll.kernel32.WriteFile(
            handle, msg, len(msg), ctypes.byref(written), None
        ):
            return None

        response_bytes = b""
        buf = ctypes.create_string_buffer(4096)
        read = wintypes.DWORD()
        while not response_bytes.endswith(b"\n"):
            success = ctypes.windll.kernel32.ReadFile(
                handle, buf, 4096, ctypes.byref(read), None
            )
            if not success or read.value == 0:
                break
            response_bytes += buf.raw[: read.value]

        decoded = response_bytes.decode("utf-8-sig").strip()
        if not decoded:
            return None
        return json.loads(decoded)
    except Exception:
        return None
    finally:
        ctypes.windll.kernel32.CloseHandle(handle)


def send_ipc_command(cmd: str, request_id: str, **kwargs) -> dict | None:
    """Send a command over the ControlHubService named pipe and return the response."""
    if platform.system() != "Windows":
        return None

    with _lock:
        return _send_windows_pipe(cmd, request_id, **kwargs)


def send_lock_command(request_id: str) -> bool:
    """Send lock command to the machine service (which routes to the companion)."""
    resp = send_ipc_command("lock", request_id)
    return bool(resp and resp.get("ok", False))


def send_sleep_command(request_id: str) -> bool:
    """Send sleep command to the machine service."""
    resp = send_ipc_command("sleep", request_id)
    return bool(resp and resp.get("ok", False))


def send_hibernate_command(request_id: str) -> bool:
    """Send hibernate command to the machine service."""
    resp = send_ipc_command("hibernate", request_id)
    return bool(resp and resp.get("ok", False))


def send_shutdown_command(request_id: str, delay_seconds: int, reason: str) -> bool:
    """Send shutdown command to the machine service."""
    resp = send_ipc_command("shutdown", request_id, delay_seconds=delay_seconds, reason=reason)
    return bool(resp and resp.get("ok", False))


def send_restart_command(request_id: str, delay_seconds: int, reason: str) -> bool:
    """Send restart command to the machine service."""
    resp = send_ipc_command("restart", request_id, delay_seconds=delay_seconds, reason=reason)
    return bool(resp and resp.get("ok", False))


def send_cancel_command(request_id: str) -> bool:
    """Send cancel_shutdown command to the machine service."""
    resp = send_ipc_command("cancel_shutdown", request_id)
    return bool(resp and resp.get("ok", False))

