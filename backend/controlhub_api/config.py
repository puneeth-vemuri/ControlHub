"""Application settings — read from environment variables at import time.

Attributes may be patched directly in tests via monkeypatch:
    monkeypatch.setattr(config.settings, "api_token", "...")
"""

from __future__ import annotations

import os
from pathlib import Path


class Settings:
    api_token: str = os.environ.get("CONTROLHUB_API_TOKEN", "")
    db_path: str = os.environ.get(
        "CONTROLHUB_DB_PATH",
        str(Path(__file__).parent.parent / "data" / "controlhub.db"),
    )
    ipc_pipe_name: str = r"\\.\pipe\ControlHubService"
    min_token_length: int = 32


settings = Settings()
