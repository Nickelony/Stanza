"""ZA3017: separates the shebang, the encoding line and the module docstring from the code below."""

from __future__ import annotations

from nickelony_stanza.analysis.model import Container
from nickelony_stanza.analysis.session import ClaimContext
from nickelony_stanza.rules.base import Rule


def _claim(container: Container, index: int, context: ClaimContext) -> bool:
    return container.kind == "module" and container.gaps[index].restriction == "module-header"


RULE = Rule(
    code="ZA3017",
    name="separate-file-header",
    level="minimal",
    message="Add a blank line below the file header.",
    description="Separate the shebang, the encoding line and the module docstring from the code below.",
    claim=_claim,
    restrictions=frozenset({"module-header"}),
)
