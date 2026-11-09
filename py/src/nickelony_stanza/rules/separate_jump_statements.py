"""ZA3009: separates a jump statement from a stanza of two or more lines above it."""

from __future__ import annotations

from nickelony_stanza.analysis.model import STATEMENT_KINDS, Container
from nickelony_stanza.analysis.session import ClaimContext
from nickelony_stanza.analysis.syntax import is_jump
from nickelony_stanza.rules.base import Rule


def _claim(container: Container, index: int, context: ClaimContext) -> bool:
    following = container.units[index + 1]

    if container.kind not in STATEMENT_KINDS or not is_jump(following.node):
        return False

    # The stanza above the jump reaches back through gaps that are neither blank nor asked for by another rule,
    # and it spans two lines as soon as one unit it reaches starts above the last line of the unit beside the jump.
    last_line = container.units[index].last_line
    start = index

    while last_line - container.units[start].first_line < 1:
        if start == 0 or container.gaps[start - 1].is_blank or context.is_claimed_by_others(start - 1):
            return False

        start -= 1

    return True


RULE = Rule(
    code="ZA3009",
    name="separate-jump-statements",
    level="strong",
    message="Add a blank line above this jump.",
    description="Separate a return, raise, break or continue from a stanza of two or more lines above it.",
    claim=_claim,
    reads_others=True,
)
