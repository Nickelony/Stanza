"""
Finds the calls of a file, the arguments a call rule names, and the place in the text where each name goes.

A call rule names the positional arguments of a call from one argument on, because Python allows no positional argument
after a keyword, and `binding.py` settles which parameter each argument fills.

- `ast` columns count the UTF-8 bytes of a line and a fix writes at character columns, so each position is converted.
- A name goes before the first token of its argument, after the `(` or `,` in front of it,
  so a parenthesized argument such as `(640)` takes its name outside the parentheses.
- A call is left alone inside a `# fmt: off` region, or on a line a `# fmt: skip` comment trails,
  because the formatter is told to keep that text as written,
  and inside an f-string before Python 3.12, whose tokenizer reads the whole f-string as one token.
- A call inside a self-documenting field of an f-string, as in `f"{call(1)=}"`, is left alone,
  because the field prints the text of its expression and a name would change what the program prints.
- A call inside a template string (Python 3.14 and later) is left alone, because each interpolation keeps its own text.
- An argument that is a generator or a `:=` expression without parentheses of its own cannot take a name,
  so its call is left alone.
- `named-argument-ignore` takes a call away from both rules before either judges it.
  A `# noqa` comment silences only the report of the rule that owns the call, so the other rule never takes it over.
"""

from __future__ import annotations

import ast
import re
import sys
import tokenize
from bisect import bisect_left
from collections.abc import Callable, Sequence
from dataclasses import dataclass
from fnmatch import fnmatchcase
from pathlib import Path
from typing import cast

from nickelony_stanza.analysis.binding import (
    Binding,
    FileResolver,
    ModuleContext,
    ResolveOptions,
    Skipped,
    SourceIndex,
)
from nickelony_stanza.analysis.scopes import INERT_KINDS
from nickelony_stanza.analysis.source import SourceFile
from nickelony_stanza.options import NAMED_ARGUMENT_SOURCES, Options

# The tokens between two arguments that carry no code.
_SKIPPED_TOKENS = frozenset({tokenize.NL, tokenize.NEWLINE, tokenize.COMMENT, tokenize.INDENT, tokenize.DEDENT})

# An f-string holds tokens of its own from Python 3.12 on.
_READS_FSTRING_TOKENS = sys.version_info >= (3, 12)

# A template string, from Python 3.14 on.
_TEMPLATE_STRING: type[ast.AST] | None = getattr(ast, "TemplateStr", None)

Position = tuple[int, int]


@dataclass(frozen=True)
class ArgumentNames:
    """The names a fix writes before the positional arguments of a call, one for each argument from `start` on."""

    call: ast.Call
    start: int
    names: tuple[str, ...]


@dataclass(frozen=True)
class NamedCall:
    """A call a rule owns: the names it writes, where its argument list opens and where each name goes."""

    code: str
    naming: ArgumentNames
    statement_line: int
    opening: Position
    anchors: tuple[Position, ...]


# The binding of a call, resolved on the first request, or None when the call is left alone.
Resolve = Callable[[], Binding | None]

# Plans the names a rule writes into a call, or None when the rule asks for none.
CallCheck = Callable[[ast.Call, Resolve, Options], ArgumentNames | None]


def read_names(binding: Binding, call: ast.Call) -> tuple[str | None, ...]:
    """Lists the name each positional argument can take, with no name for a receiver the call passes itself."""
    names = binding.names_for(len(call.args))
    return (None, *names[1:]) if binding.receiver and names else names


def build_names(call: ast.Call, names: tuple[str | None, ...], start: int) -> ArgumentNames | None:
    """Names the arguments from `start` on, or None when one of them cannot take a name."""
    tail = names[start:]

    if not tail or any(name is None for name in tail):
        return None

    return ArgumentNames(call, start, cast("tuple[str, ...]", tail))


def count_arguments(call: ast.Call) -> int:
    """Counts the arguments a call writes, named ones included."""
    return len(call.args) + len(call.keywords)


