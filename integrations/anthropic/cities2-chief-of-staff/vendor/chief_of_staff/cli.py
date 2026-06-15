from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Sequence

from .analysis import build_city_report
from .save_investigator import SaveInvestigatorRefreshError, refresh_save_investigator_output
from .sources import discover_sources


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Cities2-ChiefOfStaff")
    subparsers = parser.add_subparsers(dest="command", required=True)
    _add_common_options(subparsers.add_parser("status", help="Show available city evidence sources."))
    _add_common_options(subparsers.add_parser("analyze", help="Build a city report from available evidence."))
    args = parser.parse_args(list(argv) if argv is not None else None)

    save_investigator_output_dir = Path(args.save_investigator_output) if args.save_investigator_output else None
    use_existing_save_investigator_output = True
    if args.command == "analyze" and not args.skip_save_investigator_refresh:
        try:
            refreshed = refresh_save_investigator_output(
                project_path=Path(args.save_investigator_project) if args.save_investigator_project else None,
                save_path=Path(args.save_path) if args.save_path else None,
            )
        except SaveInvestigatorRefreshError as exception:
            print(str(exception), file=sys.stderr)
            return 1
        if refreshed.output_root is not None:
            save_investigator_output_dir = refreshed.output_root
        else:
            use_existing_save_investigator_output = False

    inventory = discover_sources(
        mods_data_dir=Path(args.mods_data) if args.mods_data else None,
        save_investigator_output_dir=save_investigator_output_dir,
        use_existing_save_investigator_output=use_existing_save_investigator_output,
    )
    if args.command == "status":
        if args.json:
            print(json.dumps(inventory.to_dict(), indent=2))
        else:
            for source in inventory.sources:
                state = "available" if source.available else "missing"
                print(f"{source.label}: {state} ({source.path})")
        return 0
    if args.command == "analyze":
        report = build_city_report(inventory)
        if args.json:
            print(json.dumps(report.to_dict(), indent=2))
        else:
            print(report.markdown, end="")
        return 0
    parser.error(f"unknown command: {args.command}")
    return 2


def _add_common_options(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("--mods-data", help="Path to the Cities: Skylines II ModsData folder.")
    parser.add_argument("--save-investigator-output", help="Path to Save Investigator output root.")
    parser.add_argument("--save-investigator-project", help="Path to the Save Investigator .csproj.")
    parser.add_argument("--save-path", help="Path to the .cok save file to refresh before analyzing.")
    parser.add_argument(
        "--skip-save-investigator-refresh",
        action="store_true",
        help="Analyze existing evidence without running Save Investigator first.",
    )
    parser.add_argument("--json", action="store_true", help="Print JSON instead of text.")


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
