"""The call resolver against `inspect`: generated signatures crossed with calls, and a fixed sample of the standard library."""

from __future__ import annotations

import ast
import importlib
import inspect
import random
import typing
from pathlib import Path

import pytest

from nickelony_stanza.analysis.binding import Binding, ModuleContext, Reason, Skipped, SourceIndex, resolve_call
from tests.test_binding import ALL, find_call, write_tree

_NAMES = [f"p{index}" for index in range(8)]

# Which way a generated call reaches the signature: by name, through `self`, through the class, or as a constructor.
_SHAPES = ("function", "self", "static", "class", "constructor")


def _random_parameters(rng: random.Random) -> tuple[str, list[str]]:
    names = rng.sample(_NAMES, 7)
    positional_only = names[: rng.randint(0, 2)]
    regular = names[len(positional_only) : len(positional_only) + rng.randint(0, 3)]

    if regular and rng.random() < 0.2:
        regular[-1] = f"__{regular[-1]}"

    keyword_only = names[len(positional_only) + len(regular) : len(positional_only) + len(regular) + rng.randint(0, 2)]
    positional = [*positional_only, *regular]
    defaults = rng.randint(0, len(positional))
    parts = [f"{name}=0" if index >= len(positional) - defaults else name for index, name in enumerate(positional)]

    if positional_only:
        parts.insert(len(positional_only), "/")

    if rng.random() < 0.4:
        parts.append("*rest")
    elif keyword_only:
        parts.append("*")

    parts.extend(f"{name}=0" if rng.random() < 0.5 else name for name in keyword_only)

    if rng.random() < 0.3:
        parts.append("**extra")

    keywords = [name for name in [*regular, *keyword_only] if not name.startswith("__")]
    return ", ".join(parts), keywords


def _random_call(rng: random.Random, keywords: list[str], has_extra: bool) -> tuple[int, list[str]]:
    chosen = [name for name in keywords if rng.random() < 0.3]

    if has_extra and rng.random() < 0.3:
        chosen.append("zz")

    return rng.randint(0, 5), chosen


def _source(shape: str, parameters: str, call: str) -> str:
    receiver = "self, " if shape in ("self", "constructor") else "cls, " if shape == "class" else ""
    receiver = receiver if parameters else receiver.rstrip(", ")

    if shape == "function":
        return f"def target({parameters}): pass\n{call}\n"

    decorator = {"static": "    @staticmethod\n", "class": "    @classmethod\n"}.get(shape, "")
    name = "__init__" if shape == "constructor" else "target"
    body = f"class C:\n{decorator}    def {name}({receiver}{parameters}): pass\n"

    if shape == "self":
        return f"{body}    def run(self):\n        {call}\n"

    return f"{body}{call}\n"


def _runtime_target(namespace: dict[str, typing.Any], shape: str) -> typing.Any:
    if shape == "function":
        return namespace["target"]

    if shape == "constructor":
        return namespace["C"]

    if shape == "self":
        return namespace["C"]().target

    return namespace["C"].target


def _check(
    binding: Binding | Skipped, signature: inspect.Signature, positional: int, keywords: list[str]
) -> str | None:
    values = [object() for _ in range(positional)]
    named = {name: object() for name in keywords}

    try:
        bound = signature.bind_partial(*values, **named)
    except TypeError:
        bound = None

    landed: list[str | None] = []

    for value in values:
        holder = (
            None if bound is None else next((name for name, held in bound.arguments.items() if held is value), None)
        )

        landed.append(holder)

    if isinstance(binding, Skipped):
        if binding.reason != Reason.CALL_SHAPE:
            return f"skipped for {binding.reason}"

        return None if bound is None or None in landed else "skipped a call that binds"

    if bound is None or None in landed:
        return "bound a call that cannot be named"

    parameters = signature.parameters

    for index, name in enumerate(binding.names_for(positional)):
        holder = landed[index]
        assert holder is not None

        nameable = parameters[holder].kind == inspect.Parameter.POSITIONAL_OR_KEYWORD and not holder.startswith(
            ("_C__", "__")
        )

        if name is None and nameable:
            return f"left argument {index} unnamed"

        if name is not None and (name != holder or not nameable):
            return f"named argument {index} {name}, which binds to {holder}"

    first = next((index for index, name in enumerate(binding.names_for(positional)) if name is not None), positional)

    if all(name is not None for name in binding.names_for(positional)[first:]):
        moved = dict(zip(binding.names_for(positional)[first:], values[first:], strict=True))
        rebound = signature.bind_partial(*values[:first], **{str(key): value for key, value in moved.items()}, **named)

        if rebound.arguments != bound.arguments:
            return "naming the arguments binds them elsewhere"

    return None


def test_generated_signatures_bind_as_inspect_binds_them(tmp_path: Path) -> None:
    rng = random.Random(3501)
    index = SourceIndex([tmp_path], installed=())

    disagreements: list[str] = []
    bound = 0

    for case in range(600):
        shape = _SHAPES[case % len(_SHAPES)]
        parameters, keywords = _random_parameters(rng)
        positional, chosen = _random_call(rng, keywords, "**extra" in parameters)

        arguments = ", ".join(
            [*(str(index) for index in range(positional)), *(f"{name}={index}" for index, name in enumerate(chosen))]
        )

        callee = {"function": "target", "self": "self.target", "constructor": "C"}.get(shape, "C.target")
        text = _source(shape, parameters, f"{callee}({arguments})")
        path = tmp_path / f"case{case}.py"

        write_tree(tmp_path, {path.name: text})

        namespace: dict[str, typing.Any] = {"__name__": f"case{case}"}
        definition = text if shape == "self" else text.removesuffix(f"{callee}({arguments})\n")

        exec(compile(definition, str(path), "exec", dont_inherit=True), namespace)

        tree = ast.parse(text)
        result = resolve_call(find_call(tree, callee), ModuleContext.build(tree, path, text), index, ALL)
        problem = _check(result, inspect.signature(_runtime_target(namespace, shape)), positional, chosen)

        bound += isinstance(result, Binding)

        if problem is not None:
            disagreements.append(f"{problem}:\n{text}")

    assert disagreements == []
    assert bound > 250


