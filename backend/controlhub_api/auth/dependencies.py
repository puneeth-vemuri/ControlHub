"""Authentication and authorisation dependencies.

``require_token`` — FastAPI Depends callable that validates Bearer tokens.
``is_loopback``   — Pure helper used by the middleware and directly by tests.
"""

from __future__ import annotations

import hmac
import uuid
from datetime import datetime, timezone

from fastapi import Header, Request
from fastapi.responses import JSONResponse

from ..config import settings


def is_loopback(host: str | None) -> bool:
    """Return True when *host* is a loopback address (IPv4 or IPv6)."""
    return host in ("127.0.0.1", "::1")


def _error_body(request_id: str, code: str, message: str) -> dict:
    return {
        "request_id": request_id,
        "status": "error",
        "error": {"code": code, "message": message},
        "timestamp": datetime.now(timezone.utc).isoformat(),
    }


def require_token(
    request: Request,
    authorization: str | None = Header(default=None),
) -> None:
    """Validate the Bearer token using constant-time comparison.

    Raises structured JSON 503 when the server is misconfigured (token too
    short) and 401 when the supplied credential does not match.
    Tokens are NEVER logged or included in error responses.
    """
    request_id: str = getattr(request.state, "request_id", str(uuid.uuid4()))
    configured: str = settings.api_token

    if len(configured) < settings.min_token_length:
        from fastapi import HTTPException
        raise HTTPException(
            status_code=503,
            detail=_error_body(
                request_id,
                "AUTH_NOT_CONFIGURED",
                "Set a 32-character CONTROLHUB_API_TOKEN before using protected routes.",
            ),
        )

    supplied = authorization.removeprefix("Bearer ") if authorization else ""
    if not hmac.compare_digest(supplied.encode(), configured.encode()):
        from fastapi import HTTPException
        raise HTTPException(
            status_code=401,
            detail=_error_body(
                request_id,
                "UNAUTHORIZED",
                "Valid bearer credentials are required.",
            ),
            headers={"WWW-Authenticate": "Bearer"},
        )
