"""Gives each gap to the one enabled rule that owns it."""

from __future__ import annotations

from collections.abc import Callable, Sequence
from dataclasses import dataclass
from typing import Literal, Protocol

from nickelony_stanza.analysis.classify import Environment, Family, Runs, find_runs
from nickelony_stanza.analysis.containers import collect_containers
from nickelony_stanza.analysis.dependent_bindings import DependentBindingGroups, find_dependent_binding_groups
from nickelony_stanza.analysis.model import Container, Gap, Restriction, Unit
from nickelony_stanza.analysis.receivers import RECEIVER_CONSUMERS, ReceiverGroups, find_receiver_groups
from nickelony_stanza.analysis.source import SourceFile
from nickelony_stanza.analysis.syntax import read_type_names
from nickelony_stanza.analysis.two_units import find_two_unit_gap
from nickelony_stanza.options import Options

# What a rule does at a gap it owns:
# "separate": writes a blank line.
# "keep": removes the blank line of a gap it asks for, and no separating rule may ask for that gap.
# "keep-run": removes the blank line of a gap it asks for, unless a separating rule asks for the gap too.
# "gather": gathers loose units after transferred separation settles;
# it reports every gap of a run, and the fix of any of them removes the blank lines of the whole run.
Role = Literal["separate", "keep", "keep-run", "gather"]


@dataclass(frozen=True)
class ClaimContext:
    """What a claim may read besides the container: the shared environment and the runs of any container."""

    environment: Environment
    enabled_codes: frozenset[str]
    runs_of: Callable[[Container], Runs]

    # The runs of neighbors that share a declared type, which only the rule that separates them reads.
    typed_runs_of: Callable[[Container], Runs]
    receivers_of: Callable[[Container], ReceiverGroups]
    dependent_bindings_of: Callable[[Container], DependentBindingGroups]
    protected_sections_of: Callable[[Container], frozenset[int]]

    # Joins already held by strong keepers, including the bounded preview of run-border readers;
    # the rules asked after every keeper read the joins of every keeper.
    is_kept: Callable[[int], bool]

    # Tells whether the keeper of a code holds the gap, unless a noqa comment silences it there; it reads the keepers
    # that read no rule, which are asked first.
    is_held_by: Callable[[str, int], bool]

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
    def transfers_separation(self) -> bool:
        """Tells whether a kept pair preserves separation above its group."""
        ...

    @property
    def removes_group(self) -> bool:
        """Tells whether one finding removes the eligible inner gaps of its owned group."""
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
    group_indices: tuple[int, ...] = ()

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
    """Cached container readers shared by native runs and relationship claims."""

    base: Callable[[Container], Runs]
    typed: Callable[[Container], Runs]
    receivers: Callable[[Container], ReceiverGroups]
    dependent_bindings: Callable[[Container], DependentBindingGroups]
    protected_sections: Callable[[Container], frozenset[int]]


def _asks(rule: Claimant, gap: Gap) -> bool:
    return not gap.is_joined and (gap.restriction is None or gap.restriction in rule.restrictions)


# Reads the gaps each rule asks for in a container, by rule code, before they are given to owners.
ClaimsObserver = Callable[[Container, dict[str, list[bool]]], None]

# Tells whether a noqa comment silences the rule of a code at the gap of a container with the given index.
Silenced = Callable[[str, Container, int], bool]


def _merge(into: list[bool], flags: Sequence[bool]) -> None:
    into[:] = [taken or flag for taken, flag in zip(into, flags, strict=True)]


