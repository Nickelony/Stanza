"""ZA3058: removes the blank line between a binding and the guard clause that tests what it binds."""

from __future__ import annotations

from nickelony_stanza.analysis.model import BODY_KINDS, Container
from nickelony_stanza.analysis.session import ClaimContext
from nickelony_stanza.rules.base import Rule
from nickelony_stanza.rules.claims import find_checked_stanza_gaps


def _claim(container: Container, index: int, context: ClaimContext) -> bool:
    if container.kind not in BODY_KINDS or container.gaps[index].has_comment:
        return False

    # The checked stanza holds every binding an `if` or a helper guard reads; an `assert` belongs to ZA3061.
    _, guarded_bindings, _ = find_checked_stanza_gaps(container, context)

    return index in guarded_bindings


RULE = Rule(
    code="ZA3058",
    name="keep-guard-with-binding",
    level="opt-in",
    message="Remove the blank line between this binding and the guard that tests it.",
    description="Remove the blank line between a statement that binds a name and the guard clause that tests it.",
    claim=_claim,
    role="keep",
    transfers_separation=True,
)
