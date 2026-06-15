from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from .models import SourceInventory, SourceStatus, path_str
from .paths import default_mods_data_dir, default_save_investigator_output_dir


def discover_sources(
    mods_data_dir: Path | str | None = None,
    save_investigator_output_dir: Path | str | None = None,
    *,
    use_existing_save_investigator_output: bool = True,
) -> SourceInventory:
    mods_data = Path(mods_data_dir).expanduser() if mods_data_dir is not None else default_mods_data_dir()
    save_output = (
        Path(save_investigator_output_dir).expanduser()
        if save_investigator_output_dir is not None
        else default_save_investigator_output_dir()
    )
    return SourceInventory(
        [
            _dataexport_status(mods_data),
            _save_investigator_status(save_output, use_existing_output=use_existing_save_investigator_output),
            _infoloom_status(mods_data),
        ]
    )


def _dataexport_status(mods_data: Path) -> SourceStatus:
    path = mods_data / "CS2DataExport" / "latest.json"
    payload = _read_json(path)
    if payload is None:
        return SourceStatus(
            name="dataexport",
            label="Cities2-DataExport",
            available=False,
            coverage_state="missing",
            path=path_str(path),
            kind="live_sample",
            message="No DataExport latest.json found.",
        )
    city = _as_dict(payload.get("city"))
    if not city:
        city = _as_dict(payload.get("City"))
    population = _as_dict(payload.get("population"))
    if not population:
        population = _as_dict(payload.get("Population"))
    transport = _as_dict(payload.get("transport_proxies"))
    if not transport:
        transport = _as_dict(payload.get("TransportProxies"))
    transit_detail = _as_dict(payload.get("transit_line_detail_semantics"))
    if not transit_detail:
        transit_detail = _as_dict(payload.get("TransitLineDetailSemantics"))
    return SourceStatus(
        name="dataexport",
        label="Cities2-DataExport",
        available=True,
        coverage_state="usable",
        path=path_str(path),
        kind="live_sample",
        message="DataExport latest.json is available.",
        summary={
            "city_name": _first_present(city, "city_name", "CityName"),
            "exported_at_utc": _first_present(payload, "exported_at_utc", "ExportedAtUtc"),
            "schema_version": _first_present(payload, "schema_version", "SchemaVersion"),
            "total_population": _first_present(population, "total_population", "TotalPopulation"),
            "active_transport_lines": _first_present(
                transport,
                "active_transport_lines",
                "ActiveTransportLines",
            ),
            "transit_hotspots": _extract_transit_hotspots(transit_detail),
        },
    )


def _infoloom_status(mods_data: Path) -> SourceStatus:
    path = mods_data / "InfoLoomBridge" / "latest.json"
    payload = _read_json(path)
    if payload is None:
        return SourceStatus(
            name="infoloombridge",
            label="Cities2-InfoLoomBridge",
            available=False,
            coverage_state="missing",
            path=path_str(path),
            kind="optional_live_detail",
            message="No InfoLoomBridge latest.json found.",
        )
    panels = _as_dict(payload.get("panels"))
    return SourceStatus(
        name="infoloombridge",
        label="Cities2-InfoLoomBridge",
        available=True,
        coverage_state="usable",
        path=path_str(path),
        kind="optional_live_detail",
        message="InfoLoomBridge latest.json is available.",
        summary={
            "exported_at_utc": _first_present(payload, "exported_at_utc", "generated_at"),
            "panel_count": len(panels),
            "panels": sorted(panels.keys()),
        },
    )


