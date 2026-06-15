from __future__ import annotations

import argparse
import shutil
import tempfile
from pathlib import Path
from typing import Callable, Iterable

from chief_of_staff import plugin_metadata
from chief_of_staff.plugin_metadata import PLATFORMS, PLATFORMS_BY_KEY, SKILL_NAMES, Platform


DEFAULT_CATALOG_ROOT = Path("../Mayor-Modder-Cities2-Plugins")

MANAGED_DIRS = ("skills", "vendor")
MANAGED_FILES = (Path("bin") / "cities2-chief-of-staff-launcher.js",)
IGNORED_DIRS = {"__pycache__", ".pytest_cache"}
IGNORED_SUFFIXES = {".pyc"}


def _metadata_files(platform: Platform) -> tuple[tuple[Path, Callable[[], str]], ...]:
    root = platform.dist_package_root
    return (
        (root / platform.manifest_dir / "plugin.json", platform.plugin_json),
        (root / ".mcp.json", platform.mcp_json),
        (root / "README.md", platform.readme_md),
    )


def _selected_platforms(package_roots: Iterable[Path] | None) -> tuple[Platform, ...]:
    if package_roots is None:
        return PLATFORMS
    by_root = {platform.dist_package_root: platform for platform in PLATFORMS}
    selected: list[Platform] = []
    for raw in package_roots:
        path = Path(raw)
        platform = by_root.get(path)
        if platform is None:
            raise ValueError(f"Unknown plugin package root: {path}")
        selected.append(platform)
    return tuple(selected)


LAUNCHER_JS = """#!/usr/bin/env node

const fs = require("node:fs");
const { spawn, spawnSync } = require("node:child_process");
const path = require("node:path");

const selfRoot = path.resolve(__dirname, "..");

function candidates() {
  const configured = process.env.CITIES2_CHIEF_OF_STAFF_PYTHON;
  const values = [];
  if (configured) values.push({ command: configured, args: [] });
  if (process.platform === "win32") values.push({ command: "py", args: ["-3"] });
  values.push({ command: "python3", args: [] });
  values.push({ command: "python", args: [] });
  return values;
}

function findPython() {
  for (const candidate of candidates()) {
    const result = spawnSync(candidate.command, [...candidate.args, "-c", "import sys; raise SystemExit(0 if sys.version_info >= (3, 11) else 1)"], {
      stdio: "ignore",
      windowsHide: true,
    });
    if (result.status === 0) return candidate;
  }
  return null;
}

function invocationForRoot(pluginRoot) {
  const baseEnv = { ...process.env, PYTHONDONTWRITEBYTECODE: "1" };
  const vendoredScript = path.join(pluginRoot, "vendor", "run_server.py");
  if (fs.existsSync(vendoredScript)) return { args: [vendoredScript], env: baseEnv };

  const vendoredServer = path.join(pluginRoot, "vendor", "chief_of_staff", "mcp_server.py");
  if (fs.existsSync(vendoredServer)) {
    const env = { ...baseEnv };
    env.PYTHONPATH = [path.join(pluginRoot, "vendor"), env.PYTHONPATH].filter(Boolean).join(path.delimiter);
    return { args: ["-m", "chief_of_staff.mcp_server"], env };
  }

  const sourceServer = path.join(pluginRoot, "chief_of_staff", "mcp_server.py");
  if (fs.existsSync(sourceServer)) {
    const env = { ...baseEnv };
    env.PYTHONPATH = [pluginRoot, env.PYTHONPATH].filter(Boolean).join(path.delimiter);
    return { args: ["-m", "chief_of_staff.mcp_server"], env };
  }

  return null;
}

function serverInvocation() {
  const roots = [selfRoot, process.env.PLUGIN_ROOT].filter(Boolean).map((value) => path.resolve(value));
  for (const root of roots) {
    const invocation = invocationForRoot(root);
    if (invocation) return invocation;
  }
  console.error(`Unable to locate Cities2 Chief of Staff server files. Checked: ${roots.join("; ")}.`);
  process.exit(1);
}

const python = findPython();
if (!python) {
  console.error("Cities2 Chief of Staff requires Python 3.11 or newer. Set CITIES2_CHIEF_OF_STAFF_PYTHON to a Python interpreter if it is not on PATH.");
  process.exit(127);
}

const invocation = serverInvocation();
const child = spawn(python.command, [...python.args, ...invocation.args, ...process.argv.slice(2)], {
  env: invocation.env,
  stdio: ["inherit", "inherit", "inherit"],
  windowsHide: true,
});

child.on("exit", (code, signal) => {
  if (signal) {
    process.kill(process.pid, signal);
    return;
  }
  process.exit(code ?? 1);
});

child.on("error", (error) => {
  console.error(`Unable to start Cities2 Chief of Staff: ${error.message}`);
  process.exit(1);
});
"""

