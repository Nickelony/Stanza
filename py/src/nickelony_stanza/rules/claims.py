"""Claims that more than one rule builds on."""

from __future__ import annotations

import ast
from dataclasses import replace
from typing import cast

from nickelony_stanza.analysis.classify import Family, read_boundary_families
from nickelony_stanza.analysis.model import MEMBER_KINDS, STATEMENT_KINDS, Container
from nickelony_stanza.analysis.sections import is_section_break
from nickelony_stanza.analysis.session import Claim, ClaimContext
from nickelony_stanza.analysis.syntax import (
    bound_names,
    ends_with_definition,
    is_assertion,
    is_definition,
    is_docstring_statement,
    is_guard,
    is_import,
    is_jump,
    read_value_names,
)
from nickelony_stanza.options import Options


def _narrowed_names(test: ast.expr) -> set[str]:
    """Reads the names an assert test narrows: `x`, `x is None`, `x is not None`, `isinstance(x, T)` or their `and`."""
    if isinstance(test, ast.BoolOp) and isinstance(test.op, ast.And):
        return {name for value in test.values for name in _narrowed_names(value)}

    if isinstance(test, ast.Compare):
        is_none_check = (
            len(test.ops) == 1
            and isinstance(test.ops[0], ast.Is | ast.IsNot)
            and isinstance(test.comparators[0], ast.Constant)
            and test.comparators[0].value is None
        )

        return _narrowed_names(test.left) if is_none_check else set()

    if isinstance(test, ast.Call) and isinstance(test.func, ast.Name) and test.func.id == "isinstance" and test.args:
        return _narrowed_names(test.args[0])

    return {test.id} if isinstance(test, ast.Name) else set()


def _assertion_phase_continues(container: Container, index: int, options: Options) -> bool:
    """Tells whether the next single-line assertion shares this assertion's unobstructed phase."""
    if not 0 <= index < len(container.gaps):
        return False

    gap = container.gaps[index]

    return (
        is_assertion(container.units[index + 1].node, options)
        and not container.units[index + 1].is_multi_line
        and not gap.has_comment
        and not gap.is_joined
        and gap.restriction is None
    )


def narrowed_by_assert(container: Container, index: int, options: Options, *, include_run: bool = False) -> set[str]:
    """
    Reads the names a single-line assert at `index` narrows, or none for any other unit.

    An assert that leads a run of assertions opens the assert phase of a test rather than narrowing for the checker,
    so it narrows none.
    """
    unit = container.units[index]

    if not isinstance(unit.node, ast.Assert) or unit.is_multi_line:
        return set()

    if not include_run and _assertion_phase_continues(container, index, options):
        return set()

    return _narrowed_names(unit.node.test)


def read_by_guard(container: Container, index: int, options: Options) -> set[str]:
    """Reads the names the condition of a guard `if` at `index` reads, or none for any other unit."""
    node = container.units[index].node

    if not isinstance(node, ast.If) or not is_guard(node, options):
        return set()

    return read_value_names(node.test)


def claim_run_boundary(family: Family) -> Claim:
    """Builds the claim of a rule that sets the runs of two or more units of one family apart from their neighbors."""
    kinds = MEMBER_KINDS if family == "member" else STATEMENT_KINDS

    return lambda container, index, context: (
        container.kind in kinds and family in read_boundary_families(context.runs_of(container), index)
    )


def find_guard_run(container: Container, context: ClaimContext) -> tuple[int, int]:
    """
    Finds where the guards that open a body begin and end, as the indexes of the first guard and of the unit past them.

    The docstring of a function comes first and is no guard. The answer is read once for the container,
    so the rules that judge each gap of it stay linear.
    """
    cached = context.memo.get("guard-run")

    if cached is not None:
        return cast("tuple[int, int]", cached)

    units = container.units
    start = 1 if container.kind == "function" and is_docstring_statement(units[0].node) else 0
    end = start

    while end < len(units) and is_guard(units[end].node, context.environment.options):
        end += 1

    context.memo["guard-run"] = (start, end)

    return start, end


def claim_clause_gap(container: Container, index: int, context: ClaimContext) -> bool:
    """Asks for the gap above a clause keyword, which both rules of the clause pair judge."""
    return container.kind == "clause"


def claim_case_gap(*, multi_line: bool) -> Claim:
    """Builds the claim of a rule that keeps two `case` clauses together: one that spans lines, or two that do not."""

    def claim(container: Container, index: int, context: ClaimContext) -> bool:
        previous, following = container.units[index], container.units[index + 1]

        if container.kind != "match" or (previous.is_multi_line or following.is_multi_line) != multi_line:
            return False

        # Black writes a blank line below a case that ends in a function or class, so the gap is not free.
        return not (isinstance(previous.node, ast.match_case) and ends_with_definition(previous.node.body[-1]))

    return claim