def _random_dataclass(rng: random.Random, name: str, base: str | None) -> str:
    keyword = ", kw_only=True" if rng.random() < 0.3 else ""
    lines = [f"@dataclass(frozen={rng.random() < 0.5}{keyword})", f"class {name}({base or ''}):"]
    sentinel = rng.randint(0, 6) if rng.random() < 0.3 else -1

    for position in range(rng.randint(1, 5)):
        if position == sentinel:
            lines.append("    _: KW_ONLY")

        field_name = f"{name.lower()}{position}" if rng.random() < 0.8 else "shared"
        annotation = rng.choice(["int", "int", "ClassVar[int]", "InitVar[int]"])

        value = rng.choice(
            [
                "",
                " = 0",
                " = field(default=0)",
                " = field(init=False, default=0)",
                " = field(kw_only=True)",
                " = field(default=0, kw_only=False)",
            ]
        )

        if annotation.startswith("ClassVar"):
            value = " = 0"

        lines.append(f"    {field_name}: {annotation}{value}")

    return "\n".join(lines) + "\n"


def test_generated_dataclasses_take_the_parameters_inspect_reads(tmp_path: Path) -> None:
    rng = random.Random(3502)
    index = SourceIndex([tmp_path], installed=())
    header = "from dataclasses import dataclass, field, KW_ONLY, InitVar\nfrom typing import ClassVar\n"

    disagreements: list[str] = []
    checked = 0

    for case in range(300):
        base = _random_dataclass(rng, "Base", None) if rng.random() < 0.6 else ""
        text = header + base + _random_dataclass(rng, "Child", "Base" if base else None) + "Child()\n"

        namespace: dict[str, typing.Any] = {"__name__": __name__}

        try:
            exec(compile(text.replace("Child()\n", ""), f"dataclass{case}", "exec", dont_inherit=True), namespace)
        except (TypeError, ValueError):
            continue

        checked += 1
        path = tmp_path / f"dataclass{case}.py"
        write_tree(tmp_path, {path.name: text})

        tree = ast.parse(text)
        result = resolve_call(find_call(tree, "Child"), ModuleContext.build(tree, path, text), index, ALL)

        expected = [
            (parameter.name, parameter.kind.name.lower(), parameter.default is not inspect.Parameter.empty)
            for parameter in inspect.signature(namespace["Child"]).parameters.values()
        ]

        actual = (
            None if isinstance(result, Skipped) else [(p.name, p.kind.value, p.has_default) for p in result.parameters]
        )

        if actual != expected:
            disagreements.append(f"{actual} != {expected}:\n{text}")

    assert checked > 100
    assert disagreements == []


_STDLIB_SAMPLE = (
    "argparse",
    "base64",
    "calendar",
    "colorsys",
    "configparser",
    "csv",
    "difflib",
    "email.utils",
    "filecmp",
    "fnmatch",
    "ftplib",
    "getopt",
    "gettext",
    "glob",
    "gzip",
    "html",
    "ipaddress",
    "netrc",
    "optparse",
    "pprint",
    "quopri",
    "sched",
    "shlex",
    "shutil",
    "statistics",
    "string",
    "textwrap",
    "wave",
)


@pytest.mark.parametrize("module_name", _STDLIB_SAMPLE)
def test_a_standard_library_callee_binds_to_the_signature_inspect_reads(tmp_path: Path, module_name: str) -> None:
    index = SourceIndex([tmp_path], installed=())
    module = importlib.import_module(module_name)
    source = Path(inspect.getfile(module)).read_text(encoding="utf-8")

    names = [
        statement.name
        for statement in ast.parse(source).body
        if isinstance(statement, ast.FunctionDef | ast.ClassDef) and not statement.name.startswith("_")
    ]

    text = f"import {module_name}\n" + "".join(f"{module_name}.{name}()\n" for name in names)

    write_tree(tmp_path, {"main.py": text})

    tree = ast.parse(text)
    context = ModuleContext.build(tree, tmp_path / "main.py", text)

    disagreements: list[str] = []
    bound = 0

    for name in names:
        result = resolve_call(find_call(tree, f"{module_name}.{name}"), context, index, ALL)

        if isinstance(result, Skipped):
            continue

        bound += 1

        try:
            signature = inspect.signature(getattr(module, name))
        except (TypeError, ValueError):
            disagreements.append(f"{name}: bound, but inspect reads no signature")
            continue

        expected = [
            (p.name, p.kind.name.lower(), p.default is not inspect.Parameter.empty)
            for p in signature.parameters.values()
        ]

        actual = [(p.name, p.kind.value, p.has_default) for p in result.parameters]

        if actual != expected:
            disagreements.append(f"{name}: {actual} != {expected}")

    assert disagreements == []
    assert bound > 0