def _save_investigator_status(output_root: Path, *, use_existing_output: bool) -> SourceStatus:
    if not use_existing_output:
        return SourceStatus(
            name="saveinvestigator",
            label="Save Investigator",
            available=False,
            coverage_state="missing",
            path=path_str(output_root),
            kind="save_analysis",
            message="Save Investigator refresh did not produce a fresh output.",
        )
    latest = _latest_child_dir(output_root)
    if latest is None:
        return SourceStatus(
            name="saveinvestigator",
            label="Save Investigator",
            available=False,
            coverage_state="missing",
            path=path_str(output_root),
            kind="save_analysis",
            message="No Save Investigator output directory found.",
        )
    city_state = _read_json(latest / "city-state-report-facts.json") or {}
    transport = _read_json(latest / "transport-report-facts.json") or {}
    line_groups = _first_present(transport, "lineGroups", "LineGroups") if isinstance(transport, dict) else None
    return SourceStatus(
        name="saveinvestigator",
        label="Save Investigator",
        available=True,
        coverage_state="usable",
        path=path_str(latest),
        kind="save_analysis",
        message="Save Investigator output is available.",
        summary={
            "latest_output": path_str(latest),
            "estimated_completion_percent": _first_present(
                _as_dict(city_state),
                "estimatedCompletionPercent",
                "EstimatedCompletionPercent",
            ),
            "transport_line_group_count": len(line_groups) if isinstance(line_groups, list) else None,
            "transit_line_names": _extract_transit_line_names(line_groups),
        },
    )


def _latest_child_dir(path: Path) -> Path | None:
    if not path.is_dir():
        return None
    children = [child for child in path.iterdir() if child.is_dir()]
    if not children:
        return None
    return max(children, key=lambda child: child.stat().st_mtime)


def _read_json(path: Path) -> dict[str, Any] | None:
    if not path.is_file():
        return None
    try:
        value = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError):
        return None
    return value if isinstance(value, dict) else None


def _as_dict(value: object) -> dict[str, Any]:
    return value if isinstance(value, dict) else {}


def _first_present(payload: dict[str, Any], *keys: str) -> Any:
    for key in keys:
        if key in payload:
            return payload[key]
    return None


def _extract_transit_hotspots(transit_detail: dict[str, Any]) -> list[dict[str, Any]]:
    lines = _first_present(transit_detail, "lines", "Lines")
    if not isinstance(lines, list):
        return []
    hotspots: list[dict[str, Any]] = []
    for line in lines:
        if not isinstance(line, dict):
            continue
        waiting = _first_present(line, "waiting_passengers_all_stops", "WaitingPassengersAllStops")
        if not isinstance(waiting, (int, float)) or waiting <= 0:
            continue
        hotspots.append(
            {
                "route_number": _first_present(line, "route_number", "RouteNumber"),
                "mode": _first_present(line, "mode", "Mode"),
                "color": _normalize_color(_first_present(line, "line_color", "LineColor")),
                "fallback_name": _first_present(line, "line_name", "LineName", "LineIdentifier"),
                "waiting_passengers": waiting,
                "max_waiting_at_stop": _first_present(
                    line,
                    "max_waiting_passengers_at_stop",
                    "MaxWaitingPassengersAtStop",
                ),
            }
        )
    hotspots.sort(key=lambda item: float(item.get("waiting_passengers") or 0), reverse=True)
    return hotspots[:5]


def _extract_transit_line_names(line_groups: object) -> dict[str, str]:
    if not isinstance(line_groups, list):
        return {}
    names: dict[str, str] = {}
    for group in line_groups:
        if not isinstance(group, dict):
            continue
        lines = _first_present(group, "lines", "Lines")
        if not isinstance(lines, list):
            continue
        for line in lines:
            if not isinstance(line, dict):
                continue
            display_name = _first_present(line, "displayName", "DisplayName")
            route_number = _first_present(line, "routeNumber", "RouteNumber")
            color = _normalize_color(_first_present(line, "colorHex", "ColorHex"))
            if display_name and route_number is not None and color:
                names[_line_key(route_number, color)] = str(display_name)
    return names


def _line_key(route_number: object, color: str) -> str:
    return f"{route_number}|{color.upper()}"


def _normalize_color(value: object) -> str:
    if not isinstance(value, str):
        return ""
    color = value.strip().upper()
    if color and not color.startswith("#"):
        color = f"#{color}"
    return color
