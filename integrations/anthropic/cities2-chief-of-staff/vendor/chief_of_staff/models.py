from __future__ import annotations

from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Any


@dataclass(frozen=True)
class SourceStatus:
    name: str
    label: str
    available: bool
    coverage_state: str
    path: str
    kind: str
    message: str
    summary: dict[str, Any] = field(default_factory=dict)

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)


@dataclass(frozen=True)
class SourceInventory:
    sources: list[SourceStatus]

    @property
    def available_sources(self) -> list[SourceStatus]:
        return [source for source in self.sources if source.available]

    @property
    def missing_sources(self) -> list[SourceStatus]:
        return [source for source in self.sources if not source.available]

    def to_dict(self) -> dict[str, Any]:
        return {"sources": [source.to_dict() for source in self.sources]}


@dataclass(frozen=True)
class CityReport:
    city_name: str
    evidence_sources: list[str]
    missing_sources: list[str]
    missing_optional_sources: list[str]
    markdown: str
    facts: dict[str, Any] = field(default_factory=dict)

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)


def path_str(path: Path) -> str:
    return str(path.expanduser().resolve())
