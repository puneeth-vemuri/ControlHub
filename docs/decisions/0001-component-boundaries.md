# ADR 0001: Component boundaries

**Status:** Accepted

Use a Flutter iOS app, a loopback-bound FastAPI host, a Windows machine service, and a separate user-session companion. Keep Windows session interactions out of Session 0. The initial milestone establishes the mobile/API boundaries and deliberately reports companion-dependent capabilities as unavailable until the companion and ACL-protected IPC are implemented.
