"""Create three small, original, media-free v1 theme packages."""

from __future__ import annotations

import json
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "samples" / "packages"


def make_theme(theme_id: str, name: str, background: tuple[str, str], panel: str, opacity: int, foreground: str) -> dict:
    views = {}
    for _view in ("chat", "work", "codex"):
        views[_view] = {
            "background": {
                "kind": "Gradient",
                "assetPath": "",
                "color": background[0],
                "gradientEndColor": background[1],
                "fit": "Cover",
                "positionX": 50,
                "positionY": 50,
                "dimPercent": 12,
                "blur": 0,
                "pausedByUser": False,
            },
            "surfaces": {
                key: {
                    "backgroundColor": panel,
                    "foregroundColor": foreground,
                    "opacity": opacity if key in ("content", "composer") else min(100, opacity + 12),
                    "blur": 12 if opacity < 90 else 0,
                    "fontFamily": "Segoe UI Variable Text",
                    "cornerRadius": 14,
                    "controlState": "default",
                }
                for key in ("sidebar", "titlebar", "content", "composer", "menu", "cards", "controls")
            },
        }
    return {
        "schemaVersion": 1,
        "id": theme_id,
        "name": name,
        "version": "1.0.0",
        "minimumStudioVersion": "0.1.0",
        "updatedAt": "2026-10-09T00:00:00+00:00",
        "views": views,
    }


THEMES = [
    ("aurora-glass", "Aurora Glass", ("#111A2D", "#343B58"), "#182136", 64, "#F3F5FA"),
    ("soft-paper", "Soft Paper", ("#E8DFCE", "#FAF5EA"), "#FFFDF8", 94, "#25231F"),
    ("carbon-mint", "Carbon Mint", ("#080B10", "#14201C"), "#111A17", 88, "#E6F5EE"),
]


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    for theme_id, name, background, panel, opacity, foreground in THEMES:
        theme = make_theme(theme_id, name, background, panel, opacity, foreground)
        manifest = {
            "schemaVersion": 1,
            "id": theme_id,
            "name": name,
            "version": "1.0.0",
            "minimumStudioVersion": "0.1.0",
            "supportedHostVersions": [],
            "assets": {},
        }
        package = OUT / f"{theme_id}-1.0.0.tstheme"
        with ZipFile(package, "w", compression=ZIP_DEFLATED) as archive:
            archive.writestr("manifest.json", json.dumps(manifest, indent=2))
            archive.writestr("theme.json", json.dumps(theme, indent=2))


if __name__ == "__main__":
    main()
