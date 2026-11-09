"""ZA3008: separates a run of assignments to attributes, items and augmented targets."""

from __future__ import annotations

from nickelony_stanza.rules.base import Rule
from nickelony_stanza.rules.claims import claim_run_boundary

RULE = Rule(
    code="ZA3008",
    name="separate-assignment-runs",
    level="strong",
    message="Add a blank line between this assignment run and its neighbor.",
    description="Separate a run of two or more assignments from the statements beside it.",
    claim=claim_run_boundary("assignment"),
    family="assignment",
)
