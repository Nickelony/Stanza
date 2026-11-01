"""ZA3010: separates the base call that opens or closes a method from the statements beside it."""

from __future__ import annotations

from nickelony_stanza.analysis.model import Container
from nickelony_stanza.analysis.session import ClaimContext
from nickelony_stanza.analysis.syntax import is_base_call
from nickelony_stanza.rules.base import Rule
from nickelony_stanza.rules.claims import find_guard_run


def _claim(container: Container, index: int, context: ClaimContext) -> bool:
    if container.kind != "function":
        return False

    units = container.units
    last = len(units) - 1
    start, end = find_guard_run(container, context)
    first = min(end, last)

    # The gap below the call that opens the body, the first statement below the guards, and the gap above the last.
    opens = index == first and first < last and is_base_call(units[first].node)
    closes = index == last - 1 and index >= start and is_base_call(units[last].node)

    return opens or closes


RULE = Rule(
    code="ZA3010",
    name="separate-base-call",
    level="strong",
    message="Add a blank line beside this base call.",
    description="Separate a super() call that opens or closes a function body from the statements beside it.",
    claim=_claim,
)
