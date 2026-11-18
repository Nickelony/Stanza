"""
The call rules leave a call unnamed where code rewrites what its callee or its class is at run time.

Each test runs the text before and after the fix and compares what the program does, because the damage of a wrong
name is a `TypeError` where the original ran.
"""

from __future__ import annotations

from pathlib import Path

import pytest

from nickelony_stanza import Config, fix
from nickelony_stanza.analysis.projects import Projects
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


_TARGET = "def target(a, b, c, d):\n    return a + b + c + d\n\n\n"
_REBIND = "lambda w, x, y, z: w"

_NAMESPACE_WRITES = {
    "a subscript of globals": f"globals()['target'] = {_REBIND}",
    "an update of globals": f"globals().update(target={_REBIND})",
    "a setdefault of globals": f"globals().setdefault('target', {_REBIND})",
    "a pop of globals": "globals().pop('missing', None)",
    "a delete of an item of globals": "globals()['extra'] = 1\ndel globals()['extra']",
    "globals passed to a function": f"def rebind(ns):\n    ns['target'] = {_REBIND}\n\n\nrebind(globals())",
    "an alias of globals": f"namespace = globals()\nnamespace['target'] = {_REBIND}",
    "globals inside a function": f"def install():\n    globals()['target'] = {_REBIND}\n\n\ninstall()",
    "globals through builtins": f"builtins.globals()['target'] = {_REBIND}",
    "exec of the module": f"exec('target = {_REBIND}')",
    "exec with globals": f"exec('target = {_REBIND}', globals())",
    "exec with a name": f"scope = globals()\nexec('target = {_REBIND}', scope)",
    "exec with a global statement in a function": (
        f"def install():\n    exec('global target; target = {_REBIND}')\n\n\ninstall()"
    ),
    "an eval of a walrus": f"eval('(target := {_REBIND})')",
    "locals at module level": f"locals()['target'] = {_REBIND}",
    "vars at module level": f"vars()['target'] = {_REBIND}",
    "an alias of exec": f"run = exec\nrun('target = {_REBIND}')",
    "exec passed to map": f"list(map(exec, ['target = {_REBIND}']))",
    "exec through builtins": f"builtins.exec('target = {_REBIND}')",
    "exec through getattr": f"getattr(builtins, 'exec')('target = {_REBIND}')",
    "exec through __builtins__": f"__builtins__['exec']('target = {_REBIND}')",
    "exec through the builtins module": f"later.exec('target = {_REBIND}')",
}


@pytest.mark.parametrize("write", _NAMESPACE_WRITES.values(), ids=_NAMESPACE_WRITES)
def test_a_module_that_can_write_its_own_namespace_resolves_no_name(write: str) -> None:
    before = f"import builtins\nimport builtins as later\n\n{_TARGET}{write}\n\nresult = target(1, 2, 3, 4)\n"
    expected = run_module(before)["result"]

    fixed = fix_with(before, "ZA3501")

    assert run_module(fixed)["result"] == expected
    assert fixed == before


_NAMESPACE_READS = {
    "an item of globals": "value = globals()['target']",
    "a lookup in globals": "found = 'target' in globals()",
    "a get of globals": "found = globals().get('target')",
    "the keys of globals": "names = list(globals().keys())",
    "locals in a function": "def info():\n    return locals()",
    "vars of an object": "def info(item):\n    return vars(item)",
    "eval in a function": "def info(x):\n    return eval('x + 1')",
    "exec with a dict of its own": "exec('value = 1', {})",
    "exec with a keyword dict of its own": "exec('value = 1', globals={'a': 1})",
    "an eval method": "def info(model):\n    return model.eval()",
    "a string that spells exec": "text = 'exec(globals())'",
}


@pytest.mark.parametrize("read", _NAMESPACE_READS.values(), ids=_NAMESPACE_READS)
def test_a_module_that_only_reads_its_namespace_still_resolves_its_names(read: str) -> None:
    before = f"{_TARGET}{read}\n\nresult = target(1, 2, 3, 4)\n"

    fixed = fix_with(before, "ZA3501")

    assert fixed.endswith("result = target(a=1, b=2, c=3, d=4)\n")
    assert run_module(fixed)["result"] == run_module(before)["result"]


