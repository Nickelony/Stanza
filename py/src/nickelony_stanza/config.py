"""Reads which rules a run enables and with which options, from `[tool.stanza]` in `pyproject.toml`."""

from __future__ import annotations

from collections.abc import Iterable
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, TypeVar, cast, get_args

from nickelony_stanza.formatter import DEFAULT_FORMATTER, FORMATTERS, FormatterName
from nickelony_stanza.options import (
    DEFAULT_MAX_LOOSE_RUN_SIZE,
    DEFAULT_NAMED_ARGUMENT_MIN_ARGUMENTS,
    MAX_LOOSE_RUN_SIZE_LIMIT,
    NAMED_ARGUMENT_MIN_ARGUMENTS_LIMIT,
    NAMED_ARGUMENT_SOURCES,
    NamedArgumentLiterals,
    NamedArgumentMatch,
    NamedArgumentMethods,
    Options,
)
from nickelony_stanza.paths import is_broad_directory
from nickelony_stanza.rules import ALL_RULES, BY_CODE, resolve_rule, rules_of_level
from nickelony_stanza.rules.base import LEVELS, Level, Rule
from nickelony_stanza.tomlreader import TOMLDecodeError, read_tool_table

DEFAULT_LEVEL: Level = "moderate"

_KEYS = {
    "level",
    "enable",
    "disable",
    "exclude",
    "formatter",
    "assertion-roots",
    "guard-roots",
    "subscription-roots",
    "max-loose-run-size",
    "named-argument-min-arguments",
    "named-argument-match",
    "named-argument-literals",
    "named-argument-sources",
    "named-argument-methods",
    "named-argument-ignore",
    "source-roots",
}

_Word = TypeVar("_Word", bound=str)


class ConfigError(Exception):
    """A configuration the tool cannot read."""


@dataclass(frozen=True)
class Config:
    """The rules a run enables, their options, the paths it skips and the formatter `format` runs first."""

    rules: tuple[Rule, ...]
    options: Options = field(default_factory=Options)
    exclude: tuple[str, ...] = ()
    level: Level = DEFAULT_LEVEL
    enable: tuple[str, ...] = ()
    disable: tuple[str, ...] = ()
    formatter: FormatterName = DEFAULT_FORMATTER

    @classmethod
    def create(
        cls,
        level: Level = DEFAULT_LEVEL,
        *,
        enable: Iterable[str] = (),
        disable: Iterable[str] = (),
        options: Options | None = None,
        exclude: Iterable[str] = (),
        formatter: FormatterName = DEFAULT_FORMATTER,
    ) -> Config:
        """Builds a configuration: the rules of a level, plus the rules enabled, minus the rules disabled."""
        if level not in LEVELS:
            raise ConfigError(f"Unknown level '{level}'. Choose one of: {', '.join(LEVELS)}.")

        if formatter not in FORMATTERS:
            raise ConfigError(f"Unknown formatter '{formatter}'. Choose one of: {', '.join(FORMATTERS)}.")

        enabled, disabled = tuple(enable), tuple(disable)

        try:
            codes = {rule.code for rule in rules_of_level(level)}
            named = {resolve_rule(identifier).code for identifier in enabled}

            # A rule named on its own replaces the opposite rule the level enables; naming both keeps both.
            codes -= {opposite for code in named if (opposite := BY_CODE[code].opposite) and opposite not in named}
            codes |= named
            codes -= {resolve_rule(identifier).code for identifier in disabled}
        except KeyError as error:
            raise ConfigError(f"Unknown rule '{error.args[0]}'.") from error

        return cls(
            rules=tuple(rule for rule in ALL_RULES if rule.code in codes),
            options=options or Options(),
            exclude=tuple(exclude),
            formatter=formatter,
            level=level,
            enable=enabled,
            disable=disabled,
        )

    def override(self, level: Level | None, enable: Iterable[str], disable: Iterable[str]) -> Config:
        """Returns the configuration with the choices of the command line added on top."""
        return Config.create(
            level or self.level,
            enable=[*self.enable, *enable],
            disable=[*self.disable, *disable],
            options=self.options,
            exclude=self.exclude,
            formatter=self.formatter,
        )


def _read_strings(table: dict[str, Any], key: str, default: tuple[str, ...] = ()) -> tuple[str, ...]:
    value = table.get(key, list(default))

    if not isinstance(value, list) or not all(isinstance(item, str) for item in value):
        raise ConfigError(f"'{key}' in [tool.stanza] must be a list of strings.")

    return tuple(cast("list[str]", value))


def _read_bounded(table: dict[str, Any], key: str, default: int, limit: int) -> int:
    """Reads a whole number from 0 to `limit`."""
    value = table.get(key, default)

    if isinstance(value, bool) or not isinstance(value, int) or not 0 <= value <= limit:
        raise ConfigError(f"'{key}' in [tool.stanza] must be a number from 0 to {limit}.")

    return value


