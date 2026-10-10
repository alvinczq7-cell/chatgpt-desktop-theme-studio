# Contributing

Thanks for helping improve Theme Studio.

## Before changing the live adapter

The host adapter is fail-closed. Do not add a ChatGPT version mapping until it has been checked on a clean Windows 11 x64 installation and all Chat, Work, and Codex views pass apply, ordinary interaction, restart, and restore checks. Never modify the official app package or bind CDP to a non-loopback address.

## Theme format changes

Keep v1 imports backward compatible. Add tests for malformed packages, asset references, and path traversal. Theme bundles may contain local media but must not contain scripts, HTML, CSS, commands, or remote URLs.

## Pull requests

Include the affected Windows/client build, steps to reproduce, test output, and screenshots for UI changes. Do not commit copyrighted theme media without redistribution permission.