def test_a_name_of_a_module_that_writes_its_namespace_is_not_resolved_from_another_module(tmp_path: Path) -> None:
    root = tmp_path / "app"
    root.mkdir()
    (root / "pyproject.toml").write_text("[project]\nname = 'demo'\n", encoding="utf-8")

    lib = root / "lib.py"
    main = root / "main.py"
    config = Config.create("none", enable=["ZA3501"])
    call = "from lib import target\ntarget(1, 2, 3, 4)\n"

    main.write_text(call, encoding="utf-8")
    lib.write_text(_TARGET + "globals().update(target=lambda w, x, y, z: w)\n", encoding="utf-8")
    assert fix(call, config, path=main, projects=Projects()).text == call
    lib.write_text(_TARGET + "names = list(globals().keys())\n", encoding="utf-8")
    assert fix(call, config, path=main, projects=Projects()).text.endswith("target(a=1, b=2, c=3, d=4)\n")


_OTHER = "def other(w, x, y, z):\n    return w\n\n\n"

_SIGNATURE_MUTATIONS = {
    "a code object": "target.__code__ = other.__code__",
    "a code object set by name": "setattr(target, '__code__', other.__code__)",
    "a code object of a member": "this = types.SimpleNamespace(target=target)\nthis.target.__code__ = other.__code__",
    "a code object of an element": "functions = [target]\nfunctions[0].__code__ = other.__code__",
    "a code object copied by update_wrapper": "functools.update_wrapper(target, other, assigned=('__code__',))",
    "a computed set of copied attributes": "functools.update_wrapper(target, other, assigned=names)",
}


@pytest.mark.parametrize("mutation", _SIGNATURE_MUTATIONS.values(), ids=_SIGNATURE_MUTATIONS)
def test_a_function_whose_signature_the_module_replaces_is_not_resolved(mutation: str) -> None:
    head = "import functools\nimport inspect\nimport types\n\nnames = ('__code__',)\n\n"
    before = f"{head}{_TARGET}{_OTHER}{mutation}\n\nresult = target(1, 2, 3, 4)\n"
    expected = run_module(before)["result"]

    fixed = fix_with(before, "ZA3501")

    assert run_module(fixed)["result"] == expected
    assert fixed == before


def test_a_method_or_constructor_whose_code_the_module_replaces_is_not_resolved() -> None:
    method = (
        "class C:\n    def m(self, a, b, c, d):\n        return a\n\n    def run(self):\n        return self.m(1, 2, 3, 4)\n\n\n"
        "def other(self, w, x, y, z):\n    return w\n\n\nC.m.__code__ = other.__code__\nresult = C().run()\n"
    )

    constructor = (
        "class C:\n    def __init__(self, a, b, c, d):\n        self.a = a\n\n\n"
        "def other(self, w, x, y, z):\n    self.a = w\n\n\nC.__init__.__code__ = other.__code__\nresult = C(1, 2, 3, 4).a\n"
    )

    for before in (method, constructor):
        expected = run_module(before)["result"]
        fixed = fix_with(before, "ZA3501")

        assert run_module(fixed)["result"] == expected
        assert fixed == before


_TARGET = "def target(a, b, c, d):\n    return a + b + c + d\n\n\n"
_OTHER = "def other(w, x, y, z):\n    return w\n\n\n"
_RECODE = "\n\nK.f.__code__ = other.__code__\nresult = target(1, 2, 3, 4)\n"

