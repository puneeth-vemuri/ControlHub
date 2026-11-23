"""Health router for liveness checks."""

from __future__ import annotations

from datetime import datetime, timezone
from fastapi import APIRouter

router = APIRouter(prefix="/api/v1", tags=["health"])


@router.get("/health")
def health():
    """Unauthenticated minimal liveness probe."""
    return {"status": "ok", "timestamp": datetime.now(timezone.utc).isoformat()}