def _joins_as_one_run(
    container: Container, index: int, context: ClaimContext, *, joined_is_tight: bool = False
) -> bool:
    """Tells whether the grouping rules read the two single-line units at a gap as one run with nothing else between."""
    if index < 0 or index >= len(container.gaps):
        return False

    previous, following = container.units[index], container.units[index + 1]
    gap = container.gaps[index]

    if previous.is_multi_line or following.is_multi_line or gap.has_comment or gap.restriction is not None:
        return False

    # A joined gap, such as two units on one line, holds no blank line, so it stands tight but is never removed.
    if gap.is_joined and not joined_is_tight:
        return False

    runs = context.runs_of(container)
    key = runs.keys[index]

    if (
        key is None
        or key != runs.keys[index + 1]
        or runs.starts[index] != runs.starts[index + 1]
        or key.family not in context.environment.families
    ):
        return False

    # Where declarations are told apart by type, two of different types are two runs.
    if key.family == "declaration" and "typed-declaration" in context.environment.families:
        typed = context.typed_runs_of(container)

        if typed.keys[index] != typed.keys[index + 1]:
            return False

    return not context.is_claimed_by_others(index)


def _is_inside_run(container: Container, index: int, context: ClaimContext) -> bool:
    """Tells whether a gap with a blank line lies inside a run, before any section is set apart."""
    return _joins_as_one_run(container, index, context) and len(container.gaps[index].blank_lines) > 0


def _is_tight_in_run(container: Container, index: int, context: ClaimContext) -> bool:
    """Tells whether a gap without a blank line joins two units of one run."""
    return _joins_as_one_run(container, index, context, joined_is_tight=True) and not container.gaps[index].is_blank


def _is_section_break(container: Container, index: int, context: ClaimContext) -> bool:
    """Tells whether a blank line sets two sections apart, a section being the tight units of one run."""
    return container.gaps[index].is_blank and is_section_break(
        index,
        lambda gap_index: _is_tight_in_run(container, gap_index, context),
        lambda gap_index: _is_inside_run(container, gap_index, context),
    )


def claim_kept_run(kinds: frozenset[str]) -> Claim:
    """Builds the claim of a rule that keeps the single-line units of a run together, except between sections."""

    def claim(container: Container, index: int, context: ClaimContext) -> bool:
        if container.kind not in kinds or not _joins_as_one_run(container, index, context):
            return False

        return not _is_section_break(container, index, context)

    return claim


def _is_loose_candidate(container: Container, index: int, context: ClaimContext) -> bool:
    """Tells whether a unit can join a loose run: a single-line statement that no neighbor sits tight against."""
    unit = container.units[index]
    node = unit.node

    if not isinstance(node, ast.stmt) or unit.is_multi_line or is_jump(node) or is_import(node) or is_definition(node):
        return False

    # A gap a keeper joins is tight once fixed, so it reads tight before that fix too.
    def is_apart(gap_index: int) -> bool:
        stays_blank = container.gaps[gap_index].is_blank and not context.is_kept(gap_index)
        return stays_blank or context.is_claimed_by_others(gap_index)

    return (index == 0 or is_apart(index - 1)) and (index == len(container.gaps) or is_apart(index))


def _find_loose_gaps(container: Container, context: ClaimContext, limit: int) -> frozenset[int]:
    """Lists the gaps of the loose runs that hold more than `limit` units."""

    def is_loose(gap_index: int) -> bool:
        gap = container.gaps[gap_index]
        removable = len(gap.blank_lines) > 0 and gap.restriction is None and not gap.has_comment

        protected = gap_index in context.protected_sections_of(container)

        return (
            removable
            and not protected
            and not context.is_kept(gap_index)
            and not context.is_claimed_by_others(gap_index)
        )

    gaps: set[int] = set()
    start = 0

    while start < len(container.units):
        if not _is_loose_candidate(container, start, context):
            start += 1
            continue

        end = start

        while end + 1 < len(container.units) and is_loose(end) and _is_loose_candidate(container, end + 1, context):
            end += 1

        if end - start + 1 > limit:
            gaps.update(range(start, end))

        start = end + 1

    return frozenset(gaps)


def claim_loose_run(kinds: frozenset[str]) -> Claim:
    """Builds the claim of a rule that gathers a loose run of units that holds more than `max-loose-run-size`."""

    def claim(container: Container, index: int, context: ClaimContext) -> bool:
        limit = context.environment.options.max_loose_run_size

        if container.kind not in kinds or limit == 0:
            return False

        if "loose-run" not in context.memo:
            context.memo["loose-run"] = _find_loose_gaps(container, context, limit)

        gaps = context.memo["loose-run"]
        return isinstance(gaps, frozenset) and index in gaps

    return claim


