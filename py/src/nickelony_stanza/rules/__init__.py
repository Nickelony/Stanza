"""The rules, the order that decides which of them reports a gap several of them ask for, and the call rules."""

from __future__ import annotations

from nickelony_stanza.rules import (
    gather_loose_members,
    gather_loose_statements,
    keep_clause_keywords_adjacent,
    keep_member_runs_together,
    keep_multi_line_cases_together,
    keep_single_line_cases_together,
    keep_statement_runs_together,
    keep_two_units_together,
    name_call_arguments,
    name_literal_arguments,
    remove_block_padding,
    separate_assertion_runs,
    separate_assignment_runs,
    separate_await_runs,
    separate_base_call,
    separate_clause_keywords,
    separate_commented_siblings,
    separate_declaration_runs,
    separate_file_header,
    separate_guard_clauses,
    separate_import_section,
    separate_invocation_runs,
    separate_jump_statements,
    separate_member_groups,
    separate_multi_line_cases,
    separate_multi_line_constructs,
    separate_same_type_declaration_runs,
    separate_subscription_runs,
)
from nickelony_stanza.rules.base import LEVELS, Level, Rule

PAIR_RULE = keep_two_units_together.RULE

# The rules that add a blank line, in the order that decides which of them reports a gap several of them ask for.
OWNER_ORDER: tuple[Rule, ...] = (
    separate_file_header.RULE,
    separate_import_section.RULE,
    separate_multi_line_constructs.RULE,
    separate_multi_line_cases.RULE,
    separate_commented_siblings.RULE,
    separate_clause_keywords.RULE,
    separate_base_call.RULE,
    separate_guard_clauses.RULE,
    separate_jump_statements.RULE,
    separate_member_groups.RULE,
    separate_declaration_runs.RULE,
    separate_same_type_declaration_runs.RULE,
    separate_assignment_runs.RULE,
    separate_assertion_runs.RULE,
    separate_await_runs.RULE,
    separate_subscription_runs.RULE,
    separate_invocation_runs.RULE,
)

# The rules that remove a blank line, in the order that decides which of them reports a gap several of them ask for.
KEEP_ORDER: tuple[Rule, ...] = (
    PAIR_RULE,
    keep_clause_keywords_adjacent.RULE,
    remove_block_padding.RULE,
    keep_multi_line_cases_together.RULE,
    keep_single_line_cases_together.RULE,
    keep_statement_runs_together.RULE,
    keep_member_runs_together.RULE,
    gather_loose_statements.RULE,
    gather_loose_members.RULE,
)

# The rules that name the arguments of a call, in the order that decides which of them reports a call both ask for.
CALL_RULES: tuple[Rule, ...] = (name_call_arguments.RULE, name_literal_arguments.RULE)

ALL_RULES: tuple[Rule, ...] = tuple(sorted((*OWNER_ORDER, *KEEP_ORDER, *CALL_RULES), key=lambda rule: rule.code))

BY_CODE = {rule.code: rule for rule in ALL_RULES}
BY_NAME = {rule.name: rule for rule in ALL_RULES}


def resolve_rule(identifier: str) -> Rule:
    """Finds a rule by its code or its name, ignoring case."""
    key = identifier.strip()
    rule = BY_CODE.get(key.upper()) or BY_NAME.get(key.lower())

    if rule is None:
        raise KeyError(identifier)

    return rule


def rules_of_level(level: Level) -> tuple[Rule, ...]:
    """Lists the rules a level enables: those of every level up to it, and none of the opt-in rules."""
    reach = LEVELS.index(level)
    return tuple(rule for rule in ALL_RULES if rule.level != "opt-in" and LEVELS.index(rule.level) <= reach)
