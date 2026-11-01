"""ZA3202: removes the blank line between a block and the clause keyword that continues its statement."""

from __future__ import annotations

from nickelony_stanza.rules.base import Rule
from nickelony_stanza.rules.claims import claim_clause_gap

RULE = Rule(
    code="ZA3202",
    name="keep-clause-keywords-adjacent",
    level="minimal",
    message="Remove the blank line above this clause keyword.",
    description="Remove the blank line between a block and the elif, else, except or finally that continues it.",
    claim=claim_clause_gap,
    role="keep",
    opposite="ZA3201",
)
