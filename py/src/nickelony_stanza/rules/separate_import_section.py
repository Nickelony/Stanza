"""ZA3015: separates a run of imports from the first statement below it."""

from __future__ import annotations

from nickelony_stanza.analysis.model import Container
from nickelony_stanza.analysis.session import ClaimContext
from nickelony_stanza.analysis.syntax import is_import
from nickelony_stanza.rules.base import Rule


def _claim(container: Container, index: int, context: ClaimContext) -> bool:
    if container.kind in {"match", "clause"}:
        return False

    return is_import(container.units[index].node) and not is_import(container.units[index + 1].node)


RULE = Rule(
    code="ZA3015",
    name="separate-import-section",
    level="minimal",
    message="Add a blank line below the imports.",
    description="Separate a run of imports from the first statement below it.",
    claim=_claim,
)
