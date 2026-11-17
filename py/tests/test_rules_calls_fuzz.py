"""The call rules against the interpreter: a generated call or record binds its arguments the same way after the rules name them."""

from __future__ import annotations

import ast
import random
import typing

from nickelony_stanza import Config, Options, fix
from tests.test_binding_fuzz import _random_call, _random_parameters

# Which way a generated call reaches the signature, as in the resolver fuzz, plus a method read from its class.
_SHAPES = ("function", "self", "static", "class", "constructor", "unbound")

# The globals a generated argument may name, so an identifier can carry its parameter's name.
_GLOBALS = "".join(f"p{index} = {100 + index}\n" for index in range(8))


def _random_argument(rng: random.Random) -> str:
    argument = rng.choice(["1", "2", "True", "False", "None", "'s'", "p0", "p1", "p2", "p3", "-4", "[5]", "(6, 7)"])
    return f"({argument})" if rng.random() < 0.15 else argument


def _random_valid_call(rng: random.Random, parameters: str) -> list[str]:
    """Writes the arguments of a call the signature accepts: its required ones, and some of the optional ones."""
    arguments = typing.cast("ast.FunctionDef", ast.parse(f"def f({parameters}): pass").body[0]).args
    positional = [*arguments.posonlyargs, *arguments.args]
    required = len(positional) - len(arguments.defaults)
    surplus = 2 if arguments.vararg is not None else 0
    count = rng.randint(required, len(positional) + surplus)

    written = [_random_argument(rng) for _ in range(count)]
    filled = {argument.arg for argument in positional[:count]}
    optional = [argument.arg for argument in arguments.args if argument.arg not in filled]

    for argument, default in zip(arguments.kwonlyargs, arguments.kw_defaults, strict=True):
        if default is None or rng.random() < 0.5:
            written.append(f"{argument.arg}=9")

    names = [name for name in optional if not name.startswith("__") and rng.random() < 0.3]
    return [*written, *(f"{name}=8" for name in names)]


def _join(rng: random.Random, arguments: list[str]) -> str:
    """Lays the arguments out on one line or several, with comments and a trailing comma now and then."""
    if not arguments or rng.random() < 0.5:
        return ", ".join(arguments)

    lines = [f"    {argument},{'  # note' if rng.random() < 0.3 else ''}" for argument in arguments]
    return "\n" + "\n".join(lines) + "\n"


def _source(shape: str, parameters: str, arguments: str) -> str:
    """Writes a module whose target returns the arguments it bound, and stores what the call returned in `result`."""
    receiver = {"self": "self", "constructor": "self", "unbound": "self", "class": "cls"}.get(shape)
    signature = ", ".join(part for part in (receiver, parameters) if part)

    if shape == "function":
        return f"{_GLOBALS}def target({signature}):\n    return dict(locals())\nresult = target({arguments})\n"

    decorator = {"static": "    @staticmethod\n", "class": "    @classmethod\n"}.get(shape, "")
    name = "__init__" if shape == "constructor" else "target"
    body = "self.seen = dict(locals())" if shape == "constructor" else "return dict(locals())"
    text = f"{_GLOBALS}class C:\n{decorator}    def {name}({signature}):\n        {body}\n"

    if shape == "self":
        return f"{text}    def run(self):\n        return self.target({arguments})\nresult = C().run()\n"

    if shape == "constructor":
        return f"{text}result = C({arguments}).seen\n"

    if shape == "unbound":
        return f"{text}result = C.target(C(), {arguments})\n" if arguments else f"{text}result = C.target(C())\n"

    return f"{text}result = C.target({arguments})\n"


def _run(text: str) -> dict[str, typing.Any] | None:
    """Runs a generated module and reads the arguments its target bound, or None when the call is refused."""
    namespace: dict[str, typing.Any] = {"__name__": "generated"}

    try:
        exec(compile(text, "generated.py", "exec", dont_inherit=True), namespace)
    except TypeError:
        return None

    return {name: value for name, value in namespace["result"].items() if name not in ("self", "cls")}


