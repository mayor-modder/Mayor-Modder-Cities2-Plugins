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
    service_joins = _read_json(latest / "transport-service-join-facts.json") or {}
    line_groups = _first_present(transport, "lineGroups", "LineGroups") if isinstance(transport, dict) else None
    station_groups = _first_present(transport, "stationGroups", "StationGroups") if isinstance(transport, dict) else None
    service_join_stations = _first_present(service_joins, "stations", "Stations") if isinstance(service_joins, dict) else None
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
            "transit_lines": _extract_transit_lines(line_groups),
            "transit_stations": _extract_transit_stations(station_groups),
            "transit_station_services": _extract_transit_station_services(service_join_stations),
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
                "top_stop": _top_transit_stop(_first_present(line, "stops", "Stops")),
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


def _extract_transit_lines(line_groups: object) -> list[dict[str, Any]]:
    if not isinstance(line_groups, list):
        return []
    resolved: list[dict[str, Any]] = []
    for group in line_groups:
        if not isinstance(group, dict):
            continue
        mode = _first_present(group, "mode", "Mode")
        lines = _first_present(group, "lines", "Lines")
        if not isinstance(lines, list):
            continue
        for line in lines:
            if not isinstance(line, dict):
                continue
            display_name = _first_present(line, "displayName", "DisplayName")
            if not display_name:
                continue
            color = _normalize_color(_first_present(line, "colorHex", "ColorHex"))
            resolved.append(
                {
                    "name": str(display_name),
                    "line_entity_index": _first_present(line, "lineEntityIndex", "LineEntityIndex"),
                    "route_number": _first_present(line, "routeNumber", "RouteNumber"),
                    "color": color,
                    "mode": _first_present(line, "mode", "Mode") or mode,
                    "is_cargo": _first_present(line, "isCargo", "IsCargo"),
                    "name_resolution": "save_investigator",
                }
            )
    return resolved


def _extract_transit_stations(station_groups: object) -> list[dict[str, Any]]:
    if not isinstance(station_groups, list):
        return []
    stations: list[dict[str, Any]] = []
    for group in station_groups:
        if not isinstance(group, dict):
            continue
        group_mode = _first_present(group, "mode", "Mode")
        group_stations = _first_present(group, "stations", "Stations")
        if not isinstance(group_stations, list):
            continue
        for station in group_stations:
            if not isinstance(station, dict):
                continue
            name = _first_present(station, "name", "Name")
            if not name:
                continue
            stations.append(
                {
                    "name": str(name),
                    "mode": _first_present(station, "mode", "Mode") or group_mode,
                    "role": _first_present(station, "role", "Role"),
                    "join_status": _first_present(station, "serviceJoinStatus", "ServiceJoinStatus"),
                    "served_line_names": _string_list(_first_present(station, "servedLineNames", "ServedLineNames")),
                    "candidate_line_names": _string_list(
                        _first_present(station, "candidateLineNames", "CandidateLineNames")
                    ),
                    "platform_names": _string_list(_first_present(station, "platformNames", "PlatformNames")),
                    "entrance_names": _string_list(_first_present(station, "entranceNames", "EntranceNames")),
                    "name_resolution": "save_investigator",
                }
            )
    return stations


def _extract_transit_station_services(stations: object) -> list[dict[str, Any]]:
    if not isinstance(stations, list):
        return []
    services: list[dict[str, Any]] = []
    for station in stations:
        if not isinstance(station, dict):
            continue
        station_name = _first_present(station, "name", "Name")
        if not station_name:
            continue
        exact_lines = _first_present(station, "exactLines", "ExactLines")
        if not isinstance(exact_lines, list):
            continue
        for line in exact_lines:
            if not isinstance(line, dict):
                continue
            line_name = _first_present(line, "lineName", "LineName")
            if not line_name:
                continue
            services.append(
                {
                    "station_name": str(station_name),
                    "station_mode": _first_present(station, "mode", "Mode"),
                    "station_role": _first_present(station, "role", "Role"),
                    "join_status": _first_present(station, "joinStatus", "JoinStatus"),
                    "line_name": str(line_name),
                    "line_entity_index": _first_present(line, "lineEntityIndex", "LineEntityIndex"),
                    "route_number": _first_present(line, "routeNumber", "RouteNumber"),
                    "color": _normalize_color(_first_present(line, "colorHex", "ColorHex")),
                    "join_component_type": _first_present(line, "joinComponentType", "JoinComponentType"),
                    "name_resolution": "save_investigator",
                }
            )
    return services


def _top_transit_stop(stops: object) -> dict[str, Any] | None:
    if not isinstance(stops, list):
        return None
    top: dict[str, Any] | None = None
    top_waiting = 0.0
    for stop in stops:
        if not isinstance(stop, dict):
            continue
        waiting = _first_present(stop, "waiting_passengers", "WaitingPassengers")
        if not isinstance(waiting, (int, float)):
            continue
        if top is None or waiting > top_waiting:
            top_waiting = float(waiting)
            top = stop
    if top is None:
        return None
    stop_name = _first_present(top, "stop_name", "StopName")
    return {
        "name": str(stop_name).strip() if isinstance(stop_name, str) and stop_name.strip() else None,
        "waiting_passengers": int(top_waiting),
        "waypoint_entity_index": _first_present(top, "waypoint_entity_index", "WaypointEntityIndex"),
        "route_position": _first_present(top, "route_position", "RoutePosition"),
        "name_resolution": "dataexport" if isinstance(stop_name, str) and stop_name.strip() else "unresolved",
        "unresolved_reason": None
        if isinstance(stop_name, str) and stop_name.strip()
        else "DataExport did not include a stop name, and Save Investigator does not yet expose a live waypoint-to-station mapping.",
    }


def _string_list(value: object) -> list[str]:
    if not isinstance(value, list):
        return []
    return [str(item) for item in value if isinstance(item, str) and item.strip()]


def _line_key(route_number: object, color: str) -> str:
    return f"{route_number}|{color.upper()}"


def _normalize_color(value: object) -> str:
    if not isinstance(value, str):
        return ""
    color = value.strip().upper()
    if color and not color.startswith("#"):
        color = f"#{color}"
    return color