class _Tokens:
    """The tokens of a file, to find where an argument list opens and where each argument starts."""

    def __init__(self, source: SourceFile) -> None:
        lines = iter(source.lines)

        self._lines = source.lines
        self._tokens = list(tokenize.generate_tokens(lambda: next(lines, "")))
        self._starts = [token.start for token in self._tokens]

    def to_position(self, line: int, byte_column: int) -> Position:
        """Converts an `ast` position, which counts UTF-8 bytes, to the character column of its line."""
        encoded = self._lines[line - 1].encode("utf-8", "surrogatepass")
        return line, len(encoded[:byte_column].decode("utf-8", "surrogatepass"))

    def _find_after(self, position: Position, delimiter: str) -> int | None:
        """Finds the token of the delimiter that follows a position past closing parentheses, or None."""
        index = bisect_left(self._starts, position)

        while index < len(self._tokens):
            token = self._tokens[index]

            if token.type == tokenize.OP and token.string == delimiter:
                return index

            if token.type not in _SKIPPED_TOKENS and token.string != ")":
                return None

            index += 1

        return None

    def _first_after(self, index: int) -> int | None:
        index += 1

        while index < len(self._tokens) and self._tokens[index].type in _SKIPPED_TOKENS:
            index += 1

        return index if index < len(self._tokens) else None

    def _end_of(self, node: ast.expr) -> Position:
        assert node.end_lineno is not None
        assert node.end_col_offset is not None

        return self.to_position(node.end_lineno, node.end_col_offset)

    def find_opening(self, call: ast.Call) -> Position | None:
        """Finds the `(` that opens the argument list of a call."""
        index = self._find_after(self._end_of(call.func), "(")
        return None if index is None else self._tokens[index].start

    def find_argument(self, call: ast.Call, index: int) -> Position | None:
        """Finds the first token of a positional argument, or None when a name cannot stand in front of it."""
        if index == 0:
            after, delimiter = self._end_of(call.func), "("
        else:
            after, delimiter = self._end_of(call.args[index - 1]), ","

        found = self._find_after(after, delimiter)
        first = None if found is None else self._first_after(found)

        if first is None:
            return None

        token = self._tokens[first]
        argument = call.args[index]

        if token.start > self.to_position(argument.lineno, argument.col_offset):
            return None

        # A generator or a `:=` expression takes a name only inside parentheses of its own.
        if isinstance(argument, ast.GeneratorExp | ast.NamedExpr) and token.string != "(":
            return None

        return token.start


def _read_segment(lines: Sequence[str], node: ast.expr) -> str | None:
    """Reads the text of a node from the lines of its file, as `ast.get_source_segment` does but without a split."""
    if node.end_lineno is None or node.end_col_offset is None:
        return None

    first, last = node.lineno - 1, node.end_lineno - 1

    if first == last:
        return lines[first].encode()[node.col_offset : node.end_col_offset].decode()

    head = lines[first].encode()[node.col_offset :].decode()
    tail = lines[last].encode()[: node.end_col_offset].decode()

    return "".join([head, *lines[first + 1 : last], tail])


def _is_self_documenting(field: ast.FormattedValue, lines: Sequence[str]) -> bool:
    """Tells whether an f-string field ends its expression with `=`, which prints the expression's own text."""
    whole = _read_segment(lines, field)
    expression = _read_segment(lines, field.value)

    if whole is None or expression is None:
        return True

    # What follows the expression is closing parentheses, blanks and comments, then `=` or the end of the field.
    # A comment runs to its line break, so an `=` inside it is not the one that ends the expression.
    start = whole.find(expression)
    rest = whole[start + len(expression) :] if start >= 0 else "="

    return re.match(r"(?:[\s)]|#[^\n]*(?=\n))*=", rest) is not None


_COMPOUND_STATEMENTS = (
    ast.If,
    ast.For,
    ast.AsyncFor,
    ast.While,
    ast.With,
    ast.AsyncWith,
    ast.Try,
    ast.FunctionDef,
    ast.AsyncFunctionDef,
    ast.ClassDef,
    ast.Match,
    *((ast.TryStar,) if sys.version_info >= (3, 11) else ()),
)


# The nodes that hold no call and no child that could, so the walk never stacks them.
_LEAVES = INERT_KINDS | {ast.Name, ast.Constant}


