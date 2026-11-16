"""ZA3020: separates a run of yield statements."""

from __future__ import annotations

from nickelony_stanza.rules.base import Rule
from nickelony_stanza.rules.claims import claim_run_boundary

RULE = Rule(
    code="ZA3020",
    name="separate-yield-runs",
    level="strong",
    message="Add a blank line between this yield run and its neighbor.",
    description="Separate a run of two or more yield statements from the statements beside it.",
    claim=claim_run_boundary("yield"),
    family="yield",
)
