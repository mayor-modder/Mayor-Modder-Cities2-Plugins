from __future__ import annotations

import os
import sys
from pathlib import Path


def default_mods_data_dir() -> Path:
    env = os.environ.get("CHIEF_OF_STAFF_MODS_DATA_DIR")
    if env:
        return Path(env).expanduser()
    if os.name == "nt":
        base = os.environ.get("USERPROFILE")
        if base:
            return (
                Path(base)
                / "AppData"
                / "LocalLow"
                / "Colossal Order"
                / "Cities Skylines II"
                / "ModsData"
            )
    if sys.platform == "darwin":
        return Path.home() / "Library/Application Support/Colossal Order/Cities Skylines II/ModsData"
    return Path.home() / ".local/share/Colossal Order/Cities Skylines II/ModsData"


def default_save_investigator_output_dir() -> Path:
    env = os.environ.get("CHIEF_OF_STAFF_SAVE_INVESTIGATOR_OUTPUT_DIR")
    if env:
        return Path(env).expanduser()
    tool_root = Path.cwd() / "tools" / "SaveInvestigator"
    candidates = [
        tool_root / "bin" / "Release" / "net8.0" / "output",
        tool_root / "bin" / "Debug" / "net8.0" / "output",
        tool_root / "output",
    ]
    for candidate in candidates:
        if candidate.exists():
            return candidate
    return candidates[0]


def default_save_investigator_project_path() -> Path:
    env = os.environ.get("CHIEF_OF_STAFF_SAVE_INVESTIGATOR_PROJECT")
    if env:
        return Path(env).expanduser()
    return Path.cwd() / "tools" / "SaveInvestigator" / "SaveInvestigator.csproj"
