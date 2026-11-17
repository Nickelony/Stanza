"""
The watches of `dynamic.py` stop a call only where code can change what a name or a signature means,
so ordinary code that merely looks like such a rewrite leaves its calls named.
"""

from __future__ import annotations

import ast
import importlib
from pathlib import Path

import pytest

from nickelony_stanza import Config, fix, lint
from nickelony_stanza.analysis.projects import Projects
from nickelony_stanza.analysis.scopes import scan_module
from tests.helpers import fix_with, run_module

_TARGET = "def target(a, b, c, d):\n    return a + b + c + d\n\n\n"
_CALL = "\n\nresult = target(1, 2, 3, 4)\n"

_QUIET_MODULES = {
    "a tuple named builtins": 'builtins = ("abs", "len")\nfound = "abs" in builtins',
    "a variable named builtins in a function": (
        "def names(tree):\n    builtins = tree\n    return [name for name in builtins]"
    ),
    "a dir of builtins": "import builtins\n\nfound = dir(builtins)",
    "an attribute set on builtins": "import builtins\n\nbuiltins._ = None",
    "an __import__ that takes the namespaces": "module = __import__('os', globals(), locals(), level=0)",
    "an exec with a namespace of its own": "about = {}\nexec('value = 1', about)",
    "an exec with a name given as a keyword": "about = {}\nexec('value = 1', globals=about)",
    "an __all__ built from globals": "__all__ = [name for name in globals() if not name.startswith('_')]",
    "a copy of globals": "table = dict(globals())",
    "a sorted globals": "names = sorted(globals())",
    "a length of globals": "size = len(globals())",
    "an iteration of globals": "def count():\n    size = 0\n    for name in globals():\n        size += 1\n    return size",
    "an eval of a constant at module level": "LIMIT = eval('1 + 1')",
    "an eval method fetched by getattr": "def run(model):\n    return getattr(model, 'eval')()",
    "an exec method fetched by getattr": "def run(model):\n    return getattr(model, 'exec')('x')",
}


@pytest.mark.parametrize("read", _QUIET_MODULES.values(), ids=_QUIET_MODULES)
def test_code_that_reads_a_namespace_or_runs_in_one_of_its_own_leaves_the_names_resolved(read: str) -> None:
    before = f"{_TARGET}{read}\n{_CALL}"

    fixed = fix_with(before, "ZA3501")

    assert fixed.endswith("result = target(a=1, b=2, c=3, d=4)\n")
    assert run_module(fixed)["result"] == run_module(before)["result"]


_LOUD_MODULES = {
    "an exec in the namespace of the module": "exec('target = 1')",
    "an exec with globals": "exec('target = 1', globals())",
    "an exec with locals in a function": "def run():\n    exec('target = 1', locals())",
    "an eval of a walrus": "eval('(target := 1)')",
    "an eval of a computed code at module level": "code = '(target := 1)'\neval(code)",
    "a getattr of exec on builtins": "import builtins\n\ngetattr(builtins, 'exec')('target = 1')",
    "a getattr of exec on __builtins__": "getattr(__builtins__, 'exec')('target = 1')",
    "the builtins module handed on": "import builtins\n\nhand_on = lambda module: module\nhand_on(builtins)",
    "builtins imported as a name": "import builtins as table\n\ntable.exec('target = 1')",
    "an item of globals written": "globals()['target'] = 1",
}


@pytest.mark.parametrize("write", _LOUD_MODULES.values(), ids=_LOUD_MODULES)
def test_code_that_can_rewrite_the_namespace_still_stops_the_names(write: str) -> None:
    before = f"{_TARGET}{write}\n{_CALL}"
    assert fix_with(before, "ZA3501") == before


_SIGNATURE_OTHER = "def other(w, x, y, z):\n    return w\n\n\n"

_HARMLESS_SIGNATURE_WRITES = {
    "a signature": "target.__signature__ = inspect.signature(other)",
    "defaults": "target.__defaults__ = (1,)",
    "keyword defaults": "target.__kwdefaults__ = {}",
    "a deleted default": "del target.__defaults__",
    "a deleted signature": "target.__signature__ = None\ndel target.__signature__",
    "a signature set by name": "setattr(target, '__signature__', inspect.signature(other))",
    "wraps with its default attributes": "wrapper = functools.wraps(other, assigned=functools.WRAPPER_ASSIGNMENTS)(target)",
    "wraps with the default updates": "wrapper = functools.wraps(other, updated=functools.WRAPPER_UPDATES)(target)",
    "update_wrapper with the default name": "functools.update_wrapper(target, other, assigned=WRAPPER_ASSIGNMENTS)",
    "update_wrapper with a joined default": (
        "functools.update_wrapper(target, other, assigned=functools.WRAPPER_ASSIGNMENTS + ('__doc__',))"
    ),
    "update_wrapper with a joined literal": "functools.update_wrapper(target, other, assigned=('__doc__',) + ('x',))",
    "update_wrapper with a signature": "functools.update_wrapper(target, other, assigned=('__signature__',))",
}


