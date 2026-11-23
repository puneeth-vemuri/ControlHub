"""Device status and capabilities routes."""

from __future__ import annotations

import platform
from datetime import datetime, timezone
from fastapi import APIRouter, Depends

from ..auth.dependencies import require_token
from ..services import ipc_service

router = APIRouter(prefix="/api/v1/device", tags=["device"])
BOOT_TIME = datetime.now(timezone.utc)


@router.get("/status", dependencies=[Depends(require_token)])
def device_status():
    companion_status = "online" if ipc_service.companion_online() else "unavailable"
    now = datetime.now(timezone.utc)
    return {
        "host": platform.node(),
        "os": platform.platform(),
        "agent": "online",
        "companion": companion_status,
        "uptime_seconds": int((now - BOOT_TIME).total_seconds()),
        "last_seen_at": now.isoformat(),
    }


@router.get("/capabilities", dependencies=[Depends(require_token)])
def capabilities():
    is_win = platform.system() == "Windows"
    comp_online = ipc_service.companion_online()
    return {
        "power": {
            "shutdown": is_win,
            "restart": is_win,
            "cancel": is_win,
            "sleep": is_win,
            "hibernate": is_win,
            "lock": is_win and comp_online,
        },
        "companion": comp_online,
        "screen_sharing": False,
    }
