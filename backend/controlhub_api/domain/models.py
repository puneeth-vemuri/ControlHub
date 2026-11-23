"""Pydantic domain models for ControlHub API requests and responses."""

from __future__ import annotations

from datetime import datetime, timezone
from typing import Any

from pydantic import BaseModel, Field


class PowerRequest(BaseModel):
    confirm: bool = False
    delay_seconds: int = Field(default=30, ge=0, le=3600)
    reason: str = Field(default="User initiated", max_length=160)


class ErrorDetail(BaseModel):
    code: str
    message: str


class ApiResponse(BaseModel):
    request_id: str
    status: str  # "accepted" | "error"
    data: dict[str, Any] | None = None
    message: str = ""
    timestamp: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))
    error: ErrorDetail | None = None


class PowerResponse(ApiResponse):
    """Specialised ApiResponse for power action results."""
    pass
