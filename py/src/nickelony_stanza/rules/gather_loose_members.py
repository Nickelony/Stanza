"""ZA3053: removes the blank lines inside a loose run of class members that holds too many."""

from __future__ import annotations

from nickelony_stanza.analysis.model import MEMBER_KINDS
from nickelony_stanza.rules.base import Rule
from nickelony_stanza.rules.claims import claim_loose_run

RULE = Rule(
    code="ZA3053",
    name="gather-loose-members",
    level="strong",
    message="Remove the blank line inside this run of loose members.",
    description="Remove the blank lines in a run of loose single-line class members longer than max-loose-run-size.",
    claim=claim_loose_run(MEMBER_KINDS),
    role="keep-run",
)
