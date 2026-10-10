"""Create original v1 theme packages, including one self-contained GIF theme."""

from __future__ import annotations

import json
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile, ZipInfo

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "samples" / "packages"
MEDIA = ROOT / "samples" / "media"
ZIP_TIMESTAMP = (2026, 10, 9, 23, 7, 40)


def write_entry(archive: ZipFile, name: str, content: bytes | str) -> None:
    info = ZipInfo(name, date_time=ZIP_TIMESTAMP)
    info.compress_type = ZIP_DEFLATED
    archive.writestr(info, content, compress_type=ZIP_DEFLATED)


def make_theme(
    theme_id: str,
    name: str,
    background: tuple[str, str],
    panel: str,
    opacity: int,
    foreground: str,
    *,
    media_kind: str = "Gradient",
    asset_path: str = "",
) -> dict:
    views = {}
    for _view in ("chat", "work", "codex"):
        views[_view] = {
            "background": {
                "kind": media_kind,
                "assetPath": asset_path,
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
    ("aurora-motion", "Aurora Motion", ("#07111F", "#142A3C"), "#10202D", 68, "#F0FAFF"),
]


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    for theme_id, name, background, panel, opacity, foreground in THEMES:
        asset_path = "assets/aurora-drift.gif" if theme_id == "aurora-motion" else ""
        media_kind = "Gif" if asset_path else "Gradient"
        theme = make_theme(theme_id, name, background, panel, opacity, foreground, media_kind=media_kind, asset_path=asset_path)
        manifest = {
            "schemaVersion": 1,
            "id": theme_id,
            "name": name,
            "version": "1.0.0",
            "minimumStudioVersion": "0.1.0",
            "supportedHostVersions": [],
            "assets": {asset_path: "gif"} if asset_path else {},
        }
        package = OUT / f"{theme_id}-1.0.0.tstheme"
        with ZipFile(package, "w", compression=ZIP_DEFLATED) as archive:
            write_entry(archive, "manifest.json", json.dumps(manifest, indent=2))
            write_entry(archive, "theme.json", json.dumps(theme, indent=2))
            if asset_path:
                media_file = MEDIA / "aurora-drift.gif"
                if not media_file.is_file():
                    raise FileNotFoundError(f"Required sample media is missing: {media_file}")
                write_entry(archive, asset_path, media_file.read_bytes())


if __name__ == "__main__":
    main()
