# Compatibility status

| Host | Version | State | Notes |
| --- | --- | --- | --- |
| New ChatGPT desktop app (Windows x64) | Not validated | Unsupported | The local development machine had version 26.1002.7124.0 installed on 2026-10-09. No CDP listener was visible in the ordinary running session. No restart or special debug launch was attempted because the app was active. |

The adapter remains fail-closed until all three views (Chat, Work, Codex) and restore behavior pass an end-to-end check on a specific build. The current `UnverifiedHostAdapter` never inspects or modifies the client DOM.

The CDP probe only checks a loopback `/json/version` endpoint when the user explicitly selects **Check local connection**. An endpoint response does not prove the process belongs to ChatGPT and does not enable theme application.
