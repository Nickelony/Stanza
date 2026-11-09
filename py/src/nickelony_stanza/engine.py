"""Finds the gaps and the calls the enabled rules ask about, applies their fixes and checks the tree they write."""

from __future__ import annotations

import ast
from collections.abc import Sequence
from dataclasses import dataclass
from functools import cache
from pathlib import Path

from nickelony_stanza.analysis.calls import ArgumentNames, CallCheck, NamedCall, find_named_calls
from nickelony_stanza.analysis.projects import STDIN_PATH, Projects
from nickelony_stanza.analysis.session import OwnedGap, analyze
from nickelony_stanza.analysis.source import SourceFile, parse
from nickelony_stanza.config import Config
from nickelony_stanza.noqa import Noqa
from nickelony_stanza.rules import CALL_RULES, KEEP_ORDER, OWNER_ORDER, PAIR_RULE
from nickelony_stanza.rules.base import Rule

MAX_PASSES = 10


class FixError(Exception):
    """A fix that would change what the program means, or that never settles."""


@dataclass(frozen=True)
class Edit:
    """A change of whole lines: delete some lines, then insert text before the first of them."""

    line: int
    delete: int
    insert: str


@dataclass(frozen=True)
class Insertion:
    """Text written into a line before the character at a 0-based column."""

    line: int
    column: int
    text: str


@dataclass(frozen=True)
class Finding:
    """A gap or a call a rule asks to change, with the edits that change it and the argument names they write."""

    line: int
    column: int
    rule: Rule
    message: str
    edits: tuple[Edit | Insertion, ...]
    naming: ArgumentNames | None = None

    @property
    def code(self) -> str:
        """The code of the rule that reports the finding."""
        return self.rule.code


@dataclass(frozen=True)
class FixResult:
    """The text after the fixes, and how many passes it took."""

    text: str
    passes: int


@cache
def default_projects(roots: tuple[str, ...]) -> Projects:
    """The indexes of a caller that passes none, kept for the process so the standard library is parsed once."""
    return Projects(roots)


def _call_checks(config: Config) -> list[tuple[str, CallCheck]]:
    codes = {rule.code for rule in config.rules}
    return [(rule.code, rule.check) for rule in CALL_RULES if rule.code in codes and rule.check is not None]


def _open_source(
    text: str, tree: ast.Module | None, config: Config, path: Path | None, projects: Projects | None
) -> SourceFile:
    """
    Reads a text, taking the tree the index holds for the same text of the same file instead of parsing it again.

    The call rules read the file through that tree, so the file keeps one tree and one scope table.
    """
    if tree is None and path is not None and _call_checks(config):
        projects = projects or default_projects(config.options.source_roots)
        held = projects.index_for(path).held_module(path, text)
        tree = None if held is None else held.tree

    return SourceFile(text, tree=tree)


def _create_gap_finding(source: SourceFile, rule: Rule, owned: OwnedGap) -> Finding:
    gap = owned.gap
    column = source.indent_width(owned.following.first_line) + 1

    if rule.role != "separate":
        edit = Edit(line=gap.blank_lines.start, delete=len(gap.blank_lines), insert="")
        return Finding(gap.blank_lines.start, column, rule, rule.message, (edit,))

    edit = Edit(line=gap.insert_line, delete=0, insert=source.eol_before(gap.insert_line))
    return Finding(gap.insert_line, column, rule, rule.message, (edit,))


def _create_call_finding(rule: Rule, named: NamedCall) -> Finding:
    line, column = named.opening
    names = named.naming.names

    edits = tuple(
        Insertion(anchor_line, anchor_column, f"{name}=")
        for (anchor_line, anchor_column), name in zip(named.anchors, names, strict=True)
    )

    return Finding(line, column + 1, rule, rule.message, edits, named.naming)


def _find_call_findings(
    source: SourceFile, config: Config, noqa: Noqa, path: Path | None, projects: Projects | None
) -> list[Finding]:
    checks = _call_checks(config)

    if not checks:
        return []

    rules = {rule.code: rule for rule in config.rules}
    projects = projects or default_projects(config.options.source_roots)
    index = projects.index_for(path)

    findings: list[Finding] = []

    for named in find_named_calls(source, checks, config.options, path or STDIN_PATH, index):
        call = named.naming.call

        if not noqa.silences(named.code, named.opening[0], call.lineno, named.statement_line):
            findings.append(_create_call_finding(rules[named.code], named))

    return findings


