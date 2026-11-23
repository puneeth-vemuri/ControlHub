"""Business logic and OS integration for power operations."""

from __future__ import annotations

import platform
import uuid
from datetime import datetime, timedelta, timezone
from typing import Any, Literal

from ..database import get_db
from ..domain.models import PowerRequest
from ..repositories import audit_repository, power_repository
from . import ipc_service


class PowerServiceError(Exception):
    """Base exception for power service errors."""
    def __init__(self, code: str, message: str, status_code: int = 400):
        super().__init__(message)
        self.code = code
        self.message = message
        self.status_code = status_code


class ConfirmationRequired(PowerServiceError):
    def __init__(self):
        super().__init__("CONFIRMATION_REQUIRED", "Explicit confirmation is required.", 409)


class UnsupportedPlatform(PowerServiceError):
    def __init__(self):
        super().__init__("UNSUPPORTED_PLATFORM", "Power operations are available only on the Windows agent.", 501)


class CompanionUnavailable(PowerServiceError):
    def __init__(self):
        super().__init__("COMPANION_UNAVAILABLE", "Lock requires the user-session companion, which is not currently available.", 503)


class ServiceUnavailable(PowerServiceError):
    def __init__(self):
        super().__init__("SERVICE_UNAVAILABLE", "The operation requires the ControlHub Windows Service, which is not currently reachable.", 503)


class OsRequestFailed(PowerServiceError):
    def __init__(self):
        super().__init__("OS_REQUEST_FAILED", "Windows did not accept the requested operation.", 500)


class NoCancellableOperation(PowerServiceError):
    def __init__(self):
        super().__init__("NO_CANCELLABLE_OPERATION", "There is no cancellable power operation.", 409)


class CancelFailed(PowerServiceError):
    def __init__(self):
        super().__init__("CANCEL_FAILED", "Windows did not cancel the pending operation.", 500)


def _is_windows() -> bool:
    """Return True if running on Windows."""
    return platform.system() == "Windows"


def _execute_os_power(action: str, delay: int, request_id: str, reason: str = "") -> None:
    """Execute power action.

    Per ADR 0007 and spec Section 3.2:
    - ALL privileged machine-level operations (shutdown, restart, cancel, sleep, hibernate)
      are sent over named pipe IPC to ControlHubService (never powrprof or advapi32 in API).
    - lock is routed through ControlHubService to the user-session companion.
    """
    if not _is_windows():
        raise UnsupportedPlatform()

    if action == "lock":
        ok = ipc_service.send_lock_command(request_id)
        if not ok:
            raise CompanionUnavailable()
        return

    if action == "sleep":
        ok = ipc_service.send_sleep_command(request_id)
        if not ok:
            raise ServiceUnavailable()
        return

    if action == "hibernate":
        ok = ipc_service.send_hibernate_command(request_id)
        if not ok:
            raise ServiceUnavailable()
        return

    if action == "shutdown":
        ok = ipc_service.send_shutdown_command(request_id, delay, reason)
        if not ok:
            raise OsRequestFailed()
        return

    if action == "restart":
        ok = ipc_service.send_restart_command(request_id, delay, reason)
        if not ok:
            raise OsRequestFailed()
        return

    raise PowerServiceError("INVALID_ACTION", f"Unsupported power action: {action}", 400)


def submit_power_action(
    action: Literal["shutdown", "restart", "sleep", "hibernate", "lock"],
    body: PowerRequest,
    request_id: str,
) -> dict[str, Any]:
    if action in ("shutdown", "restart") and not body.confirm:
        raise ConfirmationRequired()

    delay = body.delay_seconds if action in ("shutdown", "restart") else 0
    cancellable = action in ("shutdown", "restart") and delay > 0
    # Zero-delay operations (such as lock) or immediate executions complete immediately;
    # only delayed operations (shutdown/restart with delay > 0) enter the 'accepted' pending state.
    initial_state = "accepted" if cancellable else "completed"

    with get_db() as db:
        try:
            _execute_os_power(action, delay, request_id, body.reason)
        except Exception as e:
            audit_repository.insert_audit(
                db,
                request_id=request_id,
                action=f"power.{action}",
                result="failed",
                details={"action": action, "reason": body.reason, "delay_seconds": delay},
            )
            raise e

        op_id = str(uuid.uuid4())
        scheduled_for = (datetime.now(timezone.utc) + timedelta(seconds=delay)).isoformat()
        op = {
            "id": op_id,
            "action": action,
            "requested_by": "api",
            "request_id": request_id,
            "requested_at": datetime.now(timezone.utc).isoformat(),
            "scheduled_for": scheduled_for,
            "delay_seconds": delay,
            "state": initial_state,
            "reason": body.reason,
            "cancellable": 1 if cancellable else 0,
        }
        power_repository.insert_operation(db, op)
        audit_repository.insert_audit(
            db,
            request_id=request_id,
            action=f"power.{action}",
            result="accepted" if cancellable else "completed",
            details={"action": action, "reason": body.reason, "delay_seconds": delay},
        )
        return op


def get_pending_power_operation() -> dict[str, Any] | None:
    with get_db() as db:
        op = power_repository.get_latest_pending(db)
        if op:
            op["cancellable"] = bool(op["cancellable"])
        return op


def cancel_pending_power_operation(request_id: str) -> dict[str, Any]:
    if not _is_windows():
        raise UnsupportedPlatform()

    with get_db() as db:
        pending = power_repository.get_latest_pending(db)
        if not pending or not pending.get("cancellable"):
            raise NoCancellableOperation()

        ok = ipc_service.send_cancel_command(request_id)
        if not ok:
            audit_repository.insert_audit(
                db,
                request_id=request_id,
                action="power.cancel",
                result="failed",
            )
            raise CancelFailed()

        power_repository.update_operation_state(
            db,
            pending["id"],
            state="cancelled",
            result_message="Operation cancelled by user request",
        )
        audit_repository.insert_audit(
            db,
            request_id=request_id,
            action="power.cancel",
            result="accepted",
        )
        pending["state"] = "cancelled"
        pending["cancellable"] = False
        return pending
