"""Finds the Python files a run reads: the paths it is given, minus the directories and globs it skips."""

from __future__ import annotations

import os
import re
import sys
from collections.abc import Iterator, Sequence
from pathlib import Path, PurePosixPath

SKIPPED_DIRECTORIES = frozenset(
    {".git", ".hg", ".svn", ".venv", "venv", "__pycache__", "node_modules", ".tox", ".nox", ".mypy_cache"}
    | {".ruff_cache", ".pytest_cache", "build", "dist", ".eggs"}
)


def _translate_glob(pattern: str) -> re.Pattern[str]:
    parts: list[str] = []
    index = 0

    while index < len(pattern):
        if pattern.startswith("**/", index):
            parts.append("(?:.*/)?")
            index += 3
        elif pattern.startswith("**", index):
            parts.append(".*")
            index += 2
        elif pattern[index] == "*":
            parts.append("[^/]*")
            index += 1
        elif pattern[index] == "?":
            parts.append("[^/]")
            index += 1
        else:
            parts.append(re.escape(pattern[index]))
            index += 1

    return re.compile("".join(parts))


def is_excluded(path: Path, patterns: Sequence[str]) -> bool:
    """Tells whether a glob matches a path from the working directory, or a directory above it."""
    try:
        relative = PurePosixPath(path.resolve().relative_to(Path.cwd().resolve()).as_posix())
    except ValueError:
        relative = PurePosixPath(path.as_posix())

    candidates = [str(relative), *(str(parent) for parent in relative.parents if str(parent) != ".")]

    for pattern in patterns:
        normalized = pattern.rstrip("/").removesuffix("/**")
        regex = _translate_glob(normalized)

        if any(regex.fullmatch(candidate) for candidate in candidates):
            return True

        if "/" not in normalized and any(regex.fullmatch(part) for part in relative.parts):
            return True

    return False


def is_skipped_directory(name: str) -> bool:
    """Tells whether a directory is one a scan skips."""
    return name in SKIPPED_DIRECTORIES or name.endswith(".egg-info")


def is_broad_directory(path: Path) -> bool:
    """Tells whether a directory is a filesystem or drive root or the home directory, too broad to read as a project."""
    directory = Path(os.path.normcase(os.path.abspath(path)))

    try:
        home = Path(os.path.normcase(os.path.abspath(Path.home())))
    except RuntimeError:
        home = None

    return directory == directory.parent or directory == home


def _walk_python_files(root: Path, exclude: Sequence[str]) -> list[Path]:
    """Lists the `.py` files of a tree, without reading a directory the scan skips."""
    found: list[Path] = []

    for directory, subdirectories, names in os.walk(root):
        subdirectories[:] = [name for name in subdirectories if not is_skipped_directory(name)]
        found.extend(Path(directory, name) for name in names if name.endswith(".py"))

    return sorted(path for path in found if not is_excluded(path, exclude))


def collect_files(paths: Sequence[str], exclude: Sequence[str]) -> Iterator[Path]:
    """Lists the Python files the paths name: a file as given, a directory by its `.py` files."""
    for argument in paths:
        path = Path(argument)

        if path.is_dir():
            yield from _walk_python_files(path, exclude)
        elif path.suffix == ".pyi":
            print(f"{path}: skipped, because stub files are out of scope", file=sys.stderr)
        elif path.exists():
            if not is_excluded(path, exclude):
                yield path
        else:
            raise FileNotFoundError(argument)
