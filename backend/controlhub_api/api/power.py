"""Power management API routes."""

from __future__ import annotations

from datetime import datetime, timezone
from typing import Literal

from fastapi import APIRouter, Depends, HTTPException, Request

from ..auth.dependencies import require_token
from ..domain.models import PowerRequest, PowerResponse
from ..services import power_service
from ..services.power_service import PowerServiceError

router = APIRouter(prefix="/api/v1/power", tags=["power"])


def _handle_power_service_error(e: PowerServiceError, request_id: str):
    raise HTTPException(
        status_code=e.status_code,
        detail={
            "request_id": request_id,
            "status": "error",
            "error": {"code": e.code, "message": e.message},
            "timestamp": datetime.now(timezone.utc).isoformat(),
        },
    )


def _submit_power(
    action: Literal["shutdown", "restart", "sleep", "hibernate", "lock"],
    body: PowerRequest,
    request: Request,
) -> PowerResponse:
    request_id = getattr(request.state, "request_id", "")
    try:
        op = power_service.submit_power_action(action, body, request_id)
    except PowerServiceError as e:
        _handle_power_service_error(e, request_id)

    return PowerResponse(
        request_id=request_id,
        status="accepted",
        data={
            "operation_id": op["id"],
            "action": op["action"],
            "scheduled_for": op["scheduled_for"],
            "cancellable": bool(op["cancellable"]),
        },
        message="The operating system accepted the request; this does not confirm the state transition.",
        timestamp=datetime.now(timezone.utc),
    )


@router.post("/shutdown", response_model=PowerResponse, dependencies=[Depends(require_token)])
def shutdown(body: PowerRequest, request: Request):
    return _submit_power("shutdown", body, request)


@router.post("/restart", response_model=PowerResponse, dependencies=[Depends(require_token)])
def restart(body: PowerRequest, request: Request):
    return _submit_power("restart", body, request)


@router.post("/sleep", response_model=PowerResponse, dependencies=[Depends(require_token)])
def sleep(body: PowerRequest, request: Request):
    return _submit_power("sleep", body, request)


@router.post("/hibernate", response_model=PowerResponse, dependencies=[Depends(require_token)])
def hibernate(body: PowerRequest, request: Request):
    return _submit_power("hibernate", body, request)


@router.post("/lock", response_model=PowerResponse, dependencies=[Depends(require_token)])
def lock(body: PowerRequest, request: Request):
    return _submit_power("lock", body, request)


@router.get("/pending", dependencies=[Depends(require_token)])
def pending_power():
    op = power_service.get_pending_power_operation()
    return {"data": op}


@router.post("/cancel", dependencies=[Depends(require_token)])
def cancel_power(request: Request):
    request_id = getattr(request.state, "request_id", "")
    try:
        pending = power_service.cancel_pending_power_operation(request_id)
    except PowerServiceError as e:
        _handle_power_service_error(e, request_id)

    return {
        "request_id": request_id,
        "status": "accepted",
        "data": pending,
        "message": "Cancellation was accepted by Windows.",
        "timestamp": datetime.now(timezone.utc).isoformat(),
    }
