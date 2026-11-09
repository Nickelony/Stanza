"""Gives each gap to the one enabled rule that owns it."""

from __future__ import annotations

from collections.abc import Callable, Sequence
from dataclasses import dataclass
from typing import Literal, Protocol

from nickelony_stanza.analysis.classify import Environment, Family, Runs, find_runs
from nickelony_stanza.analysis.containers import collect_containers
from nickelony_stanza.analysis.model import Container, Gap, Restriction, Unit
from nickelony_stanza.analysis.source import SourceFile
from nickelony_stanza.analysis.two_units import find_two_unit_gap
from nickelony_stanza.options import Options

# What a rule does at a gap it owns:
# "separate": writes a blank line.
# "keep": removes the blank line of a gap it asks for, and no separating rule may ask for that gap.
# "keep-run": removes the blank line of a gap it asks for, unless a separating rule asks for the gap too.
Role = Literal["separate", "keep", "keep-run"]


@dataclass(frozen=True)
class ClaimContext:
    """What a claim may read besides the container: the shared environment and the runs of any container."""

    environment: Environment
    runs_of: Callable[[Container], Runs]

    # The runs of neighbors that share a declared type, which only the rule that separates them reads.
    typed_runs_of: Callable[[Container], Runs]

    # Tells whether a separating rule asks for a blank line at the gap, which only a rule that reads others may call.
    is_claimed_by_others: Callable[[int], bool]

    # What a claim keeps from one gap to the next of the container it judges.
    memo: dict[str, object]


# Tells whether a rule asks for a blank line at the gap between `units[index]` and `units[index + 1]`.
Claim = Callable[[Container, int, ClaimContext], bool]


class Claimant(Protocol):
    """What the session reads of a rule, so the analysis depends on no rule."""

    @property
    def code(self) -> str:
        """The code the owned gaps are keyed by."""
        ...

    @property
    def claim(self) -> Claim | None:
        """The question the rule asks of a gap, or None for a rule the session does not ask."""
        ...

    @property
    def role(self) -> Role:
        """What the rule does at a gap it owns."""
        ...

    @property
    def family(self) -> Family | None:
        """The family of runs the rule groups, or None for a rule that groups none."""
        ...

    @property
    def reads_others(self) -> bool:
        """Tells whether the rule reads the claims of the other rules, so it is asked after them."""
        ...

    @property
    def restrictions(self) -> frozenset[Restriction]:
        """The restricted gaps the rule may ask for."""
        ...


@dataclass(frozen=True)
class OwnedGap:
    """A gap a rule owns and reports."""

    container: Container
    index: int

    @property
    def gap(self) -> Gap:
        """The gap between the two units."""
        return self.container.gaps[self.index]

    @property
    def following(self) -> Unit:
        """The unit below the gap."""
        return self.container.units[self.index + 1]


@dataclass(frozen=True)
class _Runs:
    """The two ways to read the runs of a container, each cached for the container."""

    base: Callable[[Container], Runs]
    typed: Callable[[Container], Runs]


def _asks(rule: Claimant, gap: Gap) -> bool:
    return not gap.is_joined and (gap.restriction is None or gap.restriction in rule.restrictions)


def _read_claims(
    container: Container, enabled: Sequence[Claimant], environment: Environment, runs: _Runs
) -> dict[str, list[bool]]:
    size = len(container.gaps)
    memo: dict[str, object] = {}
    claimed: dict[str, list[bool]] = {}

    kept = [False] * size
    union = [False] * size
    everything = [False] * size

    def ask(rule: Claimant, is_claimed_by_others: Callable[[int], bool]) -> list[bool]:
        context = ClaimContext(environment, runs.base, runs.typed, is_claimed_by_others, memo)
        claim = rule.claim

        return [
            claim is not None and _asks(rule, gap) and claim(container, index, context)
            for index, gap in enumerate(container.gaps)
        ]

    # A rule that keeps a pair apart wins the gap it asks for, so no separating rule sees that gap as asked for.
    for rule in (rule for rule in enabled if rule.role == "keep"):
        claimed[rule.code] = ask(rule, kept.__getitem__)
        kept[:] = [taken or flag for taken, flag in zip(kept, claimed[rule.code], strict=True)]

    separating = [rule for rule in enabled if rule.role == "separate"]

    ordered = [rule for rule in separating if not rule.reads_others] + [
        rule for rule in separating if rule.reads_others
    ]

    for rule in ordered:
        flags = [flag and not stays for flag, stays in zip(ask(rule, union.__getitem__), kept, strict=True)]

        claimed[rule.code] = flags
        everything[:] = [taken or flag for taken, flag in zip(everything, flags, strict=True)]

        if not rule.reads_others:
            union[:] = [taken or flag for taken, flag in zip(union, flags, strict=True)]

    # A rule that keeps a run yields every gap that a separating rule asks for.
    for rule in (rule for rule in enabled if rule.role == "keep-run"):
        claimed[rule.code] = ask(rule, everything.__getitem__)

    return claimed


def analyze(
    source: SourceFile,
    enabled: Sequence[Claimant],
    options: Options,
    *,
    pair: Claimant | None,
) -> dict[str, list[OwnedGap]]:
    """
    Reads the file once and returns the gaps each rule owns, keyed by rule code.

    The enabled rules come in the order that decides which of them reports a gap several ask for,
    and `pair` is the rule that removes the blank line of a pair, or None while it is off.
    """
    families = frozenset(rule.family for rule in enabled if rule.family is not None)
    environment = Environment(options=options, families=families)

    runs_by_container: dict[tuple[int, bool], Runs] = {}

    def runs_for(typed: bool) -> Callable[[Container], Runs]:
        def runs_of(container: Container) -> Runs:
            key = (id(container), typed)

            if key not in runs_by_container:
                runs_by_container[key] = find_runs(container, environment, typed=typed)

            return runs_by_container[key]

        return runs_of

    runs = _Runs(runs_for(typed=False), runs_for(typed=True))
    separators = [rule for rule in enabled if rule.role == "separate"]
    keepers = [rule for rule in enabled if rule.role != "separate"]

    owned: dict[str, list[OwnedGap]] = {}

    for container in collect_containers(source, clauses=True, headers=True):
        pair_index = find_two_unit_gap(container)

        if pair_index is not None:
            pair_gap = container.gaps[pair_index]

            if pair is not None and pair_gap.is_blank and len(pair_gap.blank_lines) > 0:
                owned.setdefault(pair.code, []).append(OwnedGap(container, pair_index))

            continue

        claimed = _read_claims(container, enabled, environment, runs)

        for index, gap in enumerate(container.gaps):
            if gap.is_joined:
                continue

            # Blank lines are judged by the rules that remove them, and a tight gap by the rules that write one.
            if len(gap.blank_lines) > 0:
                candidates = keepers
            elif gap.is_blank:
                continue
            else:
                candidates = separators

            owner = next((rule for rule in candidates if claimed[rule.code][index]), None)

            if owner is not None:
                owned.setdefault(owner.code, []).append(OwnedGap(container, index))

    return owned
