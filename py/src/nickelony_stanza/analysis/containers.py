"""Splits a file into containers of sibling units and reads the gap between each pair of neighbors."""

from __future__ import annotations

import ast
from collections.abc import Iterator
from dataclasses import dataclass, replace

from nickelony_stanza.analysis.model import Container, ContainerKind, Gap, LeadComment, Restriction, Unit
from nickelony_stanza.analysis.source import SourceFile
from nickelony_stanza.analysis.syntax import ends_with_definition, is_definition, is_docstring_statement

_BLOCK_FIELDS = ("body", "orelse", "finalbody")

_TRY = (ast.Try, getattr(ast, "TryStar", ast.Try))
_CLAUSE_OWNERS = (ast.If, ast.For, ast.AsyncFor, ast.While)

# `_BLOCK_FIELDS` plus the fields that hold handlers and cases, which a walk reads to reach every block.
_NESTED_FIELDS = (*_BLOCK_FIELDS, "handlers", "cases")


@dataclass(frozen=True)
class _Lines:
    """The comment lines of a gap, whether a deeper comment trails the unit above, and whether the gap is unsafe."""

    comments: set[int]
    trailing_end: int
    unsafe: bool


def _walk_blocks(tree: ast.AST) -> Iterator[ast.AST]:
    """Walks the statement tree in document order, and visits no expression."""
    pending = [tree]

    while pending:
        node = pending.pop()
        yield node

        for field in _NESTED_FIELDS:
            nested = getattr(node, field, None)

            if isinstance(nested, list):
                pending.extend(reversed(nested))


def _first_line(source: SourceFile, node: ast.stmt, floor: int) -> int:
    """Reads the first line of a statement, which for a decorated definition is its first decorator."""
    decorators: list[ast.expr] = getattr(node, "decorator_list", [])

    if not decorators:
        return int(node.lineno)

    for number in range(decorators[0].lineno, floor, -1):
        if source.lines[number - 1].lstrip().startswith("@"):
            return number

    return int(decorators[0].lineno)


def _create_unit(source: SourceFile, node: ast.stmt, floor: int) -> Unit:
    return Unit(node=node, first_line=_first_line(source, node, floor), last_line=node.end_lineno or node.lineno)


def _create_case_unit(case: ast.match_case) -> Unit:
    first = case.pattern.lineno
    last = case.body[-1].end_lineno or case.body[-1].lineno

    return Unit(node=case, first_line=first, last_line=last)


def _read_container_kind(owner: ast.AST, field: str) -> ContainerKind:
    if isinstance(owner, ast.Module):
        return "module"

    if isinstance(owner, ast.ClassDef) and field == "body":
        return "class"

    if isinstance(owner, ast.FunctionDef | ast.AsyncFunctionDef) and field == "body":
        return "function"

    return "block"


def _read_restriction(
    container_kind: ContainerKind, index: int, previous: Unit, following: Unit, body_start: int
) -> Restriction | None:
    if (isinstance(previous.node, ast.stmt) and ends_with_definition(previous.node)) or (
        isinstance(following.node, ast.stmt) and is_definition(following.node)
    ):
        return "definition"

    if previous.is_preamble:
        return "module-header"

    if index == body_start and is_docstring_statement(previous.node):
        if container_kind == "module":
            return "module-header"

        if container_kind == "class":
            return "class-docstring"

        if container_kind == "function":
            return "function-docstring"

    return None


def _read_header_restriction(owner: ast.AST | None, following: Unit, lines: _Lines) -> Restriction | None:
    """
    Reads why the blank line below a block header is not free.

    A formatter writes one above a function or class that opens a block other than a function or class,
    unless a comment comes first.
    """
    if (
        lines.comments
        or not is_definition(following.node)
        or isinstance(owner, ast.FunctionDef | ast.AsyncFunctionDef | ast.ClassDef)
    ):
        return None

    return "definition"


def _classify_lines(source: SourceFile, first: int, last: int, indent: int) -> _Lines:
    """Reads the comment lines of a gap, and the last line a deeper comment holds, which trails the unit above."""
    comments: set[int] = set()
    trailing_end = 0

    for number in range(first, last + 1):
        if source.is_whitespace_line(number):
            continue

        comment = source.comments_by_line.get(number)
        width = source.indent_width(number)

        if comment is None or not comment.standalone or width < indent:
            return _Lines(comments, trailing_end, unsafe=True)

        comments.add(number)

        if width > indent:
            trailing_end = number

    return _Lines(comments, trailing_end, unsafe=False)


