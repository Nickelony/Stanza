"""ZA3003: separates a run of declarations of one type."""

from __future__ import annotations

from nickelony_stanza.analysis.classify import read_boundary_families
from nickelony_stanza.analysis.model import STATEMENT_KINDS, Container
from nickelony_stanza.analysis.session import ClaimContext
from nickelony_stanza.rules.base import Rule


def _claim(container: Container, index: int, context: ClaimContext) -> bool:
    return container.kind in STATEMENT_KINDS and bool(read_boundary_families(context.typed_runs_of(container), index))


RULE = Rule(
    code="ZA3003",
    name="separate-same-type-declaration-runs",
    level="strong",
    message="Add a blank line between this run of declarations of one type and its neighbor.",
    description="Separate a run of two or more declarations of one type from the statements beside it.",
    claim=_claim,
    family="typed-declaration",
)