def collect_call_sites(source: SourceFile) -> list[tuple[ast.Call, int]]:
    """Lists the calls of a file in source order, each with the first line of the statement that holds it."""
    sites: list[tuple[ast.Call, int]] = []
    skipped = source.fmt_skip_lines
    stack: list[tuple[ast.AST, int, bool]] = [(node, node.lineno, False) for node in reversed(source.tree.body)]

    while stack:
        node, line, kept = stack.pop()

        if isinstance(node, ast.stmt):
            line = node.lineno

            # A formatter keeps a simple statement whose last line carries `# fmt: skip`, so its calls stay as written.
            kept = kept or (not isinstance(node, _COMPOUND_STATEMENTS) and node.end_lineno in skipped)

        if isinstance(node, ast.JoinedStr) and not _READS_FSTRING_TOKENS:
            continue

        # A template string keeps the text of each interpolation, which a name would change.
        if _TEMPLATE_STRING is not None and isinstance(node, _TEMPLATE_STRING):
            continue

        if isinstance(node, ast.FormattedValue) and _is_self_documenting(node, source.lines):
            stack.extend((child, line, kept) for child in [node.format_spec] if child is not None)
            continue

        if isinstance(node, ast.Call):
            kept = kept or not skipped.isdisjoint(range(node.lineno, (node.end_lineno or node.lineno) + 1))

            if not kept and not source.in_fmt_region(node.lineno):
                sites.append((node, line))

        children = reversed(list(ast.iter_child_nodes(node)))
        stack.extend((child, line, kept) for child in children if type(child) not in _LEAVES)

    return sorted(sites, key=lambda site: (site[0].lineno, site[0].col_offset))


def _is_ignored(name: str, patterns: Sequence[str]) -> bool:
    return any(fnmatchcase(name, pattern) for pattern in patterns)


def _resolve_options(options: Options) -> ResolveOptions:
    sources = frozenset(name for name in options.named_argument_sources if name in NAMED_ARGUMENT_SOURCES)
    return ResolveOptions(sources=sources, methods=options.named_argument_methods)


def find_named_calls(
    source: SourceFile, checks: Sequence[tuple[str, CallCheck]], options: Options, path: Path, index: SourceIndex
) -> list[NamedCall]:
    """
    Gives each call to the first check that plans names for it, and finds where the names go.

    A call is resolved once, and only when a check asks for its binding; the file is adopted by the index once.
    """
    if not checks:
        return []

    resolve_options = _resolve_options(options)
    patterns = options.named_argument_ignore

    resolvers: list[FileResolver] = []
    refused: list[bool] = []
    tokens: list[_Tokens] = []

    def resolve(call: ast.Call) -> Binding | None:
        # A project that refuses every call leaves each call of the file alone, so no call is read any further.
        if refused or _is_ignored(ast.unparse(call.func), patterns):
            return None

        if not resolvers:
            resolvers.append(FileResolver(ModuleContext.build(source.tree, path, source.text), index, resolve_options))

            if resolvers[0].refusal() is not None:
                refused.append(True)
                return None

        try:
            result = resolvers[0].resolve(call)
        except ValueError:
            return None

        return None if isinstance(result, Skipped) or _is_ignored(result.callee, patterns) else result

    named: list[NamedCall] = []

    for call, statement_line in collect_call_sites(source):
        cached: list[Binding | None] = []

        def resolve_once(call: ast.Call = call, cached: list[Binding | None] = cached) -> Binding | None:
            if not cached:
                cached.append(resolve(call))

            return cached[0]

        owner = next(((code, naming) for code, check in checks if (naming := check(call, resolve_once, options))), None)

        if owner is None:
            continue

        if not tokens:
            try:
                tokens.append(_Tokens(source))
            except (tokenize.TokenError, IndentationError, SyntaxError):
                return []

        code, naming = owner
        opening = tokens[0].find_opening(call)
        anchors = [tokens[0].find_argument(call, naming.start + offset) for offset in range(len(naming.names))]

        if opening is None or any(anchor is None for anchor in anchors):
            continue

        named.append(NamedCall(code, naming, statement_line, opening, cast("tuple[Position, ...]", tuple(anchors))))

    return named
