from __future__ import annotations

from typing import Any

from .models import SourceInventory, SourceStatus


def build_transit_snapshot(inventory: SourceInventory) -> dict[str, Any]:
    dataexport = _find(inventory, "dataexport")
    save = _find(inventory, "saveinvestigator")
    line_name_lookup = _line_name_lookup(save)
    station_services = _station_services(save)
    station_names_by_line = _station_names_by_line(station_services)
    live_lines = _live_lines(dataexport, line_name_lookup, station_names_by_line)

    return {
        "evidence_sources": [source.name for source in inventory.available_sources],
        "missing_sources": [source.name for source in inventory.sources if not source.available],
        "line_name_resolution": "save_investigator" if line_name_lookup else "fallback",
        "station_name_resolution": "save_investigator" if station_services else "unavailable",
        "lines": live_lines or _saved_lines(save, station_names_by_line),
        "stations": _stations(save),
        "station_services": station_services,
        "saved_queue_hotspots": _saved_queue_hotspots(save),
        "unresolved_live_stop_queues": _unresolved_live_stop_queues(live_lines),
    }


def _find(inventory: SourceInventory, name: str) -> SourceStatus | None:
    for source in inventory.sources:
        if source.name == name:
            return source
    return None


def _line_name_lookup(save: SourceStatus | None) -> dict[str, str]:
    if not save or not save.available:
        return {}
    value = save.summary.get("transit_line_names")
    return value if isinstance(value, dict) else {}


def _station_services(save: SourceStatus | None) -> list[dict[str, Any]]:
    if not save or not save.available:
        return []
    value = save.summary.get("transit_station_services")
    return value if isinstance(value, list) else []


def _stations(save: SourceStatus | None) -> list[dict[str, Any]]:
    if not save or not save.available:
        return []
    value = save.summary.get("transit_stations")
    return value if isinstance(value, list) else []


def _saved_queue_hotspots(save: SourceStatus | None) -> list[dict[str, Any]]:
    if not save or not save.available:
        return []
    value = save.summary.get("transit_saved_queue_hotspots")
    return value if isinstance(value, list) else []


def _saved_lines(save: SourceStatus | None, station_names_by_line: dict[str, list[str]]) -> list[dict[str, Any]]:
    if not save or not save.available:
        return []
    saved = save.summary.get("transit_lines")
    if not isinstance(saved, list):
        return []
    lines: list[dict[str, Any]] = []
    for line in saved:
        if not isinstance(line, dict):
            continue
        name = line.get("name")
        if not isinstance(name, str) or not name.strip():
            continue
        lines.append({**line, "station_names": station_names_by_line.get(name, [])})
    return lines


def _live_lines(
    dataexport: SourceStatus | None,
    line_name_lookup: dict[str, str],
    station_names_by_line: dict[str, list[str]],
) -> list[dict[str, Any]]:
    if not dataexport or not dataexport.available:
        return []
    hotspots = dataexport.summary.get("transit_hotspots")
    if not isinstance(hotspots, list):
        return []
    lines: list[dict[str, Any]] = []
    for hotspot in hotspots:
        if not isinstance(hotspot, dict):
            continue
        route_number = hotspot.get("route_number")
        color = str(hotspot.get("color") or "").upper()
        line_name = line_name_lookup.get(f"{route_number}|{color}") or _fallback_line_name(hotspot)
        top_stop = hotspot.get("top_stop") if isinstance(hotspot.get("top_stop"), dict) else None
        lines.append(
            {
                "name": line_name,
                "mode": hotspot.get("mode"),
                "route_number": route_number,
                "color": color,
                "waiting_passengers": hotspot.get("waiting_passengers"),
                "max_waiting_at_stop": hotspot.get("max_waiting_at_stop"),
                "station_names": station_names_by_line.get(line_name, []),
                "top_stop": top_stop,
                "name_resolution": "save_investigator" if f"{route_number}|{color}" in line_name_lookup else "fallback",
            }
        )
    return lines


def _station_names_by_line(station_services: list[dict[str, Any]]) -> dict[str, list[str]]:
    values: dict[str, set[str]] = {}
    for service in station_services:
        line_name = service.get("line_name")
        station_name = service.get("station_name")
        if not isinstance(line_name, str) or not isinstance(station_name, str):
            continue
        values.setdefault(line_name, set()).add(station_name)
    return {line_name: sorted(stations) for line_name, stations in values.items()}


def _unresolved_live_stop_queues(lines: list[dict[str, Any]]) -> list[dict[str, Any]]:
    unresolved: list[dict[str, Any]] = []
    for line in lines:
        top_stop = line.get("top_stop")
        if not isinstance(top_stop, dict) or top_stop.get("name_resolution") != "unresolved":
            continue
        unresolved.append(
            {
                "line_name": line.get("name"),
                "waiting_passengers": top_stop.get("waiting_passengers"),
                "route_position": top_stop.get("route_position"),
                "waypoint_entity_index": top_stop.get("waypoint_entity_index"),
                "unresolved_reason": top_stop.get("unresolved_reason"),
            }
        )
    return unresolved


def _fallback_line_name(hotspot: dict[str, Any]) -> str:
    fallback = hotspot.get("fallback_name")
    if isinstance(fallback, str) and fallback.strip():
        return fallback.strip()
    route_number = hotspot.get("route_number")
    color = hotspot.get("color")
    if route_number is not None and color:
        return f"Route {route_number} {color}"
    return "Unknown transit line"
