"""
A flake8 plugin that reports the same findings as `stanza-py check`, for editors that run flake8.

Each flake8 worker process builds its own indexes and keeps them for every file it checks.
flake8 names a file read from standard input `stdin`, unless `--stdin-display-name` gives its path,
so such a text resolves its calls against its own definitions, the standard library and the installed packages alone.
A project past the caps of the project scan leaves every call unnamed, and the plugin reports no note of it,
because flake8 shows findings alone.
"""

from __future__ import annotations

import ast
from collections.abc import Iterator
from functools import cache
from pathlib import Path
from typing import Any, cast

from nickelony_stanza import __version__
from nickelony_stanza.config import DEFAULT_LEVEL, Config, load_config
from nickelony_stanza.engine import lint
from nickelony_stanza.rules.base import LEVELS, Level

# The names flake8 gives a text it reads from standard input.
_STDIN_NAMES = frozenset({"stdin", "-", ""})


@cache
def _load_default_config() -> Config:
    config, _ = load_config(None, Path.cwd())
    return config


class StanzaChecker:
    """Reports the blank lines and the argument names the rules ask for, with the codes `ZA3001` to `ZA3999`."""

    name = "nickelony-stanza"
    version = __version__

    level: Level | None = None

    def __init__(self, tree: ast.AST, lines: list[str], filename: str = "stdin") -> None:
        self._tree = tree
        self._lines = lines
        self._path = None if filename in _STDIN_NAMES else Path(filename)

    @classmethod
    def add_options(cls, option_manager: Any) -> None:
        """Adds `--stanza-level`, which replaces the level of `[tool.stanza]`."""
        option_manager.add_option(
            "--stanza-level",
            default=None,
            parse_from_config=True,
            choices=list(LEVELS),
            help=f"The Stanza preset to report. The default is the level of [tool.stanza], else {DEFAULT_LEVEL}.",
        )

    @classmethod
    def parse_options(cls, options: Any) -> None:
        """Reads the level from the flake8 options."""
        cls.level = options.stanza_level

    def run(self) -> Iterator[tuple[int, int, str, type[StanzaChecker]]]:
        """Yields one flake8 result for each finding of the file."""
        config = _load_default_config().override(self.level, (), ())
        tree = cast("ast.Module", self._tree)

        for finding in lint("".join(self._lines), config, tree=tree, path=self._path):
            yield finding.line, finding.column - 1, f"{finding.code} {finding.message}", type(self)