RUN_SERVER_PY = """from __future__ import annotations

import sys
from pathlib import Path

VENDOR_ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(VENDOR_ROOT))

from chief_of_staff.mcp_server import main  # noqa: E402


if __name__ == "__main__":
    raise SystemExit(main())
"""


def sync_packages(
    repo_root: Path | str = Path.cwd(),
    *,
    package_roots: Iterable[Path] | None = None,
) -> tuple[Path, ...]:
    root = Path(repo_root).resolve()
    changed: list[Path] = []
    for platform in _selected_platforms(package_roots):
        changed.extend(_replace_payload(root, platform.dist_package_root))
        changed.extend(_sync_metadata(root, platform))
    return _unique_sorted(changed)


def check_packages(
    repo_root: Path | str = Path.cwd(),
    *,
    package_roots: Iterable[Path] | None = None,
) -> tuple[Path, ...]:
    root = Path(repo_root).resolve()
    stale: list[Path] = []
    with tempfile.TemporaryDirectory(prefix="chief-of-staff-plugin-payload-") as tmp:
        tmp_package = Path(tmp) / "payload"
        _write_payload(root, tmp_package)
        for platform in _selected_platforms(package_roots):
            package_abs = root / platform.dist_package_root
            for dirname in MANAGED_DIRS:
                stale.extend(_changed_tree_paths(tmp_package / dirname, package_abs / dirname))
            for filename in MANAGED_FILES:
                stale.extend(_changed_paths(tmp_package / filename, package_abs / filename))
            stale.extend(_check_metadata(root, platform))
    return _unique_sorted(stale)


def sync_catalog_package(
    catalog_root: Path | str = DEFAULT_CATALOG_ROOT,
    *,
    repo_root: Path | str = Path.cwd(),
    platforms: Iterable[Platform] = PLATFORMS,
) -> tuple[Path, ...]:
    root = Path(repo_root).resolve()
    catalog = Path(catalog_root).resolve()
    platforms = tuple(platforms)

    for platform in platforms:
        marketplace = catalog / platform.catalog_marketplace_rel
        if not marketplace.is_file():
            raise FileNotFoundError(f"Catalog marketplace not found: {marketplace}")

    sync_packages(root, package_roots=tuple(platform.dist_package_root for platform in platforms))

    changed: list[Path] = []
    for platform in platforms:
        source = root / platform.dist_package_root
        target = catalog / platform.catalog_package_root
        if not target.resolve().is_relative_to(catalog):
            raise ValueError(f"Catalog package target escapes catalog root: {target}")

        changed.extend(_changed_tree_paths(source, target))
        if target.exists():
            if target.is_dir():
                shutil.rmtree(target)
            else:
                target.unlink()
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copytree(source, target)
    return _unique_sorted(changed)


def _sync_metadata(repo_root: Path, platform: Platform) -> tuple[Path, ...]:
    changed: list[Path] = []
    for target_rel, builder in _metadata_files(platform):
        target = repo_root / target_rel
        expected = builder()
        if not target.is_file() or target.read_text(encoding="utf-8") != expected:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(expected, encoding="utf-8", newline="\n")
            changed.append(target)
    return _unique_sorted(changed)


def _check_metadata(repo_root: Path, platform: Platform) -> tuple[Path, ...]:
    stale: list[Path] = []
    for target_rel, builder in _metadata_files(platform):
        target = repo_root / target_rel
        if not target.is_file() or target.read_text(encoding="utf-8") != builder():
            stale.append(target)
    return tuple(stale)


def _write_payload(repo_root: Path, payload_root: Path) -> None:
    _copy_skills(repo_root, payload_root)
    launcher = payload_root / "bin" / "cities2-chief-of-staff-launcher.js"
    launcher.parent.mkdir(parents=True, exist_ok=True)
    launcher.write_text(LAUNCHER_JS, encoding="utf-8", newline="\n")

    vendor_root = payload_root / "vendor"
    vendor_root.mkdir(parents=True, exist_ok=True)
    (vendor_root / "run_server.py").write_text(RUN_SERVER_PY, encoding="utf-8", newline="\n")

    package_source = repo_root / "chief_of_staff"
    if not package_source.is_dir():
        raise FileNotFoundError(f"Canonical package source not found: {package_source}")
    shutil.copytree(
        package_source,
        vendor_root / "chief_of_staff",
        ignore=shutil.ignore_patterns("__pycache__", "*.pyc", ".pytest_cache"),
    )