def test_a_named_call_binds_its_arguments_as_the_call_it_replaces() -> None:
    rng = random.Random(3501)
    disagreements: list[str] = []
    named = 0

    for case in range(1200):
        shape = _SHAPES[case % len(_SHAPES)]
        parameters, keywords = _random_parameters(rng)
        positional, chosen = _random_call(rng, keywords, "**extra" in parameters)

        if case % 2:
            written = [*(_random_argument(rng) for _ in range(positional)), *(f"{name}=9" for name in chosen)]
        else:
            written = _random_valid_call(rng, parameters)

        text = _source(shape, parameters, _join(rng, written))

        options = Options(
            named_argument_min_arguments=rng.choice([1, 2, 4]),
            named_argument_match=rng.choice(["allow", "require"]),
            named_argument_literals=rng.choice(["bool", "keywords"]),
        )

        fixed = fix(text, Config.create("none", enable=["ZA3501", "ZA3502"], options=options)).text
        before = _run(text)

        if before is None or fixed == text:
            continue

        named += 1

        if _run(fixed) != before:
            disagreements.append(f"{text}\n--- became ---\n{fixed}")

    assert disagreements == []
    assert named > 200


_FIELD_NAMES = ("a", "b", "c", "d", "e")
_RECORD_HEADERS = {"dataclass": "@dataclass\nclass C:\n", "named tuple": "class C(NamedTuple):\n"}


def _random_record(rng: random.Random, kind: str) -> tuple[str, int]:
    """Writes a dataclass or a typed named tuple whose body may repeat an annotation or replace the annotation mapping, and counts its names."""
    names = [rng.choice(_FIELD_NAMES) for _ in range(rng.randint(2, 5))]
    defaulted = rng.randint(1, len(names) + 1)

    lines: list[str] = []

    for index, name in enumerate(names):
        default = " = 0" if index >= defaulted else ""
        lines.append(f"    {name}: {rng.choice(['int', 'str', 'bool'])}{default}")

    replaced = rng.choice(["", "", "", "assign", "update", "delete"])

    if replaced == "assign":
        lines.append(f"    __annotations__ = {{{', '.join(f'{name!r}: int' for name in _FIELD_NAMES[:3])}}}")
    elif replaced == "update":
        lines.append("    __annotations__.update(z=int)")
    elif replaced == "delete":
        lines.append("    del __annotations__")

    return _RECORD_HEADERS[kind] + "\n".join(lines) + "\n", len(set(names))


def _run_record(text: str) -> dict[str, typing.Any] | None:
    """Runs a generated record and reads its fields, or None when the declaration or the call is refused."""
    namespace: dict[str, typing.Any] = {"__name__": "generated"}

    try:
        exec(compile(text, "generated.py", "exec", dont_inherit=True), namespace)
    except (TypeError, NameError, AttributeError, KeyError):
        return None

    made = namespace["made"]
    return dict(made._asdict() if hasattr(made, "_asdict") else vars(made))


def test_a_named_call_of_a_generated_record_binds_its_arguments_as_the_call_it_replaces() -> None:
    rng = random.Random(3502)
    disagreements: list[str] = []
    named = 0

    for case in range(600):
        kind = "dataclass" if case % 2 else "named tuple"
        declaration, fields = _random_record(rng, kind)
        count = fields if rng.random() < 0.7 else rng.randint(1, 5)
        arguments = ", ".join(rng.choice(["True", "False", "1", "2", "'s'", "p0"]) for _ in range(count))
        text = f"from dataclasses import dataclass\nfrom typing import NamedTuple\n{_GLOBALS}{declaration}made = C({arguments})\n"
        options = Options(named_argument_min_arguments=rng.choice([1, 2, 4]), named_argument_match="allow")

        fixed = fix(text, Config.create("none", enable=["ZA3501", "ZA3502"], options=options)).text
        before = _run_record(text)

        if before is None or fixed == text:
            continue

        named += 1

        if _run_record(fixed) != before:
            disagreements.append(f"{text}\n--- became ---\n{fixed}")

    assert disagreements == []
    assert named > 150
