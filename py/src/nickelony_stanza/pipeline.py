"""Runs the formatter and then the rules, and checks that the formatter leaves the result as it is."""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

from nickelony_stanza.analysis.document import Document
from nickelony_stanza.analysis.projects import Projects
from nickelony_stanza.config import Config
from nickelony_stanza.engine import fix
from nickelony_stanza.formatter import Formatter


@dataclass(frozen=True)
class PipelineResult:
    """The text after the formatter and the fix, and whether the formatter would leave it as it is."""

    text: str
    is_settled: bool


def _encoding(document: Document) -> str:
    """Names the encoding to hand the formatter, which reads a byte order mark as part of the first line."""
    return "utf-8" if document.encoding == "utf-8-sig" else document.encoding


def run_pipeline(
    document: Document,
    config: Config,
    formatter: Formatter | None,
    filename: str,
    *,
    path: Path | None = None,
    projects: Projects | None = None,
) -> PipelineResult:
    """
    Formats a document, fixes the result, and formats it again to see whether it settles, with a second round if not.

    The formatter runs first because it can join a statement onto one line or drop a blank line the fix wrote.
    A name the fix writes into a call can push a line past the formatter's width, so the formatter wraps it,
    and the fix runs once more over the wrapped text. A text is settled when the last formatter run changes nothing.
    """
    text = document.text

    if formatter is None:
        return PipelineResult(fix(text, config, path=path, projects=projects).text, is_settled=True)

    encoding = _encoding(document)

    def run_formatter(source: str) -> str:
        return formatter.format(source.encode(encoding), filename).decode(encoding)

    fixed = fix(run_formatter(text), config, path=path, projects=projects).text
    formatted = run_formatter(fixed)

    if formatted == fixed:
        return PipelineResult(fixed, is_settled=True)

    fixed = fix(formatted, config, path=path, projects=projects).text

    return PipelineResult(fixed, is_settled=run_formatter(fixed) == fixed)
