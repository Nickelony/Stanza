"""Reads the `# noqa` comments that silence a finding."""

from __future__ import annotations

import re
from dataclasses import dataclass, field

from nickelony_stanza.analysis.source import SourceFile

_CODES = r"(?P<codes>[A-Za-z]+\d*(?:[\s,]+[A-Za-z]+\d*)*)"
_LINE = re.compile(rf"#\s*noqa\b(?::\s*{_CODES})?", re.IGNORECASE)
_FILE = re.compile(rf"#\s*(?:flake8|ruff)\s*:\s*noqa\b(?::\s*{_CODES})?", re.IGNORECASE)


def _split_codes(text: str | None) -> frozenset[str] | None:
    """Reads the codes of a comment, or None for a comment that names no code and so silences them all."""
    if text is None:
        return None

    return frozenset(code.upper() for code in re.split(r"[\s,]+", text.strip()) if code)


def _covers(codes: frozenset[str] | None, code: str) -> bool:
    return codes is None or any(code.startswith(prefix) for prefix in codes)


@dataclass(frozen=True)
class Noqa:
    """The comments of a file that silence findings, by line and for the whole file."""

    lines: dict[int, frozenset[str] | None] = field(default_factory=dict)
    whole_file: list[frozenset[str] | None] = field(default_factory=list)

    @classmethod
    def read(cls, source: SourceFile) -> Noqa:
        """Reads the `# noqa`, `# flake8: noqa` and `# ruff: noqa` comments of a file."""
        lines: dict[int, frozenset[str] | None] = {}
        whole_file: list[frozenset[str] | None] = []

        for comment in source.comments:
            whole = _FILE.search(comment.text)

            if whole is not None and comment.standalone:
                whole_file.append(_split_codes(whole.group("codes")))
                continue

            match = _LINE.search(comment.text)

            if match is not None:
                lines[comment.line] = _split_codes(match.group("codes"))

        return cls(lines, whole_file)

    def silences(self, code: str, *line_numbers: int) -> bool:
        """Tells whether a comment silences a rule for a finding on any of the lines."""
        if any(_covers(codes, code) for codes in self.whole_file):
            return True

        return any(number in self.lines and _covers(self.lines[number], code) for number in line_numbers)