class _ClaimReader:
    """Asks the enabled rules about the gaps of one container in phases, each phase reading the ones before it."""

    def __init__(
        self,
        container: Container,
        enabled: Sequence[Claimant],
        environment: Environment,
        runs: _Runs,
        silenced: Silenced,
    ) -> None:
        size = len(container.gaps)

        self.container = container
        self.enabled = enabled
        self.environment = environment
        self.runs = runs
        self.silenced = silenced
        self.codes = frozenset(rule.code for rule in enabled)
        self.memo: dict[str, object] = {}
        self.claimed: dict[str, list[bool]] = {}

        # The gaps a keeper holds, and the ones whose kept pair moves a displaced separation above its group.
        self.kept = [False] * size
        self.transferable = [False] * size

        # The joins a jump stanza may reach through: those of the strong keepers and the previewed late keepers.
        self.prospective = [False] * size

        # The gaps the independent separators ask for, and the ones every separator asks for.
        self.union = [False] * size
        self.everything = [False] * size
        self.asked_by_rule: dict[str, list[bool]] = {}

        separating = [rule for rule in enabled if rule.role == "separate"]

        self.independent = [rule for rule in separating if not rule.reads_others]
        self.dependent = [rule for rule in separating if rule.reads_others]
        self.late_keepers = [rule for rule in enabled if rule.role == "keep" and rule.reads_others]

    def ask(self, rule: Claimant, is_claimed_by_others: Callable[[int], bool], memo: dict[str, object]) -> list[bool]:
        context = ClaimContext(
            self.environment,
            self.codes,
            self.runs.base,
            self.runs.typed,
            self.runs.receivers,
            self.runs.dependent_bindings,
            self.runs.protected_sections,
            self.prospective.__getitem__,
            self.is_held_by,
            is_claimed_by_others,
            memo,
        )

        claim = rule.claim

        return [
            claim is not None and _asks(rule, gap) and claim(self.container, index, context)
            for index, gap in enumerate(self.container.gaps)
        ]

    def is_held_by(self, code: str, index: int) -> bool:
        flags = self.claimed.get(code)
        return flags is not None and flags[index]

    def hold(self, rule: Claimant, flags: list[bool]) -> list[bool]:
        """Leaves out each gap where a `# noqa` silences the keeper, so the gap is judged as if the keeper were off."""
        return [flag and not self.silenced(rule.code, self.container, index) for index, flag in enumerate(flags)]

    def read_keepers(self, keepers: Sequence[Claimant], is_claimed_by_others: Callable[[int], bool]) -> None:
        for rule in keepers:
            held = self.hold(rule, self.ask(rule, is_claimed_by_others, self.memo))
            self.claimed[rule.code] = held

            _merge(self.kept, held)

            if rule.transfers_separation:
                _merge(self.transferable, held)

    def read_separators(self, rules: Sequence[Claimant]) -> None:
        for rule in rules:
            asked = self.ask(rule, self.union.__getitem__, self.memo)
            flags = [flag and not stays for flag, stays in zip(asked, self.kept, strict=True)]

            self.asked_by_rule[rule.code] = asked
            self.claimed[rule.code] = flags

            _merge(self.everything, flags)

            # A separator that reads others sees the ones asked before it too, in the order of the rules.
            _merge(self.union, flags)

    def preview_late_keepers(self) -> None:
        """
        Shows a separator that reads others the joins of the late keepers before those keepers read every separator.

        The preview reads with a memo of its own, so the final read judges its candidates against every separator.
        """
        for rule in self.late_keepers:
            _merge(self.prospective, self.hold(rule, self.ask(rule, self.union.__getitem__, {})))

        # The independent separators read the previewed joins again, from the same cached container readers.
        self.union[:] = [False] * len(self.union)
        self.everything[:] = [False] * len(self.everything)

        self.read_separators(self.independent)

    def settle_separators(self) -> dict[str, set[int]]:
        """Drops each separating claim at a kept gap, and lists the upper edges its displaced claims move to."""
        size = len(self.container.gaps)

        # Each kept chain shares one upper edge, computed once so long chains stay linear.
        edges: list[int | None] = [None] * size

        for index in range(size):
            if self.transferable[index]:
                edges[index] = edges[index - 1] if index > 0 and self.transferable[index - 1] else index - 1

        self.everything[:] = [False] * size
        transfers: dict[str, set[int]] = {}

        for rule in (*self.independent, *self.dependent):
            asked = self.asked_by_rule[rule.code]
            flags = [flag and not stays for flag, stays in zip(asked, self.kept, strict=True)]

            self.claimed[rule.code] = flags
            _merge(self.everything, flags)

            transfers[rule.code] = {
                edge
                for index, flag in enumerate(asked)
                if flag and self.transferable[index] and (edge := edges[index]) is not None and edge >= 0
            }

        return transfers

    def apply_transfers(self, transfers: dict[str, set[int]]) -> None:
        """Moves each displaced claim to the upper edge of its kept group while no keeper or comment holds it."""
        run_keepers = [rule for rule in self.enabled if rule.role == "keep-run"]

        for rule in (*self.independent, *self.dependent):
            for index in transfers[rule.code]:
                gap = self.container.gaps[index]

                if (
                    not self.kept[index]
                    and not gap.has_comment
                    and _asks(rule, gap)
                    and not any(self.claimed[keeper.code][index] for keeper in run_keepers)
                ):
                    self.claimed[rule.code][index] = True
                    self.everything[index] = True

    def read_yielding(self, role: Role) -> None:
        """Asks the rules of a role that yields every gap a separating rule asks for."""
        for rule in (rule for rule in self.enabled if rule.role == role):
            self.claimed[rule.code] = self.hold(rule, self.ask(rule, self.everything.__getitem__, self.memo))


