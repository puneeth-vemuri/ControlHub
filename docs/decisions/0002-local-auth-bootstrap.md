# ADR 0002: Local development authentication

**Status:** Accepted for M0/M1 development only

Protected API routes require a bearer value supplied through `CONTROLHUB_API_TOKEN` with at least 32 characters. Compare secrets in constant time, never log them, and expose no interactive docs by default. This is a local development bootstrap, not the final per-device pairing and credential rotation design.
