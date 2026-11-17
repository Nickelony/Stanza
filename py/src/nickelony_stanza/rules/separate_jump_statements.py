"""ZA3013: separates a jump statement from a stanza of two or more lines above it."""

from __future__ import annotations

from nickelony_stanza.rules.base import Rule
from nickelony_stanza.rules.claims import claim_jump_gap

RULE = Rule(
    code="ZA3013",
    name="separate-jump-statements",
    level="strong",
    message="Add a blank line above this jump.",
    description="Separate a return, raise, break or continue from a stanza of two or more lines above it.",
    claim=claim_jump_gap,
    reads_others=True,
)