def _read_clause_blank_lines(lines: _Lines, previous: Unit, first: int, last: int, following: Unit) -> range:
    """
    Reads the blank lines a clause keyword leaves between its block and the first comment of the gap.

    A comment indented deeper than the keyword trails the block, so the lines count from below it.
    """
    start = max(previous.last_line, lines.trailing_end) + 1
    stop = next((number for number in range(start, last + 1) if number in lines.comments), following.first_line)

    return range(start, stop) if stop > start else range(0)


def _create_gap(
    source: SourceFile, kind: ContainerKind, index: int, units: list[Unit], owner: ast.AST | None = None
) -> Gap:
    previous, following = units[index], units[index + 1]
    body_start = 1 if units[0].is_preamble else 0
    restriction = None if kind == "header" else _read_restriction(kind, index, previous, following, body_start)
    first = previous.last_line + 1
    last = following.first_line - 1

    if following.first_line <= previous.last_line:
        return Gap(
            following.first_line,
            (),
            has_comment=False,
            is_blank=False,
            is_joined=True,
            restriction=restriction,
            blank_lines=range(0),
        )

    lines = _classify_lines(source, first, last, source.indent_width(following.first_line))

    if kind == "header":
        # No block lies above a header, so a deeper comment trails nothing.
        lines = replace(lines, trailing_end=0)
        restriction = _read_header_restriction(owner, following, lines)

    lead_start = following.first_line

    lead: list[LeadComment] = []

    while not lines.unsafe and lead_start - 1 > lines.trailing_end and lead_start - 1 in lines.comments:
        lead_start -= 1
        lead.append(LeadComment(line=lead_start, text=source.comments_by_line[lead_start].text))

    lead.reverse()

    trailing = source.comments_by_line.get(previous.last_line)
    has_comment = bool(lines.comments) or (trailing is not None and not trailing.standalone)
    is_blank = lead_start - 1 >= first and source.is_whitespace_line(lead_start - 1)

    joined = (
        lines.unsafe or source.in_fmt_region(lead_start) or any(source.in_fmt_region(n) for n in range(first, last + 1))
    )

    if kind in ("clause", "header"):
        # A tool directive in the gap keeps it as written, as a preprocessor directive does for the C# rules.
        joined = joined or source.holds_tool_directive(first, last)
        blank_lines = range(0) if joined else _read_clause_blank_lines(lines, previous, first, last, following)
    else:
        deletable = not joined and not lines.comments and last >= first
        blank_lines = range(first, last + 1) if deletable else range(0)

    return Gap(
        insert_line=lead_start,
        lead_comments=tuple(lead),
        has_comment=has_comment,
        is_blank=is_blank and not lines.unsafe,
        is_joined=joined,
        restriction=restriction,
        blank_lines=blank_lines,
    )


def _create_gaps(
    source: SourceFile, kind: ContainerKind, units: list[Unit], owner: ast.AST | None = None
) -> tuple[Gap, ...]:
    return tuple(_create_gap(source, kind, index, units, owner) for index in range(len(units) - 1))


def _create_preamble(source: SourceFile) -> Unit | None:
    last = source.preamble_last_line
    return Unit(node=None, first_line=1, last_line=last, is_preamble=True) if last else None


def _read_units(source: SourceFile, statements: list[ast.stmt], floor: int) -> list[Unit]:
    units: list[Unit] = []

    for node in statements:
        units.append(_create_unit(source, node, units[-1].last_line if units else floor))

    return units


def _read_keyword_line(source: SourceFile, previous: ast.stmt, first: int) -> int | None:
    """Reads the line of the clause keyword between a block and the first line of the clause that follows it."""
    for number in range((previous.end_lineno or previous.lineno) + 1, first + 1):
        comment = source.comments_by_line.get(number)

        if not source.is_whitespace_line(number) and (comment is None or not comment.standalone):
            return number

    return None


def _read_clauses(node: ast.AST) -> list[tuple[list[ast.stmt], int]]:
    """Lists each block that a clause keyword follows, with the first line of that clause."""
    if isinstance(node, _CLAUSE_OWNERS):
        return [(node.body, node.orelse[0].lineno)] if node.orelse else []

    if not isinstance(node, _TRY):
        return []

    blocks: list[tuple[list[ast.stmt], int]] = []
    previous = node.body

    for handler in node.handlers:
        blocks.append((previous, handler.lineno))
        previous = handler.body

    for clause in (node.orelse, node.finalbody):
        if clause:
            blocks.append((previous, clause[0].lineno))
            previous = clause

    return blocks


