"""Pytest fixtures for ControlHub API tests."""

from __future__ import annotations

import os
import pytest
from httpx import ASGITransport, AsyncClient

TEST_TOKEN = "a" * 32
os.environ["CONTROLHUB_API_TOKEN"] = TEST_TOKEN

from controlhub_api import config
config.settings.api_token = TEST_TOKEN

from controlhub_api.database import init_db
from controlhub_api.main import app


@pytest.fixture
def anyio_backend():
    return "asyncio"


@pytest.fixture
def tmp_db(tmp_path, monkeypatch):
    db_file = str(tmp_path / "test.db")
    monkeypatch.setenv("CONTROLHUB_DB_PATH", db_file)
    monkeypatch.setattr(config.settings, "db_path", db_file)
    init_db()
    return db_file


@pytest.fixture
def auth_headers():
    return {"Authorization": f"Bearer {TEST_TOKEN}"}


@pytest.fixture
async def client(tmp_db):
    transport = ASGITransport(app=app)
    async with AsyncClient(transport=transport, base_url="http://127.0.0.1") as ac:
        yield ac