@pytest.mark.parametrize("write", _HARMLESS_SIGNATURE_WRITES.values(), ids=_HARMLESS_SIGNATURE_WRITES)
def test_an_attribute_that_cannot_rename_a_parameter_leaves_the_call_named(write: str) -> None:
    head = "import functools\nimport inspect\nfrom functools import WRAPPER_ASSIGNMENTS\n\n"
    before = f"{head}{_TARGET}{_SIGNATURE_OTHER}{write}\n{_CALL}"

    fixed = fix_with(before, "ZA3501")

    assert fixed.endswith("result = target(a=1, b=2, c=3, d=4)\n")
    assert run_module(fixed)["result"] == run_module(before)["result"]


_CODE_COPIES = {
    "a literal set with the code": "functools.update_wrapper(target, other, assigned=('__code__',))",
    "a joined default with the code": (
        "functools.update_wrapper(target, other, assigned=functools.WRAPPER_ASSIGNMENTS + ('__code__',))"
    ),
    "a code object": "target.__code__ = other.__code__",
}


@pytest.mark.parametrize("copy", _CODE_COPIES.values(), ids=_CODE_COPIES)
def test_a_copy_of_the_code_still_stops_the_call(copy: str) -> None:
    before = f"import functools\n\n{_TARGET}{_SIGNATURE_OTHER}{copy}\n{_CALL}"
    assert fix_with(before, "ZA3501") == before


_UNREADABLE_WRITES = {
    "a code object of a subscript": "handlers[name].__code__ = other.__code__",
    "a computed set of copied attributes": "functools.update_wrapper(target, other, assigned=computed())",
    "a computed set of a decorator": "wrapper = functools.wraps(other, assigned=computed())",
    "a joined set with a computed part": "wrapper = functools.wraps(other, assigned=functools.WRAPPER_ASSIGNMENTS + computed())",
}

_PROJECT_CALL = "from lib import target\ntarget(1, 2, 3, 4)\n"
_LIB_HEAD = "import functools\nimport inspect\n\nhandlers = {}\nname = 'x'\n\n\ndef computed():\n    return ()\n\n\n"


def _project(root: Path, **modules: str) -> Path:
    root.mkdir()
    (root / "pyproject.toml").write_text("[project]\nname = 'demo'\n", encoding="utf-8")

    for name, text in modules.items():
        (root / f"{name}.py").write_text(text, encoding="utf-8")

    return root / "main.py"


def _findings(main: Path, call: str = _PROJECT_CALL) -> int:
    config = Config.create("none", enable=["ZA3501"])
    return len(lint(call, config, path=main, projects=Projects()))


@pytest.mark.parametrize("write", _HARMLESS_SIGNATURE_WRITES.values(), ids=_HARMLESS_SIGNATURE_WRITES)
def test_a_harmless_signature_write_in_another_module_leaves_the_call_named(tmp_path: Path, write: str) -> None:
    head = "import functools\nimport inspect\nfrom functools import WRAPPER_ASSIGNMENTS\n\n"

    main = _project(
        tmp_path / "app", lib=_TARGET, patch=f"{head}{_SIGNATURE_OTHER}import lib\n\ntarget = lib.target\n{write}\n"
    )

    assert _findings(main) == 1


@pytest.mark.parametrize("write", _UNREADABLE_WRITES.values(), ids=_UNREADABLE_WRITES)
def test_an_unreadable_signature_write_in_another_module_stops_every_call(tmp_path: Path, write: str) -> None:
    other = f"{_LIB_HEAD}{_SIGNATURE_OTHER}{write}\n"
    main = _project(tmp_path / "app", lib=_TARGET, patch=other)

    assert _findings(main) == 0


_UNTIED_WRITES = {
    "a subscript": "functions = [target]\nfunctions[0].__code__ = other.__code__",
    "a loop variable": "for function in (target,):\n    function.__code__ = other.__code__",
    "a local alias": "def retarget():\n    alias = target\n    alias.__code__ = other.__code__",
    "a parameter": "def retarget(function, new):\n    function.__code__ = new.__code__\n\n\nretarget(target, other)",
    "an attribute of a value": "box = types.SimpleNamespace(target=target)\nbox.target.__code__ = other.__code__",
    "an attribute of an import": "import lib\n\nlib.target.__code__ = other.__code__",
    "a name that is a def and a parameter": (
        "def retarget(target, new):\n    target.__code__ = new.__code__\n\n\nretarget(target, other)"
    ),
    "a set by name on a parameter": (
        "def retarget(function, new):\n    setattr(function, '__code__', new.__code__)\n\n\nretarget(target, other)"
    ),
}

_TIED_WRITES = {
    "a def": "target.__code__ = other.__code__",
    "a def set by name": "setattr(target, '__code__', other.__code__)",
    "a def copied": "functools.update_wrapper(target, other, assigned=('__code__',))",
}


