"""Runs the formatter of a project over a text, so the rules can run after it."""

from __future__ import annotations

import importlib.machinery
import os
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


def _which(name: str) -> str | None:
    """
    Finds a program on the path, in no directory that the current directory could stand for.

    `shutil.which` looks in the current directory first on Windows, and for an empty or `.` entry elsewhere,
    so a checkout could plant the formatter that `format` then starts.
    """
    current = os.path.normcase(str(Path.cwd().resolve()))
    extensions = [""]

    if sys.platform == "win32":
        pathext = [
            extension for extension in os.environ.get("PATHEXT", ".COM;.EXE;.BAT;.CMD").split(os.pathsep) if extension
        ]

        if not name.lower().endswith(tuple(extension.lower() for extension in pathext)):
            extensions = pathext

    for entry in os.environ.get("PATH", "").split(os.pathsep):
        directory = Path(entry.strip('"'))

        if not entry or not directory.is_absolute() or os.path.normcase(str(directory.resolve())) == current:
            continue

        for extension in extensions:
            candidate = directory / f"{name}{extension}"

            if candidate.is_file() and os.access(candidate, os.X_OK):
                return str(candidate)

    return None


def _safe_path() -> list[str]:
    """Lists the module search path of this interpreter without the current directory, which a checkout could fill."""
    current = os.path.normcase(os.path.realpath(os.getcwd()))
    return [entry for entry in sys.path if os.path.normcase(os.path.realpath(entry or os.getcwd())) != current]


def _has_module(name: str) -> bool:
    return importlib.machinery.PathFinder.find_spec(name, _safe_path()) is not None


# Runs a module as `python -m` does, but with the current directory off the search path, so a planted module cannot win.
# Unlike `-I` and `-P` it keeps the user site-packages and the environment, and it runs on every supported Python.
_MODULE_RUNNER = """
import os, runpy, sys
current = os.path.normcase(os.path.realpath(os.getcwd()))
sys.path[:] = [p for p in sys.path if os.path.normcase(os.path.realpath(p or os.getcwd())) != current]
runpy.run_module({name!r}, run_name="__main__", alter_sys=True)
"""


def _module_command(name: str) -> tuple[str, ...]:
    return (sys.executable, "-c", _MODULE_RUNNER.format(name=name))


def _locate(name: str) -> Formatter | None:
    executable = _which(name)

    if executable is not None:
        return Formatter(name, (executable,))

    if _has_module(name):
        return Formatter(name, _module_command(name))

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
