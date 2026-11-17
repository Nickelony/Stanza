"""ZA3060: removes the blank line between a statement that binds a name and the assert that narrows that name."""

from __future__ import annotations

from nickelony_stanza.analysis.model import STATEMENT_KINDS, Container
from nickelony_stanza.analysis.session import ClaimContext
from nickelony_stanza.analysis.syntax import bound_names
from nickelony_stanza.rules.base import Rule
from nickelony_stanza.rules.claims import find_checked_stanza_gaps, narrowed_by_assert


def _claim(container: Container, index: int, context: ClaimContext) -> bool:
    if container.kind not in STATEMENT_KINDS or container.gaps[index].has_comment:
        return False

    _, _, asserted_bindings = find_checked_stanza_gaps(container, context)

    if index in asserted_bindings:
        return True

    narrowed = narrowed_by_assert(container, index + 1, context.environment.options)

    return not narrowed.isdisjoint(bound_names(container.units[index].node))


RULE = Rule(
    code="ZA3060",
    name="keep-assertion-with-binding",
    level="opt-in",
    message="Remove the blank line between this binding and the assert that narrows it.",
    description="Remove the blank line between a statement that binds a name and the assert that narrows its type.",
    claim=_claim,
    role="keep",
    transfers_separation=True,
)
