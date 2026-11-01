"""ZA3013: separates the docstring and the runs of fields and attributes of a class body."""

from __future__ import annotations

from nickelony_stanza.analysis.classify import read_boundary_families
from nickelony_stanza.analysis.model import Container
from nickelony_stanza.analysis.session import ClaimContext
from nickelony_stanza.rules.base import Rule


def _claim(container: Container, index: int, context: ClaimContext) -> bool:
    if container.kind != "class":
        return False

    if container.gaps[index].restriction == "class-docstring":
        return True

    return "member" in read_boundary_families(context.runs_of(container), index)


RULE = Rule(
    code="ZA3013",
    name="separate-member-groups",
    level="moderate",
    message="Add a blank line between this member group and its neighbor.",
    description="Separate the docstring and the runs of fields and attributes of a class body.",
    claim=_claim,
    restrictions=frozenset({"class-docstring"}),
    family="member",
)
