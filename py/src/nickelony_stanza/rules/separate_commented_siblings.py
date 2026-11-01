"""ZA3012: separates a comment that leads a single-line statement from the sibling above it."""

from __future__ import annotations

from nickelony_stanza.analysis.model import UNIT_KINDS, Container
from nickelony_stanza.analysis.session import ClaimContext
from nickelony_stanza.analysis.source import is_tool_directive
from nickelony_stanza.rules.base import Rule


def _claim(container: Container, index: int, context: ClaimContext) -> bool:
    if container.kind not in UNIT_KINDS:
        return False

    previous, following = container.units[index], container.units[index + 1]

    if previous.is_multi_line or following.is_multi_line:
        return False

    return any(not is_tool_directive(comment.text) for comment in container.gaps[index].lead_comments)


RULE = Rule(
    code="ZA3012",
    name="separate-commented-siblings",
    level="strong",
    message="Add a blank line above this comment.",
    description="Separate a comment that leads a single-line statement from the sibling above it.",
    claim=_claim,
)
