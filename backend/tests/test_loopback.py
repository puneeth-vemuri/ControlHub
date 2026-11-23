"""Loopback-only binding tests."""

import pytest
from httpx import ASGITransport, AsyncClient
from controlhub_api.auth.dependencies import is_loopback
from controlhub_api.main import app

pytestmark = pytest.mark.anyio


def test_is_loopback_helper():
    assert is_loopback("127.0.0.1") is True
    assert is_loopback("::1") is True
    assert is_loopback("192.168.1.50") is False
    assert is_loopback("10.0.0.1") is False
    assert is_loopback("0.0.0.0") is False
    assert is_loopback(None) is False


async def test_loopback_client_allowed(tmp_db):
    transport = ASGITransport(app=app, client=("127.0.0.1", 12345))
    async with AsyncClient(transport=transport, base_url="http://127.0.0.1") as c:
        r = await c.get("/api/v1/health")
        assert r.status_code == 200


async def test_non_loopback_client_rejected(tmp_db):
    transport = ASGITransport(app=app, client=("192.168.1.100", 12345))
    async with AsyncClient(transport=transport, base_url="http://192.168.1.100") as c:
        r = await c.get("/api/v1/health")
        assert r.status_code == 403
        body = r.json()
        assert body["status"] == "error"
        assert body["error"]["code"] == "LOOPBACK_REQUIRED"
