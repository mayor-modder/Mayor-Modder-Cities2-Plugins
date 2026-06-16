from __future__ import annotations

from typing import Any

from .models import CityReport, SourceInventory, SourceStatus


def build_city_report(inventory: SourceInventory) -> CityReport:
    dataexport = _find(inventory, "dataexport")
    save = _find(inventory, "saveinvestigator")
    infoloom = _find(inventory, "infoloombridge")
    city_name = _first_text(
        dataexport.summary.get("city_name") if dataexport else None,
        "Unknown city",
    )
    facts: dict[str, Any] = {}
    lines = ["# Chief of Staff Brief", ""]
    lines.append(f"City: {city_name}")
    lines.append("")
    lines.append("## Evidence Coverage")
    for source in inventory.sources:
        lines.append(f"- {source.label}: {source.coverage_state}")
    lines.append("")
    lines.append("## Summary")

    transit_hotspots: list[dict[str, Any]] = []
    if dataexport and dataexport.available:
        _add_fact_line(lines, facts, "Population", dataexport.summary.get("total_population"), number=True)
        _add_fact_line(lines, facts, "Active transport lines", dataexport.summary.get("active_transport_lines"), number=True)
        transit_hotspots = _named_transit_hotspots(dataexport, save)
    if save and save.available:
        _add_fact_line(
            lines,
            facts,
            "Save understanding",
            save.summary.get("estimated_completion_percent"),
            suffix="%",
        )
    if infoloom and infoloom.available:
        panels = infoloom.summary.get("panels")
        if isinstance(panels, list):
            facts["infoloom_panels"] = panels
            lines.append(f"- InfoLoom panels: {', '.join(str(panel) for panel in panels) or 'none'}")

    if transit_hotspots:
        facts["transit_hotspots"] = transit_hotspots
        lines.append("")
        lines.append("## Transit Hotspots")
        for hotspot in transit_hotspots:
            name = hotspot["name"]
            waiting = hotspot["waiting_passengers"]
            max_stop = hotspot.get("max_waiting_at_stop")
            if max_stop is None:
                lines.append(f"- {name}: {waiting:,} waiting")
            else:
                lines.append(f"- {name}: {waiting:,} waiting, max stop {max_stop:,}")

    lines.append("")
    lines.append("## Confidence Notes")
    missing_sources = [source for source in inventory.sources if not source.available]
    if missing_sources:
        for source in missing_sources:
            if source.name == "saveinvestigator":
                lines.append("- Missing Save Investigator limits save-derived diagnosis.")
            elif source.name == "dataexport":
                lines.append("- Missing Cities2-DataExport limits live city sample diagnosis.")
            elif source.name == "infoloombridge":
                lines.append("- Missing Cities2-InfoLoomBridge limits detailed InfoLoom-derived diagnosis.")
            else:
                lines.append(f"- Missing {source.label}: {source.message}")
    else:
        lines.append("- All known evidence sources are usable.")

    missing_source_names = [source.name for source in missing_sources]

    return CityReport(
        city_name=city_name,
        evidence_sources=[source.name for source in inventory.available_sources],
        missing_sources=missing_source_names,
        missing_optional_sources=missing_source_names,
        markdown="\n".join(lines).strip() + "\n",
        facts=facts,
    )


def _find(inventory: SourceInventory, name: str) -> SourceStatus | None:
    for source in inventory.sources:
        if source.name == name:
            return source
    return None


def _first_text(*values: object) -> str:
    for value in values:
        if isinstance(value, str) and value.strip():
            return value.strip()
    return "Unknown"


def _add_fact_line(
    lines: list[str],
    facts: dict[str, Any],
    label: str,
    value: object,
    *,
    number: bool = False,
    suffix: str = "",
) -> None:
    if value is None:
        return
    key = label.lower().replace(" ", "_")
    facts[key] = value
    if number and isinstance(value, (int, float)):
        rendered = f"{value:,.0f}"
    else:
        rendered = str(value)
    lines.append(f"- {label}: {rendered}{suffix}")


def _named_transit_hotspots(dataexport: SourceStatus, save: SourceStatus | None) -> list[dict[str, Any]]:
    hotspots = dataexport.summary.get("transit_hotspots")
    if not isinstance(hotspots, list):
        return []
    name_lookup = {}
    if save and save.available and isinstance(save.summary.get("transit_line_names"), dict):
        name_lookup = save.summary["transit_line_names"]
    named: list[dict[str, Any]] = []
    for hotspot in hotspots:
        if not isinstance(hotspot, dict):
            continue
        route_number = hotspot.get("route_number")
        color = str(hotspot.get("color") or "").upper()
        name = name_lookup.get(f"{route_number}|{color}") or _fallback_line_name(hotspot)
        waiting = hotspot.get("waiting_passengers")
        if not isinstance(waiting, (int, float)):
            continue
        named.append(
            {
                "name": name,
                "waiting_passengers": int(waiting),
                "max_waiting_at_stop": hotspot.get("max_waiting_at_stop"),
                "route_number": route_number,
                "color": color,
            }
        )
    return named


def _fallback_line_name(hotspot: dict[str, Any]) -> str:
    fallback = hotspot.get("fallback_name")
    if isinstance(fallback, str) and fallback.strip():
        return fallback.strip()
    route_number = hotspot.get("route_number")
    color = hotspot.get("color")
    if route_number is not None and color:
        return f"Route {route_number} {color}"
    return "Unknown transit line"
