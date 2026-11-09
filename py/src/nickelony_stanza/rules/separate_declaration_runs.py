"""ZA3002: separates a run of declarations of one kind."""

from __future__ import annotations

from nickelony_stanza.rules.base import Rule
from nickelony_stanza.rules.claims import claim_run_boundary

RULE = Rule(
    code="ZA3002",
    name="separate-declaration-runs",
    level="moderate",
    message="Add a blank line between this declaration run and its neighbor.",
    description="Separate a run of two or more declarations of one kind from the statements beside it.",
    claim=claim_run_boundary("declaration"),
    family="declaration",
)
