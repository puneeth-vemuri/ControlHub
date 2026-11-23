# ControlHub agent guide

## Project context

ControlHub is a private, owner-operated iPhone controller for one Windows laptop. The approved scope and safety constraints are in `ControlHub_AI_Coding_Agent_Specification.md`; implementation is milestone-based. Current work is only the iOS dashboard shell and M0/M1 foundation. The intended architecture is Flutter app → private tailnet → loopback FastAPI → Windows machine service, with a separate interactive-session companion over ACL-protected named pipes.

## Graphify workflow

Use Graphify to retrieve focused repository context before broad codebase exploration:

1. For architecture, impact, or unfamiliar-area questions, run `/graphify query "<specific question>"` and follow relevant graph paths. Prefer a narrow query over opening the full report or scanning the whole repository.
2. Treat graph results as a map, then verify implementation details in the source files they cite. The graph can lag behind edits and inferred edges are not proof.
3. If `graphify-out/graph.json` does not exist, use `/graphify .` to build the whole-project graph, including the approved specification and project docs. Preserve the local-only code parsing path; document extraction may use the active assistant model.
4. After meaningful code or architecture-doc changes, update the graph with `/graphify . --update`. Keep `graph.json` and `GRAPH_REPORT.md` current with project changes; the HTML visualization is generated output.
5. For a small, clearly local edit, read the touched source directly instead of spending tokens on a graph query.

If Graphify is unavailable, continue with targeted `rg` searches and source reads. Do not claim graph context was used unless the CLI/MCP query actually ran.

## Implementation constraints

- Follow the approved specification; start with M0/M1 and stop at milestone boundaries.
- Bind the API to loopback. Never add arbitrary shell/PowerShell execution, covert capture, or public ingress.
- Keep machine service and user-session companion responsibilities separate. Do not claim unavailable Windows or iOS behavior is implemented.
- Do not invent sensor values or treat an accepted power request as a confirmed state change.