def _read_word(table: dict[str, Any], key: str, words: tuple[_Word, ...], default: _Word) -> _Word:
    value = table.get(key, default)

    if value not in words:
        raise ConfigError(f"'{key}' in [tool.stanza] must be one of: {', '.join(words)}.")

    return cast("_Word", value)


def _read_sources(table: dict[str, Any], key: str, default: tuple[str, ...]) -> tuple[str, ...]:
    sources = _read_strings(table, key, default)

    if any(source not in NAMED_ARGUMENT_SOURCES for source in sources):
        raise ConfigError(f"'{key}' in [tool.stanza] may name only: {', '.join(NAMED_ARGUMENT_SOURCES)}.")

    return sources


def _read_roots(table: dict[str, Any], key: str, base: Path) -> tuple[str, ...]:
    """
    Reads the source roots, each relative to the directory of the configuration file, as absolute paths.

    A path that is no directory is refused, because no call could resolve against it, and so are a filesystem
    or drive root and the home directory, because the resolver reads every file below a root.
    """
    roots = tuple(str((base / root).resolve()) for root in _read_strings(table, key))

    for root in roots:
        if not Path(root).is_dir():
            raise ConfigError(f"'{key}' in [tool.stanza] names {root}, which is not a directory.")

        if is_broad_directory(Path(root)):
            raise ConfigError(f"'{key}' in [tool.stanza] names {root}, a filesystem root or the home directory.")

    return roots


def config_from_table(table: dict[str, Any], base: Path | None = None) -> Config:
    """Builds a configuration from the `[tool.stanza]` table, reading a relative path from `base`, else the cwd."""
    unknown = sorted(set(table) - _KEYS)

    if unknown:
        raise ConfigError(f"Unknown key in [tool.stanza]: {', '.join(unknown)}.")

    level = table.get("level", DEFAULT_LEVEL)

    if not isinstance(level, str):
        raise ConfigError("'level' in [tool.stanza] must be a string.")

    defaults = Options()

    options = Options(
        assertion_roots=_read_strings(table, "assertion-roots", defaults.assertion_roots),
        guard_roots=_read_strings(table, "guard-roots", defaults.guard_roots),
        subscription_roots=_read_strings(table, "subscription-roots", defaults.subscription_roots),
        max_loose_run_size=_read_bounded(
            table, "max-loose-run-size", DEFAULT_MAX_LOOSE_RUN_SIZE, MAX_LOOSE_RUN_SIZE_LIMIT
        ),
        named_argument_min_arguments=_read_bounded(
            table,
            "named-argument-min-arguments",
            DEFAULT_NAMED_ARGUMENT_MIN_ARGUMENTS,
            NAMED_ARGUMENT_MIN_ARGUMENTS_LIMIT,
        ),
        named_argument_match=_read_word(
            table, "named-argument-match", get_args(NamedArgumentMatch), defaults.named_argument_match
        ),
        named_argument_literals=_read_word(
            table, "named-argument-literals", get_args(NamedArgumentLiterals), defaults.named_argument_literals
        ),
        named_argument_sources=_read_sources(table, "named-argument-sources", defaults.named_argument_sources),
        named_argument_methods=_read_word(
            table, "named-argument-methods", get_args(NamedArgumentMethods), defaults.named_argument_methods
        ),
        named_argument_ignore=_read_strings(table, "named-argument-ignore"),
        source_roots=_read_roots(table, "source-roots", base or Path.cwd()),
    )

    formatter = table.get("formatter", DEFAULT_FORMATTER)

    if not isinstance(formatter, str):
        raise ConfigError("'formatter' in [tool.stanza] must be a string.")

    return Config.create(
        cast("Level", level),
        enable=_read_strings(table, "enable"),
        disable=_read_strings(table, "disable"),
        options=options,
        exclude=_read_strings(table, "exclude"),
        formatter=cast("FormatterName", formatter),
    )


def _read_tool_table(path: Path) -> dict[str, Any]:
    try:
        return read_tool_table(path)
    except (OSError, TOMLDecodeError) as error:
        raise ConfigError(f"Cannot read {path}: {error}") from error


def find_pyproject(start: Path) -> Path | None:
    """Finds the nearest `pyproject.toml` with a `[tool.stanza]` table, from a directory upward."""
    for directory in (start, *start.parents):
        candidate = directory / "pyproject.toml"

        if candidate.is_file() and "stanza" in _read_tool_table(candidate):
            return candidate

    return None


def load_config(path: Path | None, start: Path) -> tuple[Config, Path | None]:
    """Loads the configuration a run uses: the given file, else the nearest `pyproject.toml` with a stanza table."""
    pyproject = path or find_pyproject(start.resolve())

    if pyproject is None:
        return Config.create(), None

    table = _read_tool_table(pyproject).get("stanza")

    if table is None:
        return Config.create(), pyproject

    if not isinstance(table, dict):
        raise ConfigError("[tool.stanza] must be a table.")

    return config_from_table(cast("dict[str, Any]", table), pyproject.resolve().parent), pyproject
