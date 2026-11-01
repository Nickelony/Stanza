"""ZA3501: names the arguments of a call that passes at least `named-argument-min-arguments` arguments."""

from __future__ import annotations

import ast

from nickelony_stanza.analysis.calls import ArgumentNames, Resolve, build_names, count_arguments, read_names
from nickelony_stanza.options import Options
from nickelony_stanza.rules.base import Rule


def _check(call: ast.Call, resolve: Resolve, options: Options) -> ArgumentNames | None:
    limit = options.named_argument_min_arguments

    if limit == 0 or not call.args or count_arguments(call) < limit:
        return None

    binding = resolve()

    if binding is None:
        return None

    names = read_names(binding, call)
    allow = options.named_argument_match == "allow"

    # The leading arguments that cannot take a name, or that carry their parameter's name under `allow`, stay unnamed.
    start = next(
        (
            index
            for index, (argument, name) in enumerate(zip(call.args, names, strict=True))
            if name is not None and not (allow and isinstance(argument, ast.Name) and argument.id == name)
        ),
        None,
    )

    return None if start is None else build_names(call, names, start)


RULE = Rule(
    code="ZA3501",
    name="name-call-arguments",
    level="strong",
    message="Name the arguments of this call.",
    description="Name the arguments of a call that passes at least named-argument-min-arguments arguments.",
    claim=None,
    check=_check,
)
