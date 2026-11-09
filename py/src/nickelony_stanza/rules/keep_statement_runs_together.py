"""ZA3054: removes the blank lines inside a run of statements that the grouping rules read as one."""

from __future__ import annotations

from nickelony_stanza.analysis.model import STATEMENT_KINDS
from nickelony_stanza.rules.base import Rule
from nickelony_stanza.rules.claims import claim_kept_run

RULE = Rule(
    code="ZA3054",
    name="keep-statement-runs-together",
    level="opt-in",
    message="Remove the blank line inside this statement run.",
    description="Remove the blank lines inside a run of single-line statements that a grouping rule reads as one.",
    claim=claim_kept_run(STATEMENT_KINDS),
    role="keep-run",
)