@pytest.mark.parametrize("write", _UNTIED_WRITES.values(), ids=_UNTIED_WRITES)
def test_a_code_write_that_ties_to_no_def_of_its_module_stops_every_call_of_the_project(
    tmp_path: Path, write: str
) -> None:
    main = _project(tmp_path / "app", lib=_TARGET, patch=f"import types\n\n{_SIGNATURE_OTHER}{write}\n")
    assert _findings(main) == 0


@pytest.mark.parametrize("write", _TIED_WRITES.values(), ids=_TIED_WRITES)
def test_a_code_write_to_a_def_of_its_module_stops_only_the_calls_of_that_name(tmp_path: Path, write: str) -> None:
    patch = f"import functools\n\n\ndef target(a, b):\n    return a\n\n\n{_SIGNATURE_OTHER}{write}\n"
    main = _project(tmp_path / "app", lib="def stays(a, b, c, d):\n    return a\n", patch=patch)

    assert _findings(main, "from lib import stays\nstays(1, 2, 3, 4)\n") == 1
    assert _findings(main, "from patch import target\ntarget(1, 2)\n") == 0


def test_a_code_write_to_a_method_of_a_class_of_its_module_stops_the_calls_of_that_method(tmp_path: Path) -> None:
    method = "class C:\n    def m(self, a, b, c, d):\n        return a\n\n\n"
    main = _project(tmp_path / "app", lib=_TARGET, patch=f"{method}{_SIGNATURE_OTHER}C.m.__code__ = other.__code__\n")

    assert _findings(main) == 1


def test_a_code_write_that_ties_to_no_def_stops_a_call_across_three_modules(tmp_path: Path) -> None:
    root = tmp_path / "app"
    root.mkdir()
    (root / "pyproject.toml").write_text("[project]\nname = 'demo'\n", encoding="utf-8")
    (root / "pkg").mkdir()
    (root / "pkg" / "__init__.py").write_text("", encoding="utf-8")
    (root / "pkg" / "a.py").write_text(_TARGET, encoding="utf-8")
    (root / "pkg" / "other.py").write_text(_SIGNATURE_OTHER, encoding="utf-8")

    (root / "pkg" / "b.py").write_text(
        "from pkg import other\nfrom pkg.a import target\n\nfns = [target]\nfns[0].__code__ = other.other.__code__\n",
        encoding="utf-8",
    )

    main = root / "pkg" / "main.py"
    call = "import pkg.b\nfrom pkg.a import target\n\nresult = target(1, 2, 3, 4)\n"
    config = Config.create("none", enable=["ZA3501"])

    assert fix(call, config, path=main, projects=Projects()).text == call

    (root / "pkg" / "b.py").write_text("from pkg.a import target\n\nfns = [target]\n", encoding="utf-8")

    assert fix(call, config, path=main, projects=Projects()).text.endswith("target(a=1, b=2, c=3, d=4)\n")


def test_an_unreadable_namespace_write_in_another_module_stops_only_its_own_names(tmp_path: Path) -> None:
    main = _project(tmp_path / "app", lib=_TARGET, patch="globals().update(x=1)\n")
    assert _findings(main) == 1
    (tmp_path / "app" / "lib.py").write_text(_TARGET + "globals().update(x=1)\n", encoding="utf-8")
    assert _findings(main) == 0


_REAL_PATTERNS = {
    "a lexer that names a tuple builtins": (
        "from pygments.lexer import RegexLexer\n\n\nclass SampleLexer(RegexLexer):\n"
        "    builtins = ('abs', 'len')\n\n    def names(self):\n        return [n for n in self.builtins]\n"
        "\n\nbuiltins = ('abs', 'len')\n\n\ndef wanted(word):\n    return word in builtins\n"
    ),
    "a lexer that reads its names in a function": (
        "def get_tokens(tree):\n    builtins = tree.names\n    return [name for name in builtins]\n"
    ),
    "pip running a snippet in a namespace of its own": (
        "def run(code):\n    ns = {}\n    exec(code, ns)\n    return ns\n"
    ),
    "pip running a snippet in a module-level namespace of its own": (
        "ns = {'__name__': 'snippet'}\ncode = compile('x = 1', 'snippet', 'exec')\nexec(code, ns)\n"
    ),
    "a module that builds __all__ from globals": "__all__ = [n for n in globals() if n.isupper()]\n",
}


@pytest.mark.parametrize("source", _REAL_PATTERNS.values(), ids=_REAL_PATTERNS)
def test_the_patterns_of_real_packages_do_not_mark_a_module(source: str) -> None:
    scan = scan_module(ast.parse(source))

    assert not scan.rewrites_namespace
    assert not scan.replaced_signatures


def test_no_lexer_module_of_pygments_is_marked() -> None:
    lexers = importlib.import_module("pygments.lexers")
    files = sorted(Path(str(lexers.__file__)).parent.glob("*.py"))

    marked = [
        file.name
        for file in files
        if (scan := scan_module(ast.parse(file.read_text(encoding="utf-8")))).rewrites_namespace
        or scan.replaced_signatures
    ]

    assert len(files) > 100
    assert marked == []