def _collect_clauses(source: SourceFile, node: ast.AST) -> Iterator[Container]:
    """Reads the gap before each clause keyword, as a container of the last statement above it and the keyword line."""
    for block, first in _read_clauses(node):
        line = _read_keyword_line(source, block[-1], first)

        if line is None:
            continue

        units = [_create_unit(source, block[-1], node.lineno if hasattr(node, "lineno") else 0)]
        units.append(Unit(node=node, first_line=line, last_line=line))

        yield Container(kind="clause", node=node, units=tuple(units), gaps=_create_gaps(source, "clause", units))


def _is_elif(source: SourceFile, node: ast.AST, field: str, statements: list[ast.stmt]) -> bool:
    """Tells whether the `orelse` of an `if` is an `elif`, which is a header of its own and no body."""
    return (
        field == "orelse"
        and isinstance(node, ast.If)
        and len(statements) == 1
        and isinstance(statements[0], ast.If)
        and source.lines[statements[0].lineno - 1].lstrip(" 	").startswith("elif")
    )


def _starts_line(source: SourceFile, unit: Unit) -> bool:
    """Tells whether a statement is the first thing on its line, so a body does not share a line with its header."""
    node = unit.node

    if isinstance(node, ast.match_case):
        text = source.lines[unit.first_line - 1].lstrip(" 	")
        return text.startswith("case") and not (text[4:5].isalnum() or text[4:5] == "_")

    return isinstance(node, ast.stmt) and source.lines[unit.first_line - 1][: node.col_offset].strip(" 	") == ""


def _read_header_end(source: SourceFile, first: int) -> int | None:
    """Reads the last line of a block header, which is the nearest line above the body that is no blank or comment."""
    for number in range(first - 1, 0, -1):
        comment = source.comments_by_line.get(number)

        if not source.is_whitespace_line(number) and (comment is None or not comment.standalone):
            return number

    return None


def _create_header(source: SourceFile, node: ast.AST, following: Unit) -> Container | None:
    """Reads the gap between the last line of a block header and the first unit of its body."""
    end = _read_header_end(source, following.first_line)

    if end is None or not _starts_line(source, following):
        return None

    units = [Unit(node=None, first_line=end, last_line=end), following]

    return Container(kind="header", node=node, units=tuple(units), gaps=_create_gaps(source, "header", units, node))


def _collect_headers(source: SourceFile, node: ast.AST) -> Iterator[Container]:
    """Reads the gap below each block header of a statement, as the header line and the first unit of the body."""
    floor = node.lineno if hasattr(node, "lineno") else 0
    firsts: list[Unit] = []

    for field in _BLOCK_FIELDS:
        statements = getattr(node, field, None)

        if (
            not isinstance(node, ast.Module)
            and isinstance(statements, list)
            and statements
            and isinstance(statements[0], ast.stmt)
            and not _is_elif(source, node, field, statements)
        ):
            firsts.append(_create_unit(source, statements[0], floor))

    if isinstance(node, ast.Match) and node.cases:
        firsts.append(_create_case_unit(node.cases[0]))

    for following in firsts:
        header = _create_header(source, node, following)

        if header is not None:
            yield header


def collect_containers(source: SourceFile, *, clauses: bool = False, headers: bool = False) -> list[Container]:
    """
    Reads every container of a file that holds at least two units, with the gaps between its units.

    With `clauses`, the gap between a block and the keyword that continues its statement is a container of two units.
    With `headers`, so is the gap between the last line of a block header and the first unit of its body.
    """
    containers: list[Container] = []

    for node in _walk_blocks(source.tree):
        for field in _BLOCK_FIELDS:
            statements = getattr(node, field, None)

            if not isinstance(statements, list) or not statements or not isinstance(statements[0], ast.stmt):
                continue

            kind = _read_container_kind(node, field)
            units = _read_units(source, statements, floor=(node.lineno if hasattr(node, "lineno") else 0))

            if kind == "module" and (preamble := _create_preamble(source)) is not None:
                units.insert(0, preamble)

            if len(units) >= 2:
                containers.append(
                    Container(kind=kind, node=node, units=tuple(units), gaps=_create_gaps(source, kind, units))
                )

        if clauses:
            containers.extend(_collect_clauses(source, node))

        if headers:
            containers.extend(_collect_headers(source, node))

        if isinstance(node, ast.Match) and len(node.cases) >= 2:
            case_units = [_create_case_unit(case) for case in node.cases]

            containers.append(
                Container(
                    kind="match",
                    node=node,
                    units=tuple(case_units),
                    gaps=_create_gaps(source, "match", case_units),
                )
            )

    return containers
