from __future__ import annotations

import os
import subprocess
from dataclasses import dataclass
from pathlib import Path

from .paths import default_save_investigator_project_path


class SaveInvestigatorRefreshError(RuntimeError):
    """Raised when Save Investigator exists but cannot produce a fresh run."""


@dataclass(frozen=True)
class SaveInvestigatorRefresh:
    project_path: Path
    output_root: Path | None
    output_directory: Path | None
    skipped: bool
    message: str


def refresh_save_investigator_output(
    *,
    project_path: Path | str | None = None,
    save_path: Path | str | None = None,
    dotnet_command: str | None = None,
) -> SaveInvestigatorRefresh:
    project = Path(project_path).expanduser() if project_path is not None else default_save_investigator_project_path()
    project = project.resolve()
    if not project.is_file():
        return SaveInvestigatorRefresh(
            project_path=project,
            output_root=None,
            output_directory=None,
            skipped=True,
            message=f"Save Investigator project not found: {project}",
        )

    command = [
        dotnet_command or os.environ.get("CHIEF_OF_STAFF_DOTNET_COMMAND") or "dotnet",
        "run",
        "--project",
        str(project),
    ]
    if save_path is not None:
        command.extend(["--", str(Path(save_path).expanduser().resolve())])

    completed = subprocess.run(
        command,
        cwd=str(project.parent),
        capture_output=True,
        text=True,
        check=False,
    )
    if completed.returncode != 0:
        detail = (completed.stderr or completed.stdout or "").strip()
        if not detail:
            detail = f"exit code {completed.returncode}"
        raise SaveInvestigatorRefreshError(f"Save Investigator refresh failed: {detail}")

    output_directory = _parse_output_directory(completed.stdout) or _latest_output_directory(project)
    output_root = _output_root(output_directory)
    return SaveInvestigatorRefresh(
        project_path=project,
        output_root=output_root,
        output_directory=output_directory,
        skipped=False,
        message=f"Save Investigator refreshed: {output_directory}" if output_directory else "Save Investigator refreshed.",
    )


def _parse_output_directory(stdout: str) -> Path | None:
    for line in stdout.splitlines():
        label, separator, value = line.partition(":")
        if separator and label.strip().lower() == "output":
            path = Path(value.strip()).expanduser()
            return path.resolve()
    return None


def _latest_output_directory(project: Path) -> Path | None:
    output_root = project.parent / "bin" / "Debug" / "net8.0" / "output"
    if not output_root.is_dir():
        return None
    children = [child for child in output_root.iterdir() if child.is_dir()]
    if not children:
        return None
    return max(children, key=lambda child: child.stat().st_mtime).resolve()


def _output_root(output_directory: Path | None) -> Path | None:
    if output_directory is None:
        return None
    if (output_directory / "transport-report-facts.json").is_file():
        return output_directory.parent.resolve()
    return output_directory.resolve()
