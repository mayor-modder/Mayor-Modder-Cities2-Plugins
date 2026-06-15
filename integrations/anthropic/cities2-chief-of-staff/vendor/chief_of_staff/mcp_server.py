from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any

if __package__ in (None, ""):
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
    from chief_of_staff import __version__
    from chief_of_staff.analysis import build_city_report
    from chief_of_staff.save_investigator import SaveInvestigatorRefreshError, refresh_save_investigator_output
    from chief_of_staff.sources import discover_sources
else:
    from . import __version__
    from .analysis import build_city_report
    from .save_investigator import SaveInvestigatorRefreshError, refresh_save_investigator_output
    from .sources import discover_sources

JSON = dict[str, Any]
SERVER_NAME = "Cities2-ChiefOfStaff"
SERVER_INSTRUCTIONS = (
    "Cities2-ChiefOfStaff analyzes available Cities: Skylines II city evidence "
    "as the Mayor's office Chief of Staff. It works without optional companion "
    "mods, and becomes more useful when Cities2-DataExport, "
    "Cities2-InfoLoomBridge, or Save Investigator outputs are available."
)


def handle_request(message: JSON, config: JSON) -> JSON | None:
    method = str(message.get("method", ""))
    req_id = message.get("id")
    params = message.get("params") if isinstance(message.get("params"), dict) else {}
    if method == "initialize":
        return {
            "jsonrpc": "2.0",
            "id": req_id,
            "result": {
                "protocolVersion": "2025-06-18",
                "serverInfo": {"name": SERVER_NAME, "version": __version__},
                "capabilities": {"tools": {"listChanged": False}},
                "instructions": SERVER_INSTRUCTIONS,
            },
        }
    if method == "notifications/initialized":
        return None
    if method == "ping":
        return {"jsonrpc": "2.0", "id": req_id, "result": {}}
    if method == "tools/list":
        return {"jsonrpc": "2.0", "id": req_id, "result": {"tools": tools_catalog()}}
    if method == "tools/call":
        return _handle_tool_call(req_id, params, config)
    return {"jsonrpc": "2.0", "id": req_id, "error": {"code": -32601, "message": f"Unknown method: {method}"}}


def tools_catalog() -> list[JSON]:
    return [
        {
            "name": "chief_of_staff_get_status",
            "description": "List detected Cities: Skylines II city evidence sources for the Chief of Staff.",
            "inputSchema": {"type": "object", "properties": {}},
        },
        {
            "name": "chief_of_staff_analyze_city",
            "description": "Refresh Save Investigator, then analyze available city evidence and return a structured Chief of Staff report.",
            "inputSchema": {
                "type": "object",
                "properties": {
                    "mods_data_dir": {"type": "string"},
                    "save_path": {"type": "string"},
                    "save_investigator_project": {"type": "string"},
                    "save_investigator_output_dir": {"type": "string"},
                    "skip_save_investigator_refresh": {"type": "boolean"},
                },
            },
        },
        {
            "name": "chief_of_staff_get_report",
            "description": "Refresh Save Investigator, then return the current Chief of Staff brief as Markdown.",
            "inputSchema": {
                "type": "object",
                "properties": {
                    "mods_data_dir": {"type": "string"},
                    "save_path": {"type": "string"},
                    "save_investigator_project": {"type": "string"},
                    "save_investigator_output_dir": {"type": "string"},
                    "skip_save_investigator_refresh": {"type": "boolean"},
                },
            },
        },
    ]


def _handle_tool_call(req_id: object, params: JSON, config: JSON) -> JSON:
    name = str(params.get("name", ""))
    args = params.get("arguments") if isinstance(params.get("arguments"), dict) else {}
    if name == "chief_of_staff_get_status":
        inventory = _discover_sources(args, config, refresh_save_investigator=False)
        return {"jsonrpc": "2.0", "id": req_id, "result": _text_result(inventory.to_dict())}
    if name == "chief_of_staff_analyze_city":
        try:
            inventory = _discover_sources(args, config, refresh_save_investigator=True)
        except ValueError as exception:
            return {"jsonrpc": "2.0", "id": req_id, "error": {"code": -32000, "message": str(exception)}}
        return {"jsonrpc": "2.0", "id": req_id, "result": _text_result(build_city_report(inventory).to_dict())}
    if name == "chief_of_staff_get_report":
        try:
            inventory = _discover_sources(args, config, refresh_save_investigator=True)
        except ValueError as exception:
            return {"jsonrpc": "2.0", "id": req_id, "error": {"code": -32000, "message": str(exception)}}
        return {"jsonrpc": "2.0", "id": req_id, "result": _text_result(build_city_report(inventory).markdown)}
    return {"jsonrpc": "2.0", "id": req_id, "error": {"code": -32602, "message": f"Unknown tool: {name}"}}


