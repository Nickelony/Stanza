"""ZA3001: separates a statement that spans several lines from its neighbors."""

from __future__ import annotations

from nickelony_stanza.analysis.model import UNIT_KINDS, Container
from nickelony_stanza.analysis.session import ClaimContext
from nickelony_stanza.analysis.syntax import is_import
from nickelony_stanza.rules.base import Rule


def _claim(container: Container, index: int, context: ClaimContext) -> bool:
    previous, following = container.units[index], container.units[index + 1]

    if container.kind not in UNIT_KINDS or not (previous.is_multi_line or following.is_multi_line):
        return False

    return not (is_import(previous.node) and is_import(following.node))


RULE = Rule(
    code="ZA3001",
    name="separate-multi-line-constructs",
    level="moderate",
    message="Add a blank line between this multi-line construct and its neighbor.",
    description="Separate a statement that spans more than one line from its neighbors.",
    claim=_claim,
)
