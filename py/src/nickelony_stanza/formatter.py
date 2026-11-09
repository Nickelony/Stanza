"""Runs the formatter of a project over a text, so the rules can run after it."""

from __future__ import annotations

import importlib.util
import shutil
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Literal, get_args

from nickelony_stanza.tomlreader import TOMLDecodeError, read_tool_table

FormatterName = Literal["auto", "ruff", "black", "none"]

# The formatters a project can name, from the default, which detects one, to the choice that runs none.
FORMATTERS: tuple[FormatterName, ...] = get_args(FormatterName)
DEFAULT_FORMATTER: FormatterName = "auto"

# The formatters `auto` can pick, in the order it prefers them.
_DETECTABLE: tuple[Literal["ruff", "black"], ...] = ("ruff", "black")
_RUFF_FILES = ("ruff.toml", ".ruff.toml")


class FormatterError(Exception):
    """A formatter that is not installed, or that refuses a text."""


@dataclass(frozen=True)
class Formatter:
    """A formatter and the command that starts it."""

    name: str
    command: tuple[str, ...]

    def format(self, text: bytes, filename: str) -> bytes:
        """Formats a text through standard input, named `filename` so the formatter finds the configuration for it."""
        # A name that starts with a hyphen would read as an option, so it takes a prefix.
        named = f"./{filename}" if filename.startswith("-") else filename

        if self.name == "ruff":
            arguments = ["format", "--stdin-filename", named, "-"]
        else:
            arguments = ["-q", "--stdin-filename", named, "-"]

        result = subprocess.run([*self.command, *arguments], input=text, capture_output=True, check=False)

        if result.returncode != 0:
            message = result.stderr.decode(errors="replace").strip() or f"exit code {result.returncode}"
            raise FormatterError(f"{self.name}: {message}")

        return result.stdout


def _configured_formatter(directory: Path) -> Literal["ruff", "black"] | None:
    """Names the formatter a directory configures: Ruff by a table or a file of its own, else Black by its table."""
    if any((directory / name).is_file() for name in _RUFF_FILES):
        return "ruff"

    pyproject = directory / "pyproject.toml"

    if not pyproject.is_file():
        return None

    try:
        tables = read_tool_table(pyproject)
    except (OSError, TOMLDecodeError):
        return None

    return next((name for name in _DETECTABLE if name in tables), None)


def detect_formatter(start: Path) -> Literal["ruff", "black"] | None:
    """
    Finds the formatter the project configures, from a directory upward.

    The nearest directory that configures either formatter decides, and Ruff wins inside it.
    """
    directory = start.resolve()

    for candidate in (directory, *directory.parents):
        configured = _configured_formatter(candidate)

        if configured is not None:
            return configured

    return None


def _locate(name: str) -> Formatter | None:
    executable = shutil.which(name)

    if executable is not None:
        return Formatter(name, (executable,))

    if importlib.util.find_spec(name) is not None:
        return Formatter(name, (sys.executable, "-m", name))

    return None


def _require(name: str) -> Formatter:
    formatter = _locate(name)

    if formatter is None:
        raise FormatterError(
            f'{name} is not installed. Install it, or set formatter to a formatter you have, or to "none".'
        )

    return formatter


def _find_automatically(start: Path) -> Formatter:
    configured = detect_formatter(start)

    if configured is not None:
        return _require(configured)

    for name in _DETECTABLE:
        formatter = _locate(name)

        if formatter is not None:
            return formatter

    raise FormatterError(
        "no formatter found: the project configures neither Ruff nor Black, and neither is installed. "
        "Configure one with [tool.ruff] or [tool.black], install ruff or black, or run with --formatter none."
    )


def find_formatter(name: FormatterName, start: Path | None = None) -> Formatter | None:
    """
    Finds the formatter on the path, else as a module of this interpreter, and returns None for "none".

    For "auto" it takes the formatter the project configures from `start` upward, else the first one installed.
    """
    if name == "none":
        return None

    return _find_automatically(start or Path.cwd()) if name == "auto" else _require(name)