def _discover_sources(args: JSON, config: JSON, *, refresh_save_investigator: bool) -> object:
    mods_data_dir = args.get("mods_data_dir") or config.get("mods_data_dir")
    save_investigator_output_dir = args.get("save_investigator_output_dir") or config.get(
        "save_investigator_output_dir"
    )
    use_existing_save_investigator_output = True
    skip_refresh = (
        args["skip_save_investigator_refresh"]
        if "skip_save_investigator_refresh" in args
        else config.get("skip_save_investigator_refresh")
    )
    if refresh_save_investigator and not _truthy(skip_refresh):
        try:
            refreshed = refresh_save_investigator_output(
                project_path=args.get("save_investigator_project") or config.get("save_investigator_project"),
                save_path=args.get("save_path") or config.get("save_path"),
            )
        except SaveInvestigatorRefreshError as exception:
            raise ValueError(str(exception)) from exception
        if refreshed.output_root is not None:
            save_investigator_output_dir = refreshed.output_root
        else:
            use_existing_save_investigator_output = False

    return discover_sources(
        mods_data_dir=mods_data_dir,
        save_investigator_output_dir=save_investigator_output_dir,
        use_existing_save_investigator_output=use_existing_save_investigator_output,
    )


def _truthy(value: object) -> bool:
    if isinstance(value, bool):
        return value
    if isinstance(value, str):
        return value.strip().lower() in {"1", "true", "yes", "on"}
    return bool(value)


def _text_result(payload: object) -> JSON:
    if isinstance(payload, (dict, list)):
        text = json.dumps(payload, indent=2)
    else:
        text = str(payload)
    return {"content": [{"type": "text", "text": text}]}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=SERVER_NAME)
    parser.add_argument("--mods-data")
    parser.add_argument("--save-path")
    parser.add_argument("--save-investigator-project")
    parser.add_argument("--save-investigator-output")
    parser.add_argument("--skip-save-investigator-refresh", action="store_true")
    parser.add_argument("--version", action="store_true")
    args = parser.parse_args(argv)
    if args.version:
        print(f"cities2-chief-of-staff {__version__}")
        return 0
    config = {
        "mods_data_dir": args.mods_data,
        "save_path": args.save_path,
        "save_investigator_project": args.save_investigator_project,
        "save_investigator_output_dir": args.save_investigator_output,
        "skip_save_investigator_refresh": args.skip_save_investigator_refresh,
    }
    while True:
        message = _read_message()
        if message is None:
            return 0
        response = handle_request(message, config)
        if response is not None:
            _send_message(response)


def _read_message() -> JSON | None:
    header_lines: list[str] = []
    while True:
        line = sys.stdin.buffer.readline()
        if line == b"":
            return None
        if line in (b"\r\n", b"\n"):
            break
        header_lines.append(line.decode("ascii", errors="replace").strip())
    length = 0
    for line in header_lines:
        name, _, value = line.partition(":")
        if name.lower() == "content-length":
            length = int(value.strip())
    if length <= 0:
        return None
    payload = sys.stdin.buffer.read(length)
    value = json.loads(payload.decode("utf-8"))
    return value if isinstance(value, dict) else None


def _send_message(message: JSON) -> None:
    payload = json.dumps(message, separators=(",", ":")).encode("utf-8")
    sys.stdout.buffer.write(f"Content-Length: {len(payload)}\r\n\r\n".encode("ascii"))
    sys.stdout.buffer.write(payload)
    sys.stdout.buffer.flush()


if __name__ == "__main__":
    raise SystemExit(main())
