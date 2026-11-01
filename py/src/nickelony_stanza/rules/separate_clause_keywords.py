"""ZA3201: separates a clause keyword from the block above it."""

from __future__ import annotations

from nickelony_stanza.rules.base import Rule
from nickelony_stanza.rules.claims import claim_clause_gap

RULE = Rule(
    code="ZA3201",
    name="separate-clause-keywords",
    level="opt-in",
    message="Add a blank line above this clause keyword.",
    description="Separate the elif, else, except or finally that continues a statement from the block above it.",
    claim=claim_clause_gap,
    opposite="ZA3202",
)
