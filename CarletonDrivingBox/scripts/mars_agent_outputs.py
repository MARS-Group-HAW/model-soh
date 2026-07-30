"""Resolve MARS agent output filenames (CarletonCarDriver preferred, CarDriver fallback)."""
from __future__ import annotations

from pathlib import Path

AGENT_NAMES = ("CarletonCarDriver", "CarDriver")


def agent_output_path(directory: Path, suffix: str) -> Path:
    """Return existing agent output path, or default CarletonCarDriver name."""
    for name in AGENT_NAMES:
        path = directory / f"{name}{suffix}"
        if path.is_file():
            return path
    return directory / f"CarletonCarDriver{suffix}"
