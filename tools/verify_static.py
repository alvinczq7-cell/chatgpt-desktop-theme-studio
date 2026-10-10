"""Offline structure checks that do not require the .NET or Windows SDK."""

from __future__ import annotations

import json
import xml.etree.ElementTree as ET
from pathlib import Path
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
XAML_NS = "{http://schemas.microsoft.com/winfx/2006/xaml}"
GENERATED_DIRS = {".git", ".nuget", ".dotnet-home", "bin", "obj", ".vs"}


def iter_source_files(pattern: str):
    for path in ROOT.rglob(pattern):
        relative_parts = path.relative_to(ROOT).parts
        if path.is_file() and not GENERATED_DIRS.intersection(relative_parts):
            yield path


def main() -> int:
    errors: list[str] = []
    for path in iter_source_files("*.json"):
        try:
            json.loads(path.read_text(encoding="utf-8"))
        except Exception as exc:
            errors.append(f"Invalid JSON {path.relative_to(ROOT)}: {exc}")

    xml_paths = list(iter_source_files("*.xaml")) + list(iter_source_files("*.resw")) + list(iter_source_files("*.csproj")) + list(iter_source_files("*.manifest"))
    for path in xml_paths:
        try:
            ET.parse(path)
        except Exception as exc:
            errors.append(f"Invalid XML {path.relative_to(ROOT)}: {exc}")

    solution = ROOT / "ThemeStudio.sln"
    if solution.exists():
        text = solution.read_text(encoding="utf-8-sig")
        project_count = sum(line.startswith("Project(") for line in text.splitlines())
        end_project_count = sum(line == "EndProject" for line in text.splitlines())
        if project_count != end_project_count:
            errors.append(f"Solution project blocks are unbalanced: {project_count} projects, {end_project_count} EndProject markers")
        global_sections = sum(line.strip().startswith("GlobalSection(") for line in text.splitlines())
        end_global_sections = sum(line.strip() == "EndGlobalSection" for line in text.splitlines())
        if global_sections != end_global_sections:
            errors.append(f"Solution global sections are unbalanced: {global_sections} sections, {end_global_sections} EndGlobalSection markers")

    xaml = ROOT / "src/ThemeStudio/MainWindow.xaml"
    uid_names: set[str] = set()
    if xaml.exists():
        tree = ET.parse(xaml)
        uid_names = {node.attrib[XAML_NS + "Uid"] for node in tree.iter() if XAML_NS + "Uid" in node.attrib}
    for resw in (ROOT / "src/ThemeStudio/Strings").glob("*/Resources.resw"):
        try:
            resource_names = [node.attrib["name"] for node in ET.parse(resw).getroot().findall("data")]
            if len(resource_names) != len(set(resource_names)):
                errors.append(f"Duplicate resource key in {resw.relative_to(ROOT)}")
            for uid in uid_names:
                if not any(key.startswith(uid + ".") for key in resource_names):
                    errors.append(f"Missing resource for x:Uid '{uid}' in {resw.relative_to(ROOT)}")
        except Exception as exc:
            errors.append(f"Invalid resources {resw.relative_to(ROOT)}: {exc}")

    packages = sorted((ROOT / "samples/packages").glob("*.tstheme"))
    if len(packages) != 3:
        errors.append(f"Expected 3 sample packages, found {len(packages)}")
    for package in packages:
        try:
            with ZipFile(package) as archive:
                names = set(archive.namelist())
                manifest = json.loads(archive.read("manifest.json"))
                theme = json.loads(archive.read("theme.json"))
                if manifest["schemaVersion"] != 1 or theme["schemaVersion"] != 1:
                    errors.append(f"Unexpected schema version in {package.name}")
                if manifest["id"] != theme["id"] or manifest["name"] != theme["name"]:
                    errors.append(f"Manifest identity mismatch in {package.name}")
                if set(theme["views"]) != {"chat", "work", "codex"}:
                    errors.append(f"Sample does not define all views: {package.name}")
                for asset in manifest.get("assets", {}):
                    if not asset.startswith("assets/") or ".." in Path(asset).parts or asset not in names:
                        errors.append(f"Invalid sample asset path in {package.name}: {asset}")
        except Exception as exc:
            errors.append(f"Invalid sample package {package.name}: {exc}")

    if errors:
        print("Static verification failed:")
        print("\n".join(f"- {error}" for error in errors))
        return 1
    print(f"Static verification passed: {len(xml_paths)} XML files, {len(packages)} sample theme packages, and all JSON resources.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
