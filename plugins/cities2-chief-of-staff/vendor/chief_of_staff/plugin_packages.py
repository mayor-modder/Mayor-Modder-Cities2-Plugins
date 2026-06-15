from __future__ import annotations

import argparse
import shutil
import tempfile
from pathlib import Path
from typing import Callable, Iterable

from chief_of_staff import plugin_metadata
from chief_of_staff.plugin_metadata import SKILL_NAMES


DIST_PACKAGE_ROOT = Path("dist/plugins/cities2-chief-of-staff")
CATALOG_PACKAGE_ROOT = Path("plugins/cities2-chief-of-staff")
DEFAULT_CATALOG_ROOT = Path("../Mayor-Modder-Cities2-Plugins")

PACKAGE_ROOTS = (DIST_PACKAGE_ROOT,)

METADATA_FILES: dict[Path, tuple[tuple[Path, Callable[[], str]], ...]] = {
    DIST_PACKAGE_ROOT: (
        (DIST_PACKAGE_ROOT / ".codex-plugin" / "plugin.json", plugin_metadata.codex_plugin_json),
        (DIST_PACKAGE_ROOT / ".mcp.json", plugin_metadata.codex_mcp_json),
        (DIST_PACKAGE_ROOT / "README.md", plugin_metadata.codex_readme_md),
    ),
}

MANAGED_DIRS = ("skills", "tools", "vendor")
MANAGED_FILES = (Path("bin") / "cities2-chief-of-staff-launcher.js",)
IGNORED_DIRS = {"__pycache__", ".pytest_cache"}
IGNORED_SUFFIXES = {".pyc"}

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
    package_roots: Iterable[Path] = PACKAGE_ROOTS,
) -> tuple[Path, ...]:
    root = Path(repo_root).resolve()
    changed: list[Path] = []
    for package_root in _selected_package_roots(package_roots):
        changed.extend(_replace_payload(root, package_root))
        changed.extend(_sync_metadata(root, package_root))
    return _unique_sorted(changed)


def check_packages(
    repo_root: Path | str = Path.cwd(),
    *,
    package_roots: Iterable[Path] = PACKAGE_ROOTS,
) -> tuple[Path, ...]:
    root = Path(repo_root).resolve()
    stale: list[Path] = []
    with tempfile.TemporaryDirectory(prefix="chief-of-staff-plugin-payload-") as tmp:
        tmp_package = Path(tmp) / "payload"
        _write_payload(root, tmp_package)
        for package_root in _selected_package_roots(package_roots):
            package_abs = root / package_root
            for dirname in MANAGED_DIRS:
                stale.extend(_changed_tree_paths(tmp_package / dirname, package_abs / dirname))
            for filename in MANAGED_FILES:
                stale.extend(_changed_paths(tmp_package / filename, package_abs / filename))
            stale.extend(_check_metadata(root, package_root))
    return _unique_sorted(stale)


def sync_catalog_package(
    catalog_root: Path | str = DEFAULT_CATALOG_ROOT,
    *,
    repo_root: Path | str = Path.cwd(),
    package_root: Path = DIST_PACKAGE_ROOT,
    catalog_package_root: Path = CATALOG_PACKAGE_ROOT,
) -> tuple[Path, ...]:
    root = Path(repo_root).resolve()
    catalog = Path(catalog_root).resolve()
    marketplace = catalog / ".agents" / "plugins" / "marketplace.json"
    if not marketplace.is_file():
        raise FileNotFoundError(f"Catalog marketplace not found: {marketplace}")

    sync_packages(root, package_roots=(package_root,))
    source = root / package_root
    target = catalog / catalog_package_root
    if not target.resolve().is_relative_to(catalog):
        raise ValueError(f"Catalog package target escapes catalog root: {target}")

    changed = list(_changed_tree_paths(source, target))
    if target.exists():
        if target.is_dir():
            shutil.rmtree(target)
        else:
            target.unlink()
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copytree(source, target)
    return _unique_sorted(changed)


def _selected_package_roots(package_roots: Iterable[Path]) -> tuple[Path, ...]:
    selected = tuple(Path(path) for path in package_roots)
    unknown = [path for path in selected if path not in METADATA_FILES]
    if unknown:
        raise ValueError(f"Unknown plugin package root: {unknown[0]}")
    return selected


def _write_payload(repo_root: Path, payload_root: Path) -> None:
    _copy_skills(repo_root, payload_root)
    _copy_tools(repo_root, payload_root)
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


def _copy_tools(repo_root: Path, payload_root: Path) -> None:
    source = repo_root / "tools" / "SaveInvestigator"
    if not source.is_dir():
        raise FileNotFoundError(f"Canonical Save Investigator source not found: {source}")
    target = payload_root / "tools" / "SaveInvestigator"
    shutil.copytree(
        source,
        target,
        ignore=shutil.ignore_patterns("__pycache__", "*.pyc", ".pytest_cache", "bin", "obj", "output"),
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


def _sync_metadata(repo_root: Path, package_root: Path) -> tuple[Path, ...]:
    changed: list[Path] = []
    for target_rel, builder in METADATA_FILES[package_root]:
        target = repo_root / target_rel
        expected = builder()
        if not target.is_file() or target.read_text(encoding="utf-8") != expected:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(expected, encoding="utf-8", newline="\n")
            changed.append(target)
    return _unique_sorted(changed)


def _check_metadata(repo_root: Path, package_root: Path) -> tuple[Path, ...]:
    stale: list[Path] = []
    for target_rel, builder in METADATA_FILES[package_root]:
        target = repo_root / target_rel
        if not target.is_file() or target.read_text(encoding="utf-8") != builder():
            stale.append(target)
    return tuple(stale)


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
    parser = argparse.ArgumentParser(description="Sync generated Codex plugin packages.")
    parser.add_argument("command", choices=("sync", "check", "sync-catalog"))
    parser.add_argument("--repo-root", default=Path.cwd())
    parser.add_argument("--catalog-root", default=DEFAULT_CATALOG_ROOT)
    args = parser.parse_args(argv)

    repo_root = Path(args.repo_root)
    if args.command == "sync":
        changed = sync_packages(repo_root)
        if changed:
            print("Updated generated plugin package artifacts:")
            for path in changed:
                print(f"- {path}")
        else:
            print("Plugin package payloads are in sync.")
        return 0

    if args.command == "sync-catalog":
        changed = sync_catalog_package(args.catalog_root, repo_root=repo_root)
        if changed:
            print("Updated catalog plugin package artifacts:")
            for path in changed:
                print(f"- {path}")
        else:
            print("Catalog plugin package is in sync.")
        return 0

    stale = check_packages(repo_root)
    if not stale:
        print("Plugin package payloads are in sync.")
        return 0

    print("Plugin package generated artifacts differ from canonical sources.")
    print("Canonical sources: chief_of_staff/plugin_metadata.py, skills/brief, tools/SaveInvestigator, chief_of_staff")
    print("generated package: dist/plugins/cities2-chief-of-staff")
    print("Run: python -m chief_of_staff.plugin_packages sync")
    print("Stale paths:")
    for path in stale:
        print(f"- {path}")
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
