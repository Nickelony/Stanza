"""
The call rules leave a call unnamed where code rewrites what its callee or its class is at run time.

Each test runs the text before and after the fix and compares what the program does, because the damage of a wrong
name is a `TypeError` where the original ran.
"""

from __future__ import annotations

import pytest

from tests.helpers import fix_with, run_module

_CLEAR = "__annotations__.clear(); c: int; d: int"

_INDIRECT_NAMESPACE_BODIES = {
    "an alias of exec": f"run = exec\n    run('{_CLEAR}')",
    "exec passed to map": f"list(map(exec, ['{_CLEAR}']))",
    "getattr of builtins": f"getattr(builtins, 'exec')('{_CLEAR}')",
    "a subscript of __builtins__": f"__builtins__['exec']('{_CLEAR}')",
    "the dict of builtins": f"builtins.__dict__['exec']('{_CLEAR}')",
    "an alias of the builtins module": f"later.exec('{_CLEAR}')",
    "an alias of eval": "run = eval\n    run('__annotations__.clear()')\n    c: int\n    d: int",
    "an alias of vars": "read = vars\n    read()['__annot' + 'ations__'].clear()\n    c: int\n    d: int",
    "an alias of locals": "read = locals\n    read()['__annot' + 'ations__'].clear()\n    c: int\n    d: int",
    "a lookup of locals by name": "getattr(builtins, 'locals')()['__annot' + 'ations__'].clear()\n    c: int\n    d: int",
}


@pytest.mark.parametrize("body", _INDIRECT_NAMESPACE_BODIES.values(), ids=_INDIRECT_NAMESPACE_BODIES)
@pytest.mark.parametrize("kind", ["dataclass", "namedtuple"])
def test_a_class_body_that_reaches_its_namespace_indirectly_leaves_the_call_unnamed(kind: str, body: str) -> None:
    head = "@dataclass\nclass C:" if kind == "dataclass" else "class C(NamedTuple):"
    imports = "import builtins\nimport builtins as later\nfrom dataclasses import dataclass\nfrom typing import NamedTuple\n\n"
    before = f"{imports}{head}\n    a: int\n    b: int\n    {body}\n\nx = C(True, 2)\n"
    expected = repr(run_module(before)["x"])

    fixed = fix_with(before, "ZA3502")

    assert repr(run_module(fixed)["x"]) == expected
    assert fixed == before


_FREE_NAMESPACE_SPELLINGS = [
    ("@dataclass\nclass C:\n    text: str\n    name: str\n    exec: str = 'x'\n", "C('x', True, 'y')", "name=True"),
    ("class C(NamedTuple):\n    eval: int\n    name: Literal['vars', 'locals']\n", "C(True, 'vars')", "eval=True"),
    (
        "class C(NamedTuple):\n    source: str\n    mode: str = getattr(str, 'upper').__name__\n",
        "C(True, 'x')",
        "source=True",
    ),
]


@pytest.mark.parametrize(("declaration", "call", "named"), _FREE_NAMESPACE_SPELLINGS, ids=range(3))
def test_a_field_named_like_a_namespace_function_or_a_string_that_spells_one_does_not_stop_the_class(
    declaration: str, call: str, named: str
) -> None:
    imports = "from dataclasses import dataclass\nfrom typing import Literal, NamedTuple\n\n"
    before = f"{imports}{declaration}\nx = {call}\n"

    fixed = fix_with(before, "ZA3502")

    assert named in fixed.splitlines()[-1]
    assert repr(run_module(fixed)["x"]) == repr(run_module(before)["x"])
