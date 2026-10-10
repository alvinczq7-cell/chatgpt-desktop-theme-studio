# Compatibility status

| Host | Version | State | Notes |
| --- | --- | --- | --- |
| New ChatGPT desktop app (Windows x64) | Package `26.1007.2314.0`; Chromium `155.0.8059.40` | Package activation and CDP metadata verified; live theming unsupported | Existing sessions had no debugging listener. Two launches with fresh temporary user-data directories and `--remote-debugging-address=127.0.0.1` returned `Chrome/155.0.8059.40` from `/json/version` (ports 9335 and 19336). The second used the registered `OpenAI.Codex_2p2nqsd0c76g0!App` package activation ID. Both returned zero page targets from `/json/list`. The isolated processes and profiles were removed afterward. This verifies package activation and loopback metadata only; no page DOM, CSS, or authenticated client view was tested. |

The adapter remains fail-closed until all three views (Chat, Work, Codex) and restore behavior pass an end-to-end check on a specific build. The current `UnverifiedHostAdapter` never inspects or modifies the client DOM.

The CDP probe only checks a loopback `/json/version` endpoint when the user explicitly selects **Check local connection**. An endpoint response does not prove the process belongs to ChatGPT and does not enable theme application.