def find_findings(
    source: SourceFile, config: Config, *, path: Path | None = None, projects: Projects | None = None
) -> list[Finding]:
    """Reads the file once and lists a finding for each gap and call an enabled rule owns, silenced ones left out."""
    noqa = Noqa.read(source)
    rules = {rule.code: rule for rule in config.rules}
    enabled = [rule for rule in (*OWNER_ORDER, *KEEP_ORDER) if rule.code in rules]

    findings: list[Finding] = []

    gaps_by_code = analyze(
        source,
        enabled,
        config.options,
        pair=PAIR_RULE if PAIR_RULE.code in rules else None,
    )

    for code, gaps in gaps_by_code.items():
        for owned in gaps:
            finding = _create_gap_finding(source, rules[code], owned)

            if not noqa.silences(code, finding.line, owned.following.first_line):
                findings.append(finding)

    findings.extend(_find_call_findings(source, config, noqa, path, projects))

    return sorted(findings, key=lambda finding: (finding.line, finding.code))


def lint(
    text: str,
    config: Config,
    *,
    tree: ast.Module | None = None,
    path: Path | None = None,
    projects: Projects | None = None,
) -> list[Finding]:
    """
    Lists the findings of a text, which must parse, and takes the tree of a caller that has already parsed it.

    The call rules resolve a callee against the project of `path`, and a text without a path against itself alone.
    """
    return find_findings(_open_source(text, tree, config, path, projects), config, path=path, projects=projects)


def _position(edit: Edit | Insertion) -> tuple[int, int]:
    """Orders the edits of a line so an insertion goes in before a line edit shifts the lines below it."""
    return (edit.line, edit.column) if isinstance(edit, Insertion) else (edit.line, -1)


def apply_edits(source: SourceFile, findings: Sequence[Finding]) -> str:
    """Applies the edits of the findings, last place first so the line numbers and the columns stay true."""
    lines = list(source.lines)
    edits = [edit for finding in findings for edit in finding.edits]

    deleted = {
        number for edit in edits if isinstance(edit, Edit) for number in range(edit.line, edit.line + edit.delete)
    }

    for edit in sorted(edits, key=_position, reverse=True):
        if isinstance(edit, Insertion):
            if edit.line in deleted:
                raise FixError(f"An insertion on line {edit.line} overlaps a line another fix deletes.")

            line = lines[edit.line - 1]
            lines[edit.line - 1] = line[: edit.column] + edit.text + line[edit.column :]
        else:
            lines[edit.line - 1 : edit.line - 1 + edit.delete] = [edit.insert] if edit.insert else []

    return "".join(lines)


def dump_expected_tree(tree: ast.Module, namings: Sequence[ArgumentNames]) -> str:
    """
    Dumps the tree a fix must write: the tree it read, with each named argument moved into a keyword of its call.

    The calls are changed in place for the dump and restored after it, because the index may hold the same tree.
    """
    saved = [(naming.call, naming.call.args, naming.call.keywords) for naming in namings]

    try:
        for naming in namings:
            call = naming.call
            moved = call.args[naming.start :]
            named = [ast.keyword(arg=name, value=value) for name, value in zip(naming.names, moved, strict=True)]

            call.args, call.keywords = call.args[: naming.start], [*named, *call.keywords]

        return ast.dump(tree)
    finally:
        for call, args, keywords in saved:
            call.args, call.keywords = args, keywords


def _check_tree(source: SourceFile, findings: Sequence[Finding], text: str) -> ast.Module:
    """Parses the fixed text, and refuses it unless its tree is the one the findings promise."""
    try:
        tree = parse(text)
    except SyntaxError as error:
        raise FixError(f"A fix would write text that does not parse: {error}") from error

    namings = [finding.naming for finding in findings if finding.naming is not None]

    try:
        same = dump_expected_tree(source.tree, namings) == ast.dump(tree)
    except RecursionError as error:
        raise FixError("A fix wrote a tree too deeply nested to check, so the file is left as it was.") from error

    if not same:
        raise FixError("A fix would change the syntax tree.")

    return tree


def fix(
    text: str,
    config: Config,
    *,
    verify: bool = True,
    path: Path | None = None,
    projects: Projects | None = None,
) -> FixResult:
    """
    Applies the fixes until no finding is left, checking after each pass that the tree is the one the fixes promise.

    A blank line keeps the tree as it was, and a name moves its argument into a keyword of the same call.
    """
    current = text
    tree: ast.Module | None = None
    passes = 0

    while passes < MAX_PASSES:
        source = _open_source(current, tree, config, path, projects)
        findings = find_findings(source, config, path=path, projects=projects)

        if not findings:
            break

        current = apply_edits(source, findings)
        tree = _check_tree(source, findings, current) if verify else None

        passes += 1

    # The loop ends on its limit, not on a clean pass, so the last pass may have been the one that settled the text.
    if passes == MAX_PASSES and find_findings(SourceFile(current, tree=tree), config, path=path, projects=projects):
        raise FixError(f"The fixes did not settle in {MAX_PASSES} passes.")

    return FixResult(current, passes)
