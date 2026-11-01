"""ZA3005: separates a run of assertions."""

from __future__ import annotations

from nickelony_stanza.rules.base import Rule
from nickelony_stanza.rules.claims import claim_run_boundary

RULE = Rule(
    code="ZA3005",
    name="separate-assertion-runs",
    level="moderate",
    message="Add a blank line between this assertion run and its neighbor.",
    description="Separate a run of two or more assertions from the statements beside it.",
    claim=claim_run_boundary("assertion"),
    family="assertion",
)
