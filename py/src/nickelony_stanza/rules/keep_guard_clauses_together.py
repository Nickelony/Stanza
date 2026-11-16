"""ZA3057: removes the blank lines inside a chain of guard clauses."""

from __future__ import annotations

from nickelony_stanza.analysis.model import BODY_KINDS, Container
from nickelony_stanza.analysis.session import ClaimContext
from nickelony_stanza.analysis.syntax import is_assertion, is_guard
from nickelony_stanza.rules.base import Rule


def _claim(container: Container, index: int, context: ClaimContext) -> bool:
    previous, following = container.units[index], container.units[index + 1]

    if container.kind not in BODY_KINDS or container.gaps[index].has_comment:
        return False

    options = context.environment.options

    if any(unit.is_multi_line and is_assertion(unit.node, options) for unit in (previous, following)):
        return False

    return is_guard(previous.node, options) and is_guard(following.node, options)


RULE = Rule(
    code="ZA3057",
    name="keep-guard-clauses-together",
    level="opt-in",
    message="Remove the blank line inside this guard chain.",
    description="Remove the blank lines between neighboring guard clauses, so a chain of guards reads as one.",
    claim=_claim,
    role="keep",
    transfers_separation=True,
)
