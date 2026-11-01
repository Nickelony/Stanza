"""The shape of a rule: its identity, its level and the question it asks of each gap or each call."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Literal, get_args

from nickelony_stanza.analysis.calls import CallCheck
from nickelony_stanza.analysis.classify import Family
from nickelony_stanza.analysis.model import Restriction
from nickelony_stanza.analysis.session import Claim, Role

Level = Literal["none", "minimal", "moderate", "strong"]

# The level of a rule is the first level that enables it, or "opt-in" for a rule that no level enables.
RuleLevel = Literal["minimal", "moderate", "strong", "opt-in"]

# The levels from the level that enables nothing to the level that enables the most.
LEVELS: tuple[Level, ...] = get_args(Level)


@dataclass(frozen=True)
class Rule:
    """A rule: the code a user silences, the name docs show, the level that enables it and the question it asks."""

    code: str
    name: str
    level: RuleLevel
    message: str
    description: str
    claim: Claim | None

    # True when the rule reads the claims of the other rules, so it is evaluated after them.
    reads_others: bool = False

    # The gaps no other rule may claim, because a formatter or a convention owns them and this rule agrees.
    restrictions: frozenset[Restriction] = frozenset()

    # What the rule does at a gap it owns: write a blank line, or remove one.
    role: Role = "separate"

    # The family of runs the rule groups, so a statement is read as a member of that run only while the rule is enabled.
    family: Family | None = None

    # The code of the rule that does the opposite at the same gaps; naming one of the two replaces the other at a level.
    opposite: str | None = None

    # The names a call rule writes into a call; a call rule claims no gap, so its `claim` is None.
    check: CallCheck | None = None
