"""ZA3052: removes the blank lines inside a loose run of statements that holds too many."""

from __future__ import annotations

from nickelony_stanza.analysis.model import STATEMENT_KINDS
from nickelony_stanza.rules.base import Rule
from nickelony_stanza.rules.claims import claim_loose_run

RULE = Rule(
    code="ZA3052",
    name="gather-loose-statements",
    level="strong",
    message="Remove the blank line inside this run of loose statements.",
    description="Remove the blank lines in a run of loose single-line statements longer than max-loose-run-size.",
    claim=claim_loose_run(STATEMENT_KINDS),
    role="keep-run",
)