def claim_jump_gap(container: Container, index: int, context: ClaimContext) -> bool:
    following = container.units[index + 1]

    if container.kind not in STATEMENT_KINDS or not is_jump(following.node):
        return False

    # The stanza reaches through tight gaps and the joins strong keepers will remove in this pass.
    # It spans two lines once a reached unit starts above the last line of the unit beside the jump.
    last_line = container.units[index].last_line
    start = index

    while last_line - container.units[start].first_line < 1:
        if (
            start == 0
            or (container.gaps[start - 1].is_blank and not context.is_kept(start - 1))
            or (context.is_claimed_by_others(start - 1) and not context.is_kept(start - 1))
        ):
            return False

        start -= 1

    return True


def _read_checked_names(container: Container, index: int, options: Options) -> set[str] | None:
    unit = container.units[index]
    node = unit.node

    if unit.is_multi_line and is_assertion(node, options):
        return None

    if isinstance(node, ast.Assert):
        return narrowed_by_assert(container, index, options, include_run=True) or None

    if not is_guard(node, options):
        return None

    if isinstance(node, ast.If):
        return read_by_guard(container, index, options)

    return read_value_names(node.value) if isinstance(node, ast.Expr) else set()


def read_checked_stanza_gaps(
    container: Container, options: Options
) -> tuple[frozenset[int], frozenset[int], frozenset[int]]:
    """Reads the outer borders and inner binding gaps of checked stanzas, uncached: the test seam of their cost."""
    borders: set[int] = set()
    guarded_bindings: set[int] = set()
    asserted_bindings: set[int] = set()

    checks = [_read_checked_names(container, index, options) for index in range(len(container.units))]

    # A mixed assertion run is a test phase; only an all-narrowing run is a checked stanza.
    assertion_start = 0

    while assertion_start < len(container.units):
        if (
            not is_assertion(container.units[assertion_start].node, options)
            or container.units[assertion_start].is_multi_line
        ):
            assertion_start += 1
            continue

        assertion_end = assertion_start + 1

        while _assertion_phase_continues(container, assertion_end - 1, options):
            assertion_end += 1

        if any(checks[index] is None for index in range(assertion_start, assertion_end)):
            checks[assertion_start:assertion_end] = [None] * (assertion_end - assertion_start)

        assertion_start = assertion_end

    index = 0

    while index < len(container.units):
        checked = checks[index]

        if checked is None:
            index += 1
            continue

        start = index
        end = index

        # A stanza an assert opens hands its bindings to the assert rule; an assert-only stanza takes no border below.
        opens_with_assert = isinstance(container.units[index].node, ast.Assert)
        assert_only = opens_with_assert

        while (
            end < len(container.gaps)
            and not container.gaps[end].has_comment
            and not container.gaps[end].is_joined
            and container.gaps[end].restriction is None
        ):
            following = checks[end + 1]

            if following is None:
                break

            end += 1
            checked.update(following)
            assert_only = assert_only and isinstance(container.units[end].node, ast.Assert)

        while start > 0:
            gap = container.gaps[start - 1]

            if gap.has_comment or gap.is_joined or gap.restriction is not None:
                break

            if checked.isdisjoint(bound_names(container.units[start - 1].node)):
                break

            start -= 1

        (asserted_bindings if opens_with_assert else guarded_bindings).update(range(start, index))

        if start > 0:
            borders.add(start - 1)

        if end < len(container.gaps) and not assert_only:
            borders.add(end)

        index = end + 1

    return frozenset(borders), frozenset(guarded_bindings), frozenset(asserted_bindings)


def find_checked_stanza_gaps(
    container: Container, context: ClaimContext
) -> tuple[frozenset[int], frozenset[int], frozenset[int]]:
    """Finds outer borders and internal binding gaps of guard and narrowing-assert stanzas."""
    if container.kind not in STATEMENT_KINDS:
        return frozenset(), frozenset(), frozenset()

    if "checked-stanzas" not in context.memo:
        context.memo["checked-stanzas"] = read_checked_stanza_gaps(container, context.environment.options)

    return cast("tuple[frozenset[int], frozenset[int], frozenset[int]]", context.memo["checked-stanzas"])


def is_stanza_anchor_stable(
    container: Container, index: int, context: ClaimContext, candidates: frozenset[int], memo_key: str
) -> bool:
    """
    Tells whether run keeping leaves a candidate binding's upper stanza boundary intact.

    Gathering never takes that boundary, because the gap a keeper joins makes its binding no loose statement.
    """
    if index == 0:
        return True

    if context.is_held_by("ZA3064", index - 1):
        return False

    # A run keeper reads the borders before a displaced separation moves, so a kept candidate is no border to it.
    def is_run_border(gap_index: int) -> bool:
        return gap_index not in candidates and context.is_claimed_by_others(gap_index)

    memo = cast("dict[str, object]", context.memo.setdefault(memo_key, {}))

    return not (
        "ZA3054" in context.enabled_codes
        and claim_kept_run(STATEMENT_KINDS)(
            container, index - 1, replace(context, memo=memo, is_claimed_by_others=is_run_border)
        )
    )
