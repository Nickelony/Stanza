"""Reads TOML with the reader of the running interpreter: `tomllib`, else `tomli` on Python 3.10."""

from __future__ import annotations

import sys
from pathlib import Path
from typing import Any, cast

if sys.version_info >= (3, 11):
    import tomllib
else:  # pragma: no cover - exercised on Python 3.10 only
    import tomli as tomllib

TOMLDecodeError = tomllib.TOMLDecodeError


def read_tool_table(path: Path) -> dict[str, Any]:
    """
    Reads the `[tool]` table of a TOML file, or an empty table when it has none.

    Raises `OSError` when the file cannot be read and `TOMLDecodeError` when it is no TOML.
    """
    with path.open("rb") as handle:
        tool = tomllib.load(handle).get("tool", {})

    return cast("dict[str, Any]", tool) if isinstance(tool, dict) else {}