def _read_claims(
    container: Container, enabled: Sequence[Claimant], environment: Environment, runs: _Runs, silenced: Silenced
) -> dict[str, list[bool]]:
    reader = _ClaimReader(container, enabled, environment, runs, silenced)

    # A strong keeper wins its gap, so no separating rule sees that gap as asked for.
    strong = [rule for rule in enabled if rule.role == "keep" and not rule.reads_others]

    reader.read_keepers(strong, reader.kept.__getitem__)
    reader.prospective[:] = reader.kept
    reader.read_separators(reader.independent)

    if reader.dependent and reader.late_keepers:
        reader.preview_late_keepers()

    reader.read_separators(reader.dependent)

    # A keeper that reads run borders judges them after the separating claims exist.
    reader.read_keepers(reader.late_keepers, reader.everything.__getitem__)
    reader.prospective[:] = reader.kept
    transfers = reader.settle_separators()

    # Run keepers judge the original borders, before a displaced claim can take their upper gap.
    reader.read_yielding("keep-run")
    reader.apply_transfers(transfers)

    # Gathering judges the final borders; otherwise a loose group could erase the edge the next pass writes.
    reader.read_yielding("gather")

    return reader.claimed


def _removes_components(rule: Claimant) -> bool:
    """Tells whether a finding of the rule removes the blank lines of its whole component, not of its own gap."""
    return rule.removes_group or rule.role == "gather"


def _find_removal_groups(
    container: Container, keepers: Sequence[Claimant], claimed: dict[str, list[bool]]
) -> dict[int, tuple[int, ...]]:
    """Finds each grouped remover's contiguous owned components once, including their already-tight edges."""
    if not any(_removes_components(rule) for rule in keepers):
        return {}

    owners = [
        next((rule for rule in keepers if claimed[rule.code][index]), None) for index in range(len(container.gaps))
    ]

    groups: dict[int, tuple[int, ...]] = {}
    start = 0

    while start < len(owners):
        owner = owners[start]

        if owner is None or not _removes_components(owner):
            start += 1
            continue

        end = start + 1

        while end < len(owners) and owners[end] is owner:
            end += 1

        indices = tuple(index for index in range(start, end) if container.gaps[index].blank_lines)
        groups.update((index, indices) for index in indices)
        start = end

    return groups


