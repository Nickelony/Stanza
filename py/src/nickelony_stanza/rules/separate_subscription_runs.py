"""ZA3008: separates a run of subscriptions to an event."""

from __future__ import annotations

from nickelony_stanza.rules.base import Rule
from nickelony_stanza.rules.claims import claim_run_boundary

RULE = Rule(
    code="ZA3008",
    name="separate-subscription-runs",
    level="strong",
    message="Add a blank line between this subscription run and its neighbor.",
    description="Separate a run of two or more event subscriptions from the statements beside it.",
    claim=claim_run_boundary("subscription"),
    family="subscription",
)
