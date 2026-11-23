# Initial threat model

ControlHub controls the owner's laptop over a private network. Tailnet membership is not sufficient authorization. The API binds to loopback and protected endpoints require an application bearer credential. No endpoint accepts shell text or executable paths.

Power actions are constrained to named operations. Shutdown and restart require explicit confirmation and default to a 30-second delay; cancellation is supported while Windows permits it. Responses indicate that Windows accepted a request, not that a transition completed. Audit records omit credentials and request content beyond a short user reason.

The current token environment variable is a development bootstrap only. It is not per-device pairing, rotation, or production secret storage. The current API process also does not implement the future service/companion IPC boundary; interactive session actions remain unavailable in production until that boundary exists.