def _copy_skills(repo_root: Path, payload_root: Path) -> None:
    for skill_name in SKILL_NAMES:
        source = repo_root / "skills" / skill_name
        if not source.is_dir():
            raise FileNotFoundError(f"Canonical skill source not found: {source}")
        target = payload_root / "skills" / skill_name
        shutil.copytree(
            source,
            target,
            ignore=shutil.ignore_patterns("__pycache__", "*.pyc", ".pytest_cache"),
        )


def _replace_payload(repo_root: Path, package_root: Path) -> tuple[Path, ...]:
    package_abs = repo_root / package_root
    package_abs.mkdir(parents=True, exist_ok=True)
    changed: list[Path] = []
    with tempfile.TemporaryDirectory(prefix="chief-of-staff-plugin-payload-") as tmp:
        tmp_package = Path(tmp) / "payload"
        _write_payload(repo_root, tmp_package)

        for dirname in MANAGED_DIRS:
            source = tmp_package / dirname
            target = package_abs / dirname
            changed.extend(_changed_tree_paths(source, target))
            if target.exists():
                if target.is_dir():
                    shutil.rmtree(target)
                else:
                    target.unlink()
            shutil.copytree(source, target)

        for filename in MANAGED_FILES:
            source = tmp_package / filename
            target = package_abs / filename
            changed.extend(_changed_paths(source, target))
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)

    return _unique_sorted(changed)


def _changed_paths(expected: Path, actual: Path) -> tuple[Path, ...]:
    if not actual.is_file():
        return (actual,)
    if expected.read_bytes() != actual.read_bytes():
        return (actual,)
    return ()


def _changed_tree_paths(expected: Path, actual: Path) -> tuple[Path, ...]:
    changed: list[Path] = []
    if not actual.is_dir():
        return tuple(actual / path.relative_to(expected) for path in _files_under(expected))

    expected_files = {path.relative_to(expected): path for path in _files_under(expected)}
    actual_files = {path.relative_to(actual): path for path in _files_under(actual, include_ignored=True)}

    for relative, expected_file in expected_files.items():
        actual_file = actual / relative
        if relative not in actual_files or expected_file.read_bytes() != actual_file.read_bytes():
            changed.append(actual_file)
    for relative in sorted(actual_files.keys() - expected_files.keys()):
        changed.append(actual / relative)
    return tuple(changed)


def _files_under(root: Path, *, include_ignored: bool = False) -> tuple[Path, ...]:
    if root.is_file():
        return (root,)
    if include_ignored:
        return tuple(sorted(path for path in root.rglob("*") if path.is_file()))
    return tuple(
        sorted(
            path
            for path in root.rglob("*")
            if path.is_file()
            and not any(part in IGNORED_DIRS for part in path.relative_to(root).parts)
            and path.suffix not in IGNORED_SUFFIXES
        )
    )


def _unique_sorted(paths: Iterable[Path]) -> tuple[Path, ...]:
    return tuple(sorted({Path(path).resolve() for path in paths}, key=lambda path: str(path)))


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Sync generated plugin packages.")
    parser.add_argument("command", choices=("sync", "check", "sync-catalog"))
    parser.add_argument("--repo-root", default=Path.cwd())
    parser.add_argument("--catalog-root", default=DEFAULT_CATALOG_ROOT)
    parser.add_argument("--target", choices=("codex", "claude", "all"), default="all")
    args = parser.parse_args(argv)

    repo_root = Path(args.repo_root)
    platforms = PLATFORMS if args.target == "all" else (PLATFORMS_BY_KEY[args.target],)
    selected_roots = tuple(platform.dist_package_root for platform in platforms)

    if args.command == "sync":
        changed = sync_packages(repo_root, package_roots=selected_roots)
        if changed:
            print("Updated generated plugin package artifacts:")
            for path in changed:
                print(f"- {path}")
        else:
            print("Plugin package payloads are in sync.")
        return 0

    if args.command == "sync-catalog":
        changed = sync_catalog_package(args.catalog_root, repo_root=repo_root, platforms=platforms)
        if changed:
            print("Updated catalog plugin package artifacts:")
            for path in changed:
                print(f"- {path}")
        else:
            print("Catalog plugin package is in sync.")
        return 0

    stale = check_packages(repo_root, package_roots=selected_roots)
    if not stale:
        print("Plugin package payloads are in sync.")
        return 0

    print("Plugin package generated artifacts differ from canonical sources.")
    print("Canonical sources: chief_of_staff/plugin_metadata.py, skills/cities2-chief-of-staff, chief_of_staff")
    print("generated packages: " + ", ".join(platform.dist_package_root.as_posix() for platform in platforms))
    print("Run: python -m chief_of_staff.plugin_packages sync")
    print("Stale paths:")
    for path in stale:
        print(f"- {path}")
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
