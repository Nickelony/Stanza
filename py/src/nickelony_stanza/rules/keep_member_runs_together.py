"""ZA3056: removes the blank lines inside a run of class members that the member group rule reads as one."""

from __future__ import annotations

from nickelony_stanza.analysis.model import MEMBER_KINDS
from nickelony_stanza.rules.base import Rule
from nickelony_stanza.rules.claims import claim_kept_run

RULE = Rule(
    code="ZA3056",
    name="keep-member-runs-together",
    level="opt-in",
    message="Remove the blank line inside this member run.",
    description="Remove the blank lines inside a run of single-line class members that ZA3017 reads as one.",
    claim=claim_kept_run(MEMBER_KINDS),
    role="keep-run",
)
