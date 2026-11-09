"""ZA3006: separates a run of await statements."""

from __future__ import annotations

from nickelony_stanza.rules.base import Rule
from nickelony_stanza.rules.claims import claim_run_boundary

RULE = Rule(
    code="ZA3006",
    name="separate-await-runs",
    level="strong",
    message="Add a blank line between this await run and its neighbor.",
    description="Separate a run of two or more await statements from the statements beside it.",
    claim=claim_run_boundary("await"),
    family="await",
)