# The class bodies and the statements around them that leave `K.f` holding a function other than a method `f` of `K`,
# so the write to `K.f.__code__` changes `target` and no call of the module may be named.
_NOT_THE_METHOD = {
    "an attribute that holds a function": "class K:\n    f = target\n",
    "an attribute set after a def": "class K:\n    def f(self):\n        pass\n\n    f = target\n",
    "an attribute set in an if": "class K:\n    if True:\n        f = target\n    else:\n        def f(self):\n            pass\n",
    "a name captured by a match": (
        "class K:\n    def f(self):\n        pass\n\n    match target:\n        case f:\n            pass\n"
    ),
    "an attribute set after the class": "class K:\n    def f(self):\n        pass\n\n\nK.f = target\n",
    "an attribute set by name": "class K:\n    def f(self):\n        pass\n\n\nsetattr(K, 'f', target)\n",
    "an attribute set by a computed name": "class K:\n    def f(self):\n        pass\n\n\nsetattr(K, 'F'.lower(), target)\n",
    "a class that a decorator changes": (
        "def swap(cls):\n    cls.f = target\n    return cls\n\n\n@swap\nclass K:\n    def f(self):\n        pass\n"
    ),
    "a method that a decorator replaces": (
        "def deco(fn):\n    return target\n\n\nclass K:\n    @deco\n    def f(self):\n        pass\n"
    ),
    "a class bound twice": "class K:\n    def f(self):\n        pass\n\n\nclass K:\n    f = target\n",
    "a class bound again in a function": (
        "class K:\n    def f(self):\n        pass\n\n\ndef make():\n    class K:\n        f = target\n\n    return K\n"
    ),
    "a class bound by a loop": "class K:\n    def f(self):\n        pass\n\n\nclass J:\n    f = target\n\n\nfor K in [J]:\n    pass\n",
}


@pytest.mark.parametrize("shape", _NOT_THE_METHOD.values(), ids=_NOT_THE_METHOD)
def test_a_code_write_to_a_class_attribute_that_may_hold_another_function_is_not_resolved(shape: str) -> None:
    before = _TARGET + _OTHER + shape + _RECODE
    expected = run_module(before)["result"]
    fixed = fix_with(before, "ZA3501")

    assert run_module(fixed)["result"] == expected
    assert fixed == before


def test_a_code_write_to_a_def_that_a_decorator_replaces_is_not_resolved() -> None:
    before = (
        _TARGET
        + _OTHER
        + "def deco(fn):\n    return target\n\n\n@deco\ndef f():\n    pass\n\n\nf.__code__ = other.__code__\nresult = target(1, 2, 3, 4)\n"
    )

    expected = run_module(before)["result"]
    fixed = fix_with(before, "ZA3501")

    assert run_module(fixed)["result"] == expected
    assert fixed == before


_SIGNATURE_NEIGHBORS = {
    "a docstring": "target.__doc__ = 'draws'",
    "a note": "target.cache = {}",
    "a read of the code": "size = target.__code__.co_argcount",
    "an update_wrapper with its defaults": "functools.update_wrapper(target, other)",
    "a code object of another function": "other.__code__ = other.__code__",
}


@pytest.mark.parametrize("neighbor", _SIGNATURE_NEIGHBORS.values(), ids=_SIGNATURE_NEIGHBORS)
def test_an_attribute_that_leaves_the_signature_alone_does_not_stop_the_call(neighbor: str) -> None:
    before = f"import functools\n\n{_TARGET}{_OTHER}{neighbor}\n\nresult = target(1, 2, 3, 4)\n"

    fixed = fix_with(before, "ZA3501")

    assert fixed.endswith("result = target(a=1, b=2, c=3, d=4)\n")
    assert run_module(fixed)["result"] == run_module(before)["result"]


def test_a_signature_replaced_in_another_module_stops_the_call_in_this_one(tmp_path: Path) -> None:
    root = tmp_path / "app"
    root.mkdir()
    (root / "pyproject.toml").write_text("[project]\nname = 'demo'\n", encoding="utf-8")
    (root / "lib.py").write_text(_TARGET, encoding="utf-8")
    (root / "patch.py").write_text(f"import lib\n\n{_OTHER}lib.target.__code__ = other.__code__\n", encoding="utf-8")

    main = root / "main.py"
    config = Config.create("none", enable=["ZA3501"])
    call = "from lib import target\ntarget(1, 2, 3, 4)\n"

    assert fix(call, config, path=main, projects=Projects()).text == call

    (root / "patch.py").write_text("import lib\n\nlib.target.__doc__ = 'draws'\n", encoding="utf-8")

    assert fix(call, config, path=main, projects=Projects()).text.endswith("target(a=1, b=2, c=3, d=4)\n")
