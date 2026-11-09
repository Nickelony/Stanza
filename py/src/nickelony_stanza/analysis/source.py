"""Reads a Python file once: its lines, its syntax tree and its comments."""

from __future__ import annotations

import ast
import re
import tokenize
import warnings
from bisect import bisect_left
from dataclasses import dataclass
from functools import cached_property

_LINE = re.compile(r"[^\r\n]*(?:\r\n|\r|\n)|[^\r\n]+")
_COOKIE = re.compile(r"^[ \t\f]*#.*?coding[:=][ \t]*[-\w.]+")

# The one definition of a tool directive: a comment that addresses a type checker, a linter or a formatter.
_TOOL_DIRECTIVE = re.compile(
    r"#\s*(?:type:|noqa\b|pragma\b|fmt:|yapf:|pylint:|pyright:|pyre-|mypy:|isort:|flake8:|ruff:|nosec\b|noinspection\b|pytype:)",
    re.IGNORECASE,
)

_FMT_OFF = re.compile(r"#\s*(?:fmt:\s*off|yapf:\s*disable)\b")
_FMT_ON = re.compile(r"#\s*(?:fmt:\s*on|yapf:\s*enable)\b")
_FMT_SKIP = re.compile(r"#\s*fmt:\s*skip\b")


def parse(text: str) -> ast.Module:
    """Parses a text, without the warnings the compiler prints for an escape sequence or a literal."""
    with warnings.catch_warnings():
        warnings.simplefilter("ignore")
        return ast.parse(text)


def is_tool_directive(comment: str) -> bool:
    """Tells whether a comment is a tool directive, such as `# type: ignore`, `# noqa` or `# fmt: off`."""
    return _TOOL_DIRECTIVE.match(comment) is not None


def split_lines(text: str) -> list[str]:
    """Splits text at the line breaks the Python tokenizer knows, keeping each line ending."""
    return _LINE.findall(text)


@dataclass(frozen=True)
class Comment:
    """A comment token, with the line it stands on."""

    line: int
    text: str
    standalone: bool


class SourceFile:
    """
    A parsed file: the text split into lines, the syntax tree and the comments.

    A caller that has already parsed the text, as the flake8 plugin has, passes its tree instead of parsing again.
    """

    def __init__(self, text: str, *, tree: ast.Module | None = None) -> None:
        self.text = text
        self.lines = split_lines(text)
        self.tree = parse(text) if tree is None else tree
        self.comments = self._read_comments()
        self.comments_by_line = {comment.line: comment for comment in self.comments}

    @cached_property
    def default_eol(self) -> str:
        """The line ending of the first line that has one, or a line feed."""
        for line in self.lines:
            ending = self.read_ending(line)

            if ending:
                return ending

        return "\n"

    @cached_property
    def preamble_last_line(self) -> int:
        """The last line of the shebang and encoding cookie that open the file, or 0 when there are none."""
        last = 0

        if self.lines and self.lines[0].startswith("#!"):
            last = 1

        for number in (1, 2):
            if number > len(self.lines) or not _COOKIE.match(self.lines[number - 1]):
                continue

            if number == 1 or self.lines[0].strip(" \t\f\r\n") == "" or self.lines[0].lstrip().startswith("#"):
                last = max(last, number)

        return last

    @cached_property
    def fmt_regions(self) -> list[tuple[int, int]]:
        """The line ranges between a `# fmt: off` comment and the `# fmt: on` that ends it, the off line excluded."""
        regions: list[tuple[int, int]] = []
        start: int | None = None

        for comment in self.comments:
            if start is None and _FMT_OFF.match(comment.text):
                start = comment.line
            elif start is not None and _FMT_ON.match(comment.text):
                regions.append((start, comment.line))
                start = None

        if start is not None:
            regions.append((start, len(self.lines) + 1))

        return regions

    @cached_property
    def fmt_skip_lines(self) -> frozenset[int]:
        """The lines a trailing `# fmt: skip` comment, alone or after another directive, tells a formatter to keep."""
        return frozenset(
            comment.line for comment in self.comments if not comment.standalone and _FMT_SKIP.search(comment.text)
        )

    def holds_tool_directive(self, first: int, last: int) -> bool:
        """Tells whether a tool directive comment stands on a line from `first` to `last`."""
        return any(
            (comment := self.comments_by_line.get(number)) is not None and is_tool_directive(comment.text)
            for number in range(first, last + 1)
        )

    @staticmethod
    def read_ending(line: str) -> str:
        """Reads the line ending a line carries."""
        if line.endswith("\r\n"):
            return "\r\n"

        return line[-1] if line[-1:] in {"\n", "\r"} else ""

    def eol_before(self, number: int) -> str:
        """Reads the line ending the file uses directly above a line, so an inserted line matches its neighbors."""
        for index in range(min(number, len(self.lines)) - 2, -1, -1):
            ending = self.read_ending(self.lines[index])

            if ending:
                return ending

        return self.default_eol

    def is_whitespace_line(self, number: int) -> bool:
        """Tells whether a line holds only whitespace."""
        return self.lines[number - 1].strip(" \t\f\r\n") == ""

    def indent_width(self, number: int) -> int:
        """Reads the width of the whitespace that opens a line, counting a tab as up to eight columns."""
        line = self.lines[number - 1]
        return len(line[: len(line) - len(line.lstrip(" \t\f"))].expandtabs(8))

    def in_fmt_region(self, number: int) -> bool:
        """Tells whether a line sits inside a region a formatter is told to leave alone."""
        index = bisect_left(self.fmt_regions, (number,)) - 1
        return index >= 0 and number <= self.fmt_regions[index][1]

    def _read_comments(self) -> list[Comment]:
        comments: list[Comment] = []

        try:
            lines = iter(self.lines)

            for token in tokenize.generate_tokens(lambda: next(lines, "")):
                if token.type != tokenize.COMMENT:
                    continue

                line, col = token.start
                standalone = token.line[:col].strip(" \t\f") == ""

                comments.append(Comment(line=line, text=token.string, standalone=standalone))
        except (tokenize.TokenError, IndentationError):
            return self._read_comments_by_line()

        return comments

    def _read_comments_by_line(self) -> list[Comment]:
        comments: list[Comment] = []

        for number, line in enumerate(self.lines, start=1):
            stripped = line.lstrip(" \t\f")

            if stripped.startswith("#"):
                comments.append(Comment(line=number, text=stripped.rstrip("\r\n"), standalone=True))

        return comments
