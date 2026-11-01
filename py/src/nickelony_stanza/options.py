"""The settings that change how the rules read a statement or a call."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Literal, get_args

DEFAULT_ASSERTION_ROOTS = ("assert*", "expect", "pytest.raises", "pytest.warns", "pytest.deprecated_call")

DEFAULT_SUBSCRIPTION_ROOTS = (
    "connect",
    "disconnect",
    "subscribe",
    "unsubscribe",
    "add_listener",
    "remove_listener",
    "add_done_callback",
)

# The most statements or members a loose run holds before the gather rules join it.
DEFAULT_MAX_LOOSE_RUN_SIZE = 3
MAX_LOOSE_RUN_SIZE_LIMIT = 1000

# The fewest arguments a call passes before ZA3501 names them, where 0 turns the rule off.
DEFAULT_NAMED_ARGUMENT_MIN_ARGUMENTS = 4
NAMED_ARGUMENT_MIN_ARGUMENTS_LIMIT = 1000

# Whether an identifier that carries its parameter's name may stay unnamed.
NamedArgumentMatch = Literal["allow", "require"]

DEFAULT_NAMED_ARGUMENT_MATCH: NamedArgumentMatch = "allow"

# Which literals ZA3502 names: `True` and `False`, or `None` too.
NamedArgumentLiterals = Literal["bool", "keywords"]
DEFAULT_NAMED_ARGUMENT_LITERALS: NamedArgumentLiterals = "keywords"

# Which methods a call through `self`, `cls` or `super()` may bind to: every name, the `_private` ones, or none.
NamedArgumentMethods = Literal["all", "private", "none"]
DEFAULT_NAMED_ARGUMENT_METHODS: NamedArgumentMethods = "all"

# Where a callee may be defined for its call to be named: the project, the standard library or an installed package.
NamedArgumentSource = Literal["project", "stdlib", "installed"]
NAMED_ARGUMENT_SOURCES: tuple[NamedArgumentSource, ...] = get_args(NamedArgumentSource)
DEFAULT_NAMED_ARGUMENT_SOURCES = ("project", "stdlib", "installed")


@dataclass(frozen=True)
class Options:
    """The options of a run, shared by the rules so they read a statement and a call the same way."""

    assertion_roots: tuple[str, ...] = DEFAULT_ASSERTION_ROOTS
    guard_roots: tuple[str, ...] = ()
    subscription_roots: tuple[str, ...] = DEFAULT_SUBSCRIPTION_ROOTS
    max_loose_run_size: int = DEFAULT_MAX_LOOSE_RUN_SIZE
    named_argument_min_arguments: int = DEFAULT_NAMED_ARGUMENT_MIN_ARGUMENTS
    named_argument_match: NamedArgumentMatch = DEFAULT_NAMED_ARGUMENT_MATCH
    named_argument_literals: NamedArgumentLiterals = DEFAULT_NAMED_ARGUMENT_LITERALS
    named_argument_sources: tuple[str, ...] = DEFAULT_NAMED_ARGUMENT_SOURCES
    named_argument_methods: NamedArgumentMethods = DEFAULT_NAMED_ARGUMENT_METHODS
    named_argument_ignore: tuple[str, ...] = ()

    # The directories the resolver reads as the project, absolute once loaded; empty detects them from each file.
    source_roots: tuple[str, ...] = ()
