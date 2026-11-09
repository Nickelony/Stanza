"""The facts the rules read: units, the gaps between them and the containers that hold them."""

from __future__ import annotations

import ast
from dataclasses import dataclass
from typing import Literal

# A "clause" container holds the two units around a clause keyword: the last statement of a block and the keyword line.
# A "header" container holds the last line of a block header and the first unit of the body below it.
ContainerKind = Literal["module", "class", "function", "block", "match", "clause", "header"]

# The kinds a rule that lays out statements judges, and the kinds a rule that lays out class members judges.
STATEMENT_KINDS = frozenset({"module", "function", "block"})
MEMBER_KINDS = frozenset({"class"})

# The kinds whose units are statements, whether of a body or of a class.
UNIT_KINDS = STATEMENT_KINDS | MEMBER_KINDS

# The kinds of a function or a block inside one, whose body the guard rule judges.
BODY_KINDS = frozenset({"function", "block"})

# Why a gap is not free for the rules that add blank lines:
# "definition": a formatter forces the blank lines beside a function or class.
# "module-header": the preamble and the module docstring, which ZA3017 owns.
# "class-docstring": the blank line a formatter forces below a class docstring, which ZA3013 owns.
# "function-docstring": pydocstyle D202 forbids a blank line below a function docstring.
Restriction = Literal["definition", "module-header", "class-docstring", "function-docstring"]


@dataclass(frozen=True)
class Unit:
    """One sibling of a container: a statement, a case, or the shebang and encoding lines that open a file."""

    node: ast.AST | None
    first_line: int
    last_line: int
    is_preamble: bool = False

    @property
    def is_multi_line(self) -> bool:
        """Tells whether the unit spans more than one line."""
        return self.last_line > self.first_line


@dataclass(frozen=True)
class LeadComment:
    """A standalone comment line that stands directly above a unit and travels with it."""

    line: int
    text: str


@dataclass(frozen=True)
class Gap:
    """The space between two neighboring units, read once so the rules see the same facts."""

    # The line a blank line goes above: the first line of the comments that lead the next unit, or the unit itself.
    insert_line: int
    lead_comments: tuple[LeadComment, ...]

    # True when any comment stands between the two units, a trailing comment on the first unit included.
    has_comment: bool

    # True when a blank line stands between the first unit and the next unit with its leading comments.
    is_blank: bool

    # True when no blank line can stand here: the units share a line, a comment cannot be read, or a formatter is off.
    # A tool directive in a clause gap counts as off too.
    is_joined: bool
    restriction: Restriction | None

    # The lines a rule that removes the gap deletes; empty when the gap holds anything but blank lines.
    # A clause gap deletes the blank lines above its first comment and keeps the ones below it.
    blank_lines: range


@dataclass(frozen=True)
class Container:
    """A list of sibling units, such as the body of a function, with the gaps between them."""

    kind: ContainerKind
    node: ast.AST
    units: tuple[Unit, ...]
    gaps: tuple[Gap, ...]
