"""ZA3012: separates a guard chain and the bindings it checks from neighboring statements."""

from __future__ import annotations

from dataclasses import replace

from nickelony_stanza.analysis.model import BODY_KINDS, Container
from nickelony_stanza.analysis.session import ClaimContext
from nickelony_stanza.rules.base import Rule
from nickelony_stanza.rules.claims import claim_jump_gap, find_checked_stanza_gaps


def _claim(container: Container, index: int, context: ClaimContext) -> bool:
    if container.kind not in BODY_KINDS:
        return False

    borders, _, _ = find_checked_stanza_gaps(container, context)

    if index not in borders:
        return False

    jump_context = replace(
        context,
        is_claimed_by_others=lambda gap_index: (gap_index in borders) or context.is_claimed_by_others(gap_index),
    )

    return not ("ZA3013" in context.enabled_codes and claim_jump_gap(container, index, jump_context))


RULE = Rule(
    code="ZA3012",
    name="separate-checked-stanzas",
    level="opt-in",
    message="Add a blank line beside this checked stanza.",
    description="Separate a guard chain and the bindings it checks from neighboring statements.",
    claim=_claim,
    reads_others=True,
)
