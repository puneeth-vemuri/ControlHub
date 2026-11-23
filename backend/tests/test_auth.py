"""Authentication and authorization tests."""

import pytest

pytestmark = pytest.mark.anyio


async def test_health_no_auth_allowed(client):
    r = await client.get("/api/v1/health")
    assert r.status_code == 200
    assert r.json()["status"] == "ok"


async def test_protected_no_token_rejected(client):
    r = await client.get("/api/v1/device/status")
    assert r.status_code == 401
    body = r.json()
    assert body["status"] == "error"
    assert body["error"]["code"] == "UNAUTHORIZED"


async def test_protected_wrong_token_rejected(client):
    r = await client.get(
        "/api/v1/device/status",
        headers={"Authorization": "Bearer invalidtokenthatislongenough12345678"},
    )
    assert r.status_code == 401
    assert r.json()["error"]["code"] == "UNAUTHORIZED"


async def test_protected_valid_token_accepted(client, auth_headers):
    r = await client.get("/api/v1/device/status", headers=auth_headers)
    assert r.status_code == 200
    body = r.json()
    assert "host" in body
    assert body["agent"] == "online"


async def test_short_token_raises_503(client, monkeypatch):
    from controlhub_api import config
    monkeypatch.setattr(config.settings, "api_token", "tooshort")
    r = await client.get("/api/v1/device/status", headers={"Authorization": "Bearer tooshort"})
    assert r.status_code == 503
    assert r.json()["error"]["code"] == "AUTH_NOT_CONFIGURED"