def analyze(
    source: SourceFile,
    enabled: Sequence[Claimant],
    options: Options,
    *,
    pair: Claimant | None,
    two_unit_rules: Sequence[Claimant],
    silenced: Silenced,
    observer: ClaimsObserver | None = None,
) -> dict[str, list[OwnedGap]]:
    """
    Reads the file once and returns the gaps each rule owns, keyed by rule code.

    The enabled rules come in the order that decides which of them reports a gap several ask for,
    and `pair` is the rule that removes the blank line of a pair, or None while it is off.
    While it is off, the enabled `two_unit_rules` alone judge such a pair.
    `silenced` tells where a `# noqa` silences a keeper or the pair, which then leaves the gap as if it were off there.
    `observer` sees the claims of each container the enabled rules judge, so a test can read what a rule asks.
    """
    families = frozenset(rule.family for rule in enabled if rule.family is not None)
    codes = frozenset(rule.code for rule in enabled)

    environment = Environment(
        options=options,
        families=families,
        receiver_calls=bool(options.receiver_roots) and not codes.isdisjoint(RECEIVER_CONSUMERS),
        receiver_separation=bool(options.receiver_roots) and "ZA3010" in codes,
        type_names=read_type_names(source.tree) if "ZA3063" in codes else frozenset(),
    )

    receivers_by_container: dict[int, ReceiverGroups] = {}

    def receivers_of(container: Container) -> ReceiverGroups:
        key = id(container)

        if key not in receivers_by_container:
            receivers_by_container[key] = find_receiver_groups(container, options)

        return receivers_by_container[key]

    bindings_by_container: dict[int, DependentBindingGroups] = {}

    def dependent_bindings_of(container: Container) -> DependentBindingGroups:
        key = id(container)

        if key not in bindings_by_container:
            bindings_by_container[key] = find_dependent_binding_groups(container)

        return bindings_by_container[key]

    sections_by_container: dict[int, frozenset[int]] = {}

    def protected_sections_of(container: Container) -> frozenset[int]:
        key = id(container)

        if key not in sections_by_container:
            sections = receivers_of(container).sections if "ZA3064" in codes else frozenset()

            if "ZA3061" in codes:
                sections |= dependent_bindings_of(container).sections

            sections_by_container[key] = sections

        return sections_by_container[key]

    runs_by_container: dict[tuple[int, bool], Runs] = {}

    def runs_for(typed: bool) -> Callable[[Container], Runs]:
        def runs_of(container: Container) -> Runs:
            key = (id(container), typed)

            if key not in runs_by_container:
                borders = receivers_of(container).borders if environment.receiver_separation else frozenset()

                borders |= protected_sections_of(container)
                runs_by_container[key] = find_runs(container, environment, typed=typed, borders=borders)

            return runs_by_container[key]

        return runs_of

    runs = _Runs(
        runs_for(typed=False), runs_for(typed=True), receivers_of, dependent_bindings_of, protected_sections_of
    )

    owned: dict[str, list[OwnedGap]] = {}

    for container in collect_containers(source, clauses=True, headers=True):
        judging = enabled
        pair_index = find_two_unit_gap(container)

        if pair_index is not None:
            pair_gap = container.gaps[pair_index]

            # A pair that a noqa comment silences at its gap is off there, so the relationship rules judge it.
            judge = None if pair is None or silenced(pair.code, container, pair_index) else pair

            if judge is not None and pair_gap.is_blank and len(pair_gap.blank_lines) > 0:
                owned.setdefault(judge.code, []).append(OwnedGap(container, pair_index))

            if judge is not None or not two_unit_rules:
                continue

            judging = two_unit_rules

        claimed = _read_claims(container, judging, environment, runs, silenced)

        if observer is not None:
            observer(container, claimed)

        _own_gaps(container, judging, claimed, owned)

    return owned


def _own_gaps(
    container: Container, judging: Sequence[Claimant], claimed: dict[str, list[bool]], owned: dict[str, list[OwnedGap]]
) -> None:
    """Gives each gap of a container to the first rule that asks for it, adding it to `owned` by the rule's code."""
    separators = [rule for rule in judging if rule.role == "separate"]
    keepers = [rule for rule in judging if rule.role != "separate"]
    removal_groups = _find_removal_groups(container, keepers, claimed)

    reported: set[int] = set()

    for index, gap in enumerate(container.gaps):
        if gap.is_joined or index in reported:
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
            indices = removal_groups.get(index, ()) if _removes_components(owner) else ()
            owned.setdefault(owner.code, []).append(OwnedGap(container, index, indices))

            # A grouped keeper reports its component once; a gather rule reports every gap, each fixing the whole run.
            if owner.removes_group:
                reported.update(indices)
