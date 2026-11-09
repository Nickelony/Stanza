"""Claims that more than one rule builds on."""

from __future__ import annotations

import ast
from typing import cast

from nickelony_stanza.analysis.classify import Family, read_boundary_families
from nickelony_stanza.analysis.model import MEMBER_KINDS, STATEMENT_KINDS, Container
from nickelony_stanza.analysis.session import Claim, ClaimContext
from nickelony_stanza.analysis.syntax import (
    ends_with_definition,
    is_definition,
    is_docstring_statement,
    is_guard,
    is_import,
    is_jump,
)


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

    if key is None or key != runs.keys[index + 1] or key.family not in context.environment.families:
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
    before = _is_tight_in_run(container, index - 1, context)
    after = _is_tight_in_run(container, index + 1, context)

    if before and after:
        return True

    # A lone unit has a blank line on its other side too, which sets it apart from a second section.
    if before:
        return _is_inside_run(container, index + 1, context) and _is_tight_in_run(container, index + 2, context)

    return after and _is_inside_run(container, index - 1, context) and _is_tight_in_run(container, index - 2, context)


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

    def is_apart(gap_index: int) -> bool:
        return container.gaps[gap_index].is_blank or context.is_claimed_by_others(gap_index)

    return (index == 0 or is_apart(index - 1)) and (index == len(container.gaps) or is_apart(index))


def _find_loose_gaps(container: Container, context: ClaimContext, limit: int) -> frozenset[int]:
    """Lists the gaps of the loose runs that hold more than `limit` units."""

    def is_loose(gap_index: int) -> bool:
        gap = container.gaps[gap_index]
        removable = len(gap.blank_lines) > 0 and gap.restriction is None and not gap.has_comment

        return removable and not context.is_claimed_by_others(gap_index)

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
