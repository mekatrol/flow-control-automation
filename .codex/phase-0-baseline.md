# Phase 0 compatibility baseline

Recorded on 25 July 2026 in the repository development environment.

| Gate | Result |
| --- | --- |
| Go formatting | Passed |
| `go test ./...` | Passed |
| Frontend formatting | Passed |
| Frontend lint | Passed |
| Frontend unit tests | 97 passed |
| Frontend type-check and production build | Passed |
| Existing Playwright suite | 140 passed |

The Playwright baseline covers desktop Chromium, Firefox, Edge, and mobile
Chromium. No point data file, point route, controller route, or user-visible
behaviour was introduced in Phase 0.
