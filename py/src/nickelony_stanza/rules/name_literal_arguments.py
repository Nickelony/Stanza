"""ZA3502: names the `True`, `False` and `None` arguments of a call that passes two or more arguments."""

from __future__ import annotations

import ast

from nickelony_stanza.analysis.calls import ArgumentNames, Resolve, build_names, count_arguments, read_names
from nickelony_stanza.options import Options
from nickelony_stanza.rules.base import Rule


def _is_literal(argument: ast.expr, options: Options) -> bool:
    if not isinstance(argument, ast.Constant):
        return False

    return (
        argument.value is True
        or argument.value is False
        or (argument.value is None and options.named_argument_literals == "keywords")
    )


def _check(call: ast.Call, resolve: Resolve, options: Options) -> ArgumentNames | None:
    if count_arguments(call) < 2 or not any(_is_literal(argument, options) for argument in call.args):
        return None

    binding = resolve()

    if binding is None:
        return None

    names = read_names(binding, call)

    # The first literal that can take a name and every argument after it, since no positional argument follows a name.
    start = next(
        (
            index
            for index, (argument, name) in enumerate(zip(call.args, names, strict=True))
            if name is not None and _is_literal(argument, options)
        ),
        None,
    )

    return None if start is None else build_names(call, names, start)


RULE = Rule(
    code="ZA3502",
    name="name-literal-arguments",
    level="strong",
    message="Name the literal arguments of this call.",
    description="Name the True, False and None arguments of a call that passes two or more arguments.",
    claim=None,
    check=_check,
)
