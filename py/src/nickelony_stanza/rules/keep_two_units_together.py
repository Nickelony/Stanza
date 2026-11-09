"""ZA3051: removes the blank line between the only two single-line units of a container."""

from __future__ import annotations

from nickelony_stanza.rules.base import Rule

RULE = Rule(
    code="ZA3051",
    name="keep-two-units-together",
    level="moderate",
    message="Remove the blank line between these two lines.",
    description="Remove the blank line between the only two units of a container when each spans one line.",
    claim=None,
    role="keep",
)
