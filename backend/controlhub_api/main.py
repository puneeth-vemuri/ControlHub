"""Loopback-only API foundation for the ControlHub Windows agent."""

from __future__ import annotations

import uuid
from contextlib import asynccontextmanager
from datetime import datetime, timezone

from fastapi import FastAPI, HTTPException, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse

from .api import device, health, power
from .auth.dependencies import is_loopback
from .database import init_db


@asynccontextmanager
async def lifespan(app: FastAPI):
    init_db()
    yield


app = FastAPI(
    title="ControlHub API",
    version="0.1.0",
    docs_url=None,
    redoc_url=None,
    lifespan=lifespan,
)


@app.middleware("http")
async def loopback_guard(request: Request, call_next):
    client_host = request.client.host if request.client else None
    if client_host and not is_loopback(client_host):
        return JSONResponse(
            status_code=403,
            content={
                "request_id": getattr(request.state, "request_id", str(uuid.uuid4())),
                "status": "error",
                "error": {
                    "code": "LOOPBACK_REQUIRED",
                    "message": "This API is only accessible from the local machine.",
                },
                "timestamp": datetime.now(timezone.utc).isoformat(),
            },
        )
    return await call_next(request)


@app.middleware("http")
async def request_id_middleware(request: Request, call_next):
    request.state.request_id = request.headers.get("X-Request-ID") or str(uuid.uuid4())
    response = await call_next(request)
    response.headers["X-Request-ID"] = request.state.request_id
    return response


@app.exception_handler(HTTPException)
async def custom_http_exception_handler(request: Request, exc: HTTPException):
    headers = exc.headers or {}
    if "X-Request-ID" not in headers:
        headers["X-Request-ID"] = getattr(request.state, "request_id", str(uuid.uuid4()))

    if isinstance(exc.detail, dict):
        return JSONResponse(status_code=exc.status_code, content=exc.detail, headers=headers)
    return JSONResponse(
        status_code=exc.status_code,
        content={
            "request_id": getattr(request.state, "request_id", str(uuid.uuid4())),
            "status": "error",
            "error": {"code": "HTTP_ERROR", "message": str(exc.detail)},
            "timestamp": datetime.now(timezone.utc).isoformat(),
        },
        headers=headers,
    )


@app.exception_handler(RequestValidationError)
async def validation_exception_handler(request: Request, exc: RequestValidationError):
    return JSONResponse(
        status_code=422,
        content={
            "request_id": getattr(request.state, "request_id", str(uuid.uuid4())),
            "status": "error",
            "error": {"code": "VALIDATION_ERROR", "message": str(exc)},
            "timestamp": datetime.now(timezone.utc).isoformat(),
        },
        headers={"X-Request-ID": getattr(request.state, "request_id", str(uuid.uuid4()))},
    )


app.include_router(health.router)
app.include_router(device.router)
app.include_router(power.router)
