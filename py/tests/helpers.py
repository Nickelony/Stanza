"""Shared helpers: fixing text with chosen rules, formatting with Black and Ruff, and mutating a file's blank lines."""

from __future__ import annotations

import ast
import io
import random
import subprocess
import sys
import tokenize
from collections.abc import Iterable
from dataclasses import dataclass, replace
from pathlib import Path

from nickelony_stanza import Config, Options, fix, lint
from nickelony_stanza.engine import Finding
from nickelony_stanza.rules import ALL_RULES, CALL_RULES

CORPUS = Path(__file__).parent / "corpus"
SCRIPTS = Path(sys.executable).parent


OPT_IN = [rule.code for rule in ALL_RULES if rule.level == "opt-in"]

# The levels, and the strong level with every opt-in rule in three ways:
# with the tight half of each pair, with the separating half of the clause pair, and with both halves of both pairs.
CONFIGS = {
    "minimal": Config.create("minimal"),
    "moderate": Config.create("moderate"),
    "strong": Config.create("strong"),
    "everything": Config.create("strong", enable=[code for code in OPT_IN if code != "ZA3201"]),
    "everything separating": Config.create("strong", enable=OPT_IN),
    "everything both halves": Config.create("strong", enable=[*OPT_IN, "ZA3111", "ZA3202"]),
}


def without_call_rules(config: Config) -> Config:
    """Leaves the call rules out of a configuration, for a check that a formatter keeps every line as the fix wrote it."""
    return replace(config, rules=tuple(rule for rule in config.rules if rule not in CALL_RULES))


def config_for(rules: Iterable[str], options: Options | None = None) -> Config:
    """Builds a configuration that enables exactly the given rules."""
    return Config.create("none", enable=list(rules), options=options)


def fix_with(text: str, *rules: str, options: Options | None = None) -> str:
    """Fixes a text with exactly the given rules."""
    return fix(text, config_for(rules, options)).text


def findings_with(text: str, *rules: str, options: Options | None = None) -> list[Finding]:
    """Lists the findings of exactly the given rules."""
    return lint(text, config_for(rules, options))


def run_formatter(tool: str, text: str) -> str | None:
    """Formats a text with Black or Ruff, or returns None when the tool refuses it."""
    if tool == "black":
        command = [str(SCRIPTS / "black"), "-q", "--line-length", "88", "-"]
    else:
        command = [str(SCRIPTS / "ruff"), "format", "--isolated", "--line-length", "88", "-"]

    result = subprocess.run(command, input=text.encode(), capture_output=True, check=False)
    return result.stdout.decode() if result.returncode == 0 else None


def _same_node(before: object, after: object) -> bool:
    """Compares two nodes, letting a call of the second move a tail of positional arguments into leading keywords."""
    if isinstance(before, list) and isinstance(after, list):
        return len(before) == len(after) and all(
            _same_node(one, other) for one, other in zip(before, after, strict=True)
        )

    if not isinstance(before, ast.AST) or type(before) is not type(after):
        # `1 == True` and `1 == 1.0`, so a value compares by its type too, as `ast.dump` does.
        return type(before) is type(after) and before == after

    if isinstance(before, ast.Call) and isinstance(after, ast.Call):
        kept = len(after.args)
        moved = len(before.args) - kept
        named = after.keywords[: max(moved, 0)]

        if moved < 0 or len(after.keywords) != moved + len(before.keywords) or any(k.arg is None for k in named):
            return False

        values = [keyword.value for keyword in named]

        return _same_node(
            [before.func, *before.args, *before.keywords], [after.func, *after.args, *values, *after.keywords[moved:]]
        )

    return all(_same_node(getattr(before, name, None), getattr(after, name, None)) for name in before._fields)


def same_tree(first: str, second: str, config: Config | None = None) -> bool:
    """
    Tells whether two texts parse to the same syntax tree.

    Under a configuration that enables a call rule, a call of the second text may pass a tail of its positional
    arguments by name instead, which is the one change those rules make.
    """
    before, after = ast.parse(first), ast.parse(second)

    if config is None or not any(rule in CALL_RULES for rule in config.rules):
        return ast.dump(before) == ast.dump(after)

    return _same_node(before, after)


def corpus_files() -> list[Path]:
    """Lists the corpus files."""
    return sorted(CORPUS.glob("*.py"))


def _string_lines(text: str) -> set[int]:
    """Collects the lines inside a multi-line string token, which a mutation must not touch."""
    inside: set[int] = set()
    starts: list[int] = []

    fstring_start = getattr(tokenize, "FSTRING_START", -1)
    fstring_end = getattr(tokenize, "FSTRING_END", -1)

    for token in tokenize.generate_tokens(io.StringIO(text).readline):
        if token.type == tokenize.STRING:
            inside.update(range(token.start[0] + 1, token.end[0]))
        elif token.type == fstring_start:
            starts.append(token.start[0])
        elif token.type == fstring_end:
            inside.update(range(starts.pop() + 1, token.end[0]))

    return inside


def _logical_line_ends(text: str) -> list[int]:
    return [
        token.end[0] for token in tokenize.generate_tokens(io.StringIO(text).readline) if token.type == tokenize.NEWLINE
    ]


def drop_blank_lines(text: str, seed: int) -> str:
    """Drops about half of the blank lines outside strings."""
    generator = random.Random(seed)
    protected = _string_lines(text)
    lines = text.split("\n")

    return "\n".join(
        line
        for number, line in enumerate(lines, start=1)
        if line.strip() or number in protected or generator.random() < 0.5
    )


def add_blank_lines(text: str, seed: int) -> str:
    """Adds a blank line after about a fifth of the logical lines."""
    generator = random.Random(seed)
    ends = {number for number in _logical_line_ends(text) if generator.random() < 0.2}
    lines = text.split("\n")

    result: list[str] = []

    for number, line in enumerate(lines, start=1):
        result.append(line)

        if number in ends:
            result.append("")

    return "\n".join(result)


@dataclass(frozen=True)
class Case:
    """A text a rule fixes, or leaves alone when `after` is None."""

    id: str
    rules: tuple[str, ...]
    before: str
    after: str | None = None
    options: Options | None = None


def case(id: str, rule: str, before: str, after: str | None = None, options: Options | None = None) -> Case:
    """Builds a case of one rule."""
    return Case(id, (rule,), before, after, options)


def check_case(item: Case) -> None:
    """Checks that the rules write the text after, and that the text after is clean."""
    expected = item.before if item.after is None else item.after

    assert fix_with(item.before, *item.rules, options=item.options) == expected
    assert fix_with(expected, *item.rules, options=item.options) == expected
