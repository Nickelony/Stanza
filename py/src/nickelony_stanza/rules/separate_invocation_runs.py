"""ZA3004: separates a run of calls of one helper."""

from __future__ import annotations

from nickelony_stanza.rules.base import Rule
from nickelony_stanza.rules.claims import claim_run_boundary

RULE = Rule(
    code="ZA3004",
    name="separate-invocation-runs",
    level="moderate",
    message="Add a blank line between this invocation run and its neighbor.",
    description="Separate a run of two or more calls of one helper from the statements beside it.",
    claim=claim_run_boundary("invocation"),
    family="invocation",
)
