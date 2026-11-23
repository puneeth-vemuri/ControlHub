# ControlHub

ControlHub is a private, owner-operated remote management system for a personally owned Windows laptop, controlled from an iPhone via Tailscale.

## Current status (Milestone 0 & Milestone 1 Foundation)

- **Backend API (FastAPI):** Modular package architecture, loopback-only binding enforcement (`127.0.0.1:8765`), Bearer token authentication, Win32 power management (shutdown, restart, sleep, hibernate, cancel), SQLite persistent power state and audit logging.
- **Windows Components (.NET 8):** Windows Machine Service (`ControlHub.Service` for Session 0) and User-Session Companion (`ControlHub.Companion` for interactive session lock) communicating via named pipes.
- **iPhone Mobile App (Flutter):** Interactive dashboard wired to live API endpoints, secure Keychain credential persistence, quick power actions with confirmation dialogs, and pending operation cancellation. Designed for Windows compilation and packaging via [xtool](https://github.com/xtool-org/xtool) without requiring a Mac.
- **CI / Quality:** All unit and static analysis checks passing via local automated script (`ci.ps1`).

---

## Local Development & Setup

### Prerequisites
- **Windows 10 / 11**
- **Python 3.12+** (tested on 3.13)
- **Flutter SDK >= 3.24**
- **.NET 8 SDK** (for building Windows service & companion binaries)
- **xtool** (for iOS bundling and deployment on Windows)

### 1. Run All Automated Checks (CI)

```powershell
powershell -ExecutionPolicy Bypass -File .\ci.ps1
```

### 2. Start the Backend API

```powershell
# Set development auth token (must be >= 32 characters)
$env:CONTROLHUB_API_TOKEN = "a-secure-development-token-32-chars-long"

# Start the loopback FastAPI service
Set-Location backend
.\.venv\Scripts\python.exe -m uvicorn controlhub_api.main:app --host 127.0.0.1 --port 8765
```

The API will automatically initialize the SQLite database at `backend/data/controlhub.db`.

### 3. Run the Mobile App (Flutter)

```powershell
cd apps/mobile
flutter pub get
flutter test
flutter analyze
```

Configure the agent connection inside the app's **Settings** tab:
- **Base URL:** `http://127.0.0.1:8765`
- **API Token:** `<your-32-character-token>`

### 4. Windows Service & Companion Setup

Refer to [`windows/README.md`](windows/README.md) for details on installing the background machine service and registering the companion in Task Scheduler.

---

## Repository Structure

```text
controlhub/
├── apps/mobile/             # Flutter iOS controller app
│   ├── lib/core/            # Secure storage, Dio HTTP client, constants
│   ├── lib/domain/          # DeviceStatus, Capabilities, PendingOperation
│   ├── lib/features/        # Dashboard, Power dialogs, Settings screen
│   └── test/                # Unit tests
├── backend/
│   ├── controlhub_api/      # FastAPI package
│   │   ├── api/             # Health, Device, Power routers
│   │   ├── auth/            # Bearer token and loopback dependencies
│   │   ├── domain/          # Pydantic request/response models
│   │   ├── repositories/    # SQLite power_operations and audit_events CRUD
│   │   └── services/        # Power orchestration, Win32 ctypes, IPC client
│   ├── migrations/          # SQLite schema DDL (001_initial.sql)
│   └── tests/               # Pytest suite (auth, loopback, power, audit, recovery)
├── windows/
│   ├── ipc-contracts/       # Shared named-pipe message definitions (.NET 8)
│   ├── service/             # Windows machine service (Session 0)
│   └── companion/           # User-session companion (interactive lock)
├── docs/
│   ├── architecture.md      # Compact repository map and API contracts
│   ├── decisions/           # Architecture Decision Records (ADRs 0001 - 0007)
│   └── threat-model.md      # Security and threat analysis
├── ci.ps1                   # Local CI verification script
└── README.md
```

See [docs/architecture.md](docs/architecture.md) for detailed contracts and invariants.
