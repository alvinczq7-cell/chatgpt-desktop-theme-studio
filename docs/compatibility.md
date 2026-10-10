# Compatibility status

| Host | Version | State | Notes |
| --- | --- | --- | --- |
| New ChatGPT desktop app (Windows x64) | Not validated | Unsupported | Read-only inspection on 2026-10-10 found `ChatGPT.exe` file version `155.0.8059.40` running in 8 processes. None had a `--remote-debugging-port` argument or a listening TCP port. The active app was not restarted or relaunched with debugging enabled. |

The adapter remains fail-closed until all three views (Chat, Work, Codex) and restore behavior pass an end-to-end check on a specific build. The current `UnverifiedHostAdapter` never inspects or modifies the client DOM.

The CDP probe only checks a loopback `/json/version` endpoint when the user explicitly selects **Check local connection**. An endpoint response does not prove the process belongs to ChatGPT and does not enable theme application.
