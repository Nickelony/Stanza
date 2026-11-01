"""The call resolver: callee shapes, class shapes, decorators, methods and the reason a call is left alone."""

from __future__ import annotations

import ast
import textwrap
from pathlib import Path

import pytest

from nickelony_stanza.analysis.binding import (
    SOURCES,
    Binding,
    MethodsMode,
    ModuleContext,
    ParameterKind,
    Reason,
    ResolveOptions,
    Skipped,
    Source,
    SourceIndex,
    resolve_call,
)

ALL = ResolveOptions(sources=SOURCES, methods="all")


def write_tree(root: Path, files: dict[str, str]) -> None:
    """Writes the files of a layout, creating the directories they sit in."""
    for name, text in files.items():
        path = root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(textwrap.dedent(text), encoding="utf-8")


def find_call(tree: ast.Module, callee: str, occurrence: int = -1) -> ast.Call:
    """Finds a call by the source of its callee, the last one unless an occurrence is given."""
    calls = [node for node in ast.walk(tree) if isinstance(node, ast.Call) and ast.unparse(node.func) == callee]
    assert calls, f"no call of {callee}"

    return sorted(calls, key=lambda call: (call.lineno, call.col_offset))[occurrence]


def resolve_in(
    index: SourceIndex,
    path: Path,
    callee: str,
    *,
    options: ResolveOptions = ALL,
    occurrence: int = -1,
) -> Binding | Skipped:
    """Resolves a call of a file on disk."""
    text = path.read_text(encoding="utf-8")
    tree = ast.parse(text)

    return resolve_call(find_call(tree, callee, occurrence), ModuleContext.build(tree, path, text), index, options)


def resolve_text(
    tmp_path: Path,
    text: str,
    callee: str,
    *,
    sources: frozenset[Source] = SOURCES,
    methods: MethodsMode = "all",
    occurrence: int = -1,
) -> Binding | Skipped:
    """Resolves a call of a single-file project."""
    write_tree(tmp_path, {"main.py": text})

    index = SourceIndex([tmp_path], installed=())
    options = ResolveOptions(sources=sources, methods=methods)

    return resolve_in(index, tmp_path / "main.py", callee, options=options, occurrence=occurrence)


def names(result: Binding | Skipped, count: int) -> tuple[str | None, ...]:
    """Lists the names a binding gives the first arguments, failing on a skipped call."""
    assert isinstance(result, Binding), result
    return result.names_for(count)


def reason(result: Binding | Skipped) -> Reason:
    """Reads the reason of a skipped call, failing on a binding."""
    assert isinstance(result, Skipped), result
    return result.reason


# Functions.


def test_a_module_function_binds_each_positional_argument_to_its_parameter(tmp_path: Path) -> None:
    result = resolve_text(tmp_path, "def f(a, b, c=1, *, d): ...\nf(1, 2, 3, d=4)\n", "f")

    assert names(result, 3) == ("a", "b", "c")
    assert isinstance(result, Binding)
    assert result.callee == "main.f"
    assert result.source == "project"

    assert [(parameter.name, parameter.kind, parameter.has_default) for parameter in result.parameters] == [
        ("a", ParameterKind.POSITIONAL_OR_KEYWORD, False),
        ("b", ParameterKind.POSITIONAL_OR_KEYWORD, False),
        ("c", ParameterKind.POSITIONAL_OR_KEYWORD, True),
        ("d", ParameterKind.KEYWORD_ONLY, False),
    ]


def test_a_positional_only_or_mangled_parameter_cannot_be_named(tmp_path: Path) -> None:
    result = resolve_text(tmp_path, "def f(a, /, __b, c): ...\nf(1, 2, 3)\n", "f")
    assert names(result, 3) == (None, None, "c")


@pytest.mark.parametrize(
    "call",
    ["f(*items)", "f(1, **options)", "f(1, 2, 3)", "g(1, 2, 3)", "f(1, a=2)"],
)
def test_a_call_that_cannot_be_named_as_written_is_left_alone(tmp_path: Path, call: str) -> None:
    text = f"def f(a, b): ...\ndef g(a, *rest): ...\n{call}\n"
    assert reason(resolve_text(tmp_path, text, call.split("(")[0])) == Reason.CALL_SHAPE


def test_a_positional_argument_before_varargs_still_binds(tmp_path: Path) -> None:
    assert names(resolve_text(tmp_path, "def g(a, *rest): ...\ng(1)\n", "g"), 1) == ("a",)


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ("def f(a, b): ...\ndef user():\n    f = print\n    f(1, 2)\n", Reason.LOCAL_BINDING),
        ("def f(a, b): ...\ndef f(c, d): ...\nf(1, 2)\n", Reason.NAME_REBOUND),
        ("def f(a, b): ...\ndef reset():\n    global f\n    f = None\nf(1, 2)\n", Reason.NAME_REBOUND),
        ("import sys\nif sys:\n    def f(a, b): ...\nf(1, 2)\n", Reason.CONDITIONAL_BINDING),
        ("try:\n    def f(a, b): ...\nexcept ImportError:\n    pass\nf(1, 2)\n", Reason.CONDITIONAL_BINDING),
        ("import functools\ndef g(a, b): ...\nf = functools.partial(g, 1)\nf(1, 2)\n", Reason.ASSIGNED_VALUE),
        ("f = lambda a, b: None\nf(1, 2)\n", Reason.ASSIGNED_VALUE),
        ("divmod(1, 2)\n", Reason.BUILTIN_OR_C),
        ("missing(1, 2)\n", Reason.UNBOUND_NAME),
        ("def f(a, b): ...\n[g(1, 2) for g in [f]]\n", Reason.LOCAL_BINDING),
        ("def f(a, b): ...\nf.__call__(1, 2)\n", Reason.RECEIVER_IS_VALUE),
        ("def f(a, b): ...\ndef user(x):\n    x.f(1, 2)\n", Reason.RECEIVER_IS_VALUE),
    ],
)
def test_a_name_that_may_hold_another_value_is_left_alone(tmp_path: Path, text: str, expected: Reason) -> None:
    calls = [node for node in ast.walk(ast.parse(text)) if isinstance(node, ast.Call) and len(node.args) == 2]
    callee = ast.unparse(max(calls, key=lambda call: call.lineno).func)

    assert reason(resolve_text(tmp_path, text, callee)) == expected


def test_an_alias_of_a_function_binds_like_the_function(tmp_path: Path) -> None:
    assert names(resolve_text(tmp_path, "def f(a, b): ...\ng = f\ng(1, 2)\n", "g"), 2) == ("a", "b")


def test_a_local_that_shadows_nothing_leaves_the_module_name_visible(tmp_path: Path) -> None:
    text = "def f(a, b): ...\ndef user(x):\n    y = x\n    f(1, 2)\n"
    assert names(resolve_text(tmp_path, text, "f"), 2) == ("a", "b")


def test_a_mangled_module_name_is_read_inside_a_class(tmp_path: Path) -> None:
    text = "def _C__helper(a, b): ...\nclass C:\n    def run(self):\n        __helper(1, 2)\n"
    assert names(resolve_text(tmp_path, text, "__helper"), 2) == ("a", "b")


def test_a_project_disabled_by_the_options_binds_nothing(tmp_path: Path) -> None:
    result = resolve_text(tmp_path, "def f(a, b): ...\nf(1, 2)\n", "f", sources=frozenset({"stdlib"}))
    assert reason(result) == Reason.SOURCE_DISABLED


# Decorators.


@pytest.mark.parametrize(
    "decorator",
    [
        "import abc\n@abc.abstractmethod",
        "from typing import final\n@final",
        "import typing\n@typing.no_type_check",
        "from contextlib import contextmanager\n@contextmanager",
        "import typing_extensions\n@typing_extensions.override",
        "import warnings\n@warnings.deprecated('old')",
        "@staticmethod",
    ],
)
def test_a_decorator_that_keeps_the_signature_is_seen_through(tmp_path: Path, decorator: str) -> None:
    text = f"{decorator}\ndef f(a, b): ...\nf(1, 2)\n"
    assert names(resolve_text(tmp_path, text, "f"), 2) == ("a", "b")


@pytest.mark.parametrize(
    "decorator",
    [
        "import functools\n@functools.lru_cache",
        "from functools import cache\n@cache",
        "import functools\n@functools.singledispatch",
        "def wrap(function): return function\n@wrap",
        "def final(function): return function\n@final",
        "@classmethod",
    ],
)
def test_any_other_decorator_leaves_the_call_alone(tmp_path: Path, decorator: str) -> None:
    text = f"{decorator}\ndef f(a, b): ...\nf(1, 2)\n"
    assert reason(resolve_text(tmp_path, text, "f")) == Reason.DECORATOR


def test_overloads_that_agree_on_their_positional_names_bind_to_the_implementation(tmp_path: Path) -> None:
    text = """\
        from typing import overload
        @overload
        def f(a: int) -> int: ...
        @overload
        def f(a: str, b: int) -> str: ...
        def f(a, b=0): ...
        f(1, 2)
    """

    assert names(resolve_text(tmp_path, text, "f"), 2) == ("a", "b")


@pytest.mark.parametrize("variant", ["def f(x: int, b: int) -> int: ...", "def f(a: int, /, b: int) -> int: ..."])
def test_overloads_that_disagree_leave_the_call_alone(tmp_path: Path, variant: str) -> None:
    text = f"from typing import overload\n@overload\n{variant}\n@overload\ndef f(a: str) -> str: ...\ndef f(a, b=0): ...\nf(1, 2)\n"
    assert reason(resolve_text(tmp_path, text, "f")) == Reason.DECORATOR


# Classes.


def test_a_class_binds_to_its_init_without_the_receiver(tmp_path: Path) -> None:
    result = resolve_text(tmp_path, "class C:\n    def __init__(self, a, b): ...\nC(1, 2)\n", "C")

    assert names(result, 2) == ("a", "b")
    assert isinstance(result, Binding)
    assert result.callee == "main.C"


def test_a_class_binds_to_the_init_it_inherits(tmp_path: Path) -> None:
    text = "import abc\nclass B(abc.ABC):\n    def __init__(self, a, b): ...\nclass C(B): pass\nC(1, 2)\n"
    assert names(resolve_text(tmp_path, text, "C"), 2) == ("a", "b")


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ("class C:\n    def __new__(cls, a, b): ...\n    def __init__(self, a, b): ...\n", Reason.CLASS_SHAPE),
        ("class M(type): pass\nclass C(metaclass=M):\n    def __init__(self, a, b): ...\n", Reason.CLASS_SHAPE),
        ("def deco(cls): return cls\n@deco\nclass C:\n    def __init__(self, a, b): ...\n", Reason.DECORATOR),
        ("from typing import Protocol\nclass C(Protocol):\n    def __init__(self, a, b): ...\n", Reason.CLASS_SHAPE),
        ("from typing import TypedDict\nclass C(TypedDict):\n    a: int\n    b: int\n", Reason.CLASS_SHAPE),
        ("class C(Exception):\n    def __init__(self, a, b):\n        self.a = a\n", Reason.CLASS_SHAPE),
        ("class C(int):\n    def __init__(self, a, b): ...\n", Reason.BUILTIN_OR_C),
        ("class C:\n    pass\n", Reason.BUILTIN_OR_C),
        ("import _thread\nclass C(_thread.RLock):\n    def __init__(self, a, b): ...\n", Reason.UNREADABLE_MODULE),
    ],
)
def test_a_class_whose_constructor_is_not_modeled_is_left_alone(tmp_path: Path, text: str, expected: Reason) -> None:
    assert reason(resolve_text(tmp_path, f"{text}C(1, 2)\n", "C")) == expected


@pytest.mark.parametrize(
    "text",
    [
        "from typing import final\n@final\nclass C:\n    def __init__(self, a, b): ...\n",
        "import functools\n@functools.total_ordering\nclass C:\n    def __init__(self, a, b): ...\n",
        "import abc\nclass C(metaclass=abc.ABCMeta):\n    def __init__(self, a, b): ...\n",
        "class C(dict):\n    def __init__(self, a, b): ...\n",
        "class C(ValueError):\n    def __init__(self, a, b):\n        super().__init__(a)\n",
        "from typing import Generic, TypeVar\nT = TypeVar('T')\nclass C(Generic[T]):\n    def __init__(self, a, b): ...\n",
        "from typing import Protocol\nclass P(Protocol):\n    def run(self): ...\nclass C(P):\n    def __init__(self, a, b): ...\n",
    ],
)
def test_a_class_with_a_transparent_decorator_or_a_safe_base_binds(tmp_path: Path, text: str) -> None:
    assert names(resolve_text(tmp_path, f"{text}C(1, 2)\n", "C"), 2) == ("a", "b")


def test_an_enum_is_left_alone(tmp_path: Path) -> None:
    assert isinstance(
        resolve_text(tmp_path, "from enum import Enum\nclass C(Enum):\n    A = 1\nC(1, 2)\n", "C"), Skipped
    )


def test_a_subscripted_generic_class_binds_like_the_class(tmp_path: Path) -> None:
    text = "from typing import Generic, TypeVar\nT = TypeVar('T')\nclass C(Generic[T]):\n    def __init__(self, a, b): ...\nC[int](1, 2)\n"
    assert names(resolve_text(tmp_path, text, "C[int]"), 2) == ("a", "b")


def test_a_nested_class_binds_through_its_owner(tmp_path: Path) -> None:
    text = "class Outer:\n    class Inner:\n        def __init__(self, a, b): ...\nOuter.Inner(1, 2)\n"
    result = resolve_text(tmp_path, text, "Outer.Inner")

    assert names(result, 2) == ("a", "b")
    assert isinstance(result, Binding)
    assert result.callee == "main.Outer.Inner"


# Dataclasses.


def _dataclass_parameters(tmp_path: Path, body: str) -> list[tuple[str, str, bool]]:
    text = f"import dataclasses\nfrom dataclasses import dataclass, field, KW_ONLY, InitVar\nfrom typing import ClassVar\n{body}\nC(1)\n"
    result = resolve_text(tmp_path, text, "C")

    assert isinstance(result, Binding), result
    return [(parameter.name, parameter.kind.value, parameter.has_default) for parameter in result.parameters]


def test_a_dataclass_takes_its_fields_in_declaration_order(tmp_path: Path) -> None:
    body = "@dataclass\nclass C:\n    a: int\n    b: int = 1\n    c: ClassVar[int] = 2\n    d: 'ClassVar[int]' = 3\n    e: list = field(default_factory=list)\n"

    assert _dataclass_parameters(tmp_path, body) == [
        ("a", "positional_or_keyword", False),
        ("b", "positional_or_keyword", True),
        ("e", "positional_or_keyword", True),
    ]


def test_a_dataclass_puts_the_fields_of_its_bases_first(tmp_path: Path) -> None:
    body = "@dataclasses.dataclass\nclass B:\n    a: int\n    b: int = 0\nclass M(B): pass\n@dataclass(frozen=True)\nclass C(M):\n    c: int = 1\n    a: int = 2\n"

    assert _dataclass_parameters(tmp_path, body) == [
        ("a", "positional_or_keyword", True),
        ("b", "positional_or_keyword", True),
        ("c", "positional_or_keyword", True),
    ]


def test_a_dataclass_moves_its_keyword_only_fields_after_the_others(tmp_path: Path) -> None:
    body = (
        "@dataclass(kw_only=True)\nclass B:\n    a: int\n    z: int = field(default=0, kw_only=False)\n"
        "@dataclass\nclass C(B):\n    b: int\n    c: int = field(kw_only=True)\n    _: KW_ONLY\n    d: int = 1\n"
    )

    assert _dataclass_parameters(tmp_path, body) == [
        ("z", "positional_or_keyword", True),
        ("b", "positional_or_keyword", False),
        ("a", "keyword_only", False),
        ("c", "keyword_only", False),
        ("d", "keyword_only", True),
    ]


def test_a_dataclass_skips_a_field_left_out_of_init_and_keeps_an_init_var(tmp_path: Path) -> None:
    body = "@dataclass\nclass C:\n    a: int\n    b: int = field(init=False)\n    c: InitVar[int] = 0\n"

    assert _dataclass_parameters(tmp_path, body) == [
        ("a", "positional_or_keyword", False),
        ("c", "positional_or_keyword", True),
    ]


def test_a_dataclass_without_init_or_with_its_own_init_binds_to_that_init(tmp_path: Path) -> None:
    body = (
        "class B:\n    def __init__(self, x): ...\n@dataclass(init=False)\nclass C(B):\n    a: int\n"
        "@dataclass\nclass D:\n    a: int\n    def __init__(self, y): ...\n"
    )

    assert _dataclass_parameters(tmp_path, body) == [("x", "positional_or_keyword", False)]
    assert names(resolve_text(tmp_path, f"from dataclasses import dataclass\n{body}D(1)\n", "D"), 1) == ("y",)


@pytest.mark.parametrize(
    "body",
    [
        "flag = True\n@dataclass(init=flag)\nclass C:\n    a: int\n",
        "@dataclass\nclass C:\n    a: int = field(init=bool(1))\n",
        "@dataclass\nclass C:\n    if True:\n        a: int\n",
    ],
)
def test_a_dataclass_whose_fields_depend_on_a_value_is_left_alone(tmp_path: Path, body: str) -> None:
    text = f"from dataclasses import dataclass, field\n{body}C(1)\n"
    assert reason(resolve_text(tmp_path, text, "C")) == Reason.CLASS_SHAPE


# Named tuples.


@pytest.mark.parametrize(
    "declaration",
    [
        "from typing import NamedTuple\nclass C(NamedTuple):\n    a: int\n    b: int = 0\n    def total(self): ...\n",
        "from collections import namedtuple\nC = namedtuple('C', 'a, b', defaults=[0])\n",
        "import collections\nC = collections.namedtuple('C', ['a', 'b'], defaults=(0,))\n",
        "from collections import namedtuple\nclass C(namedtuple('C', 'a b', defaults=(0,))):\n    __slots__ = ()\n",
        "from typing import NamedTuple\nclass B(NamedTuple):\n    a: int\n    b: int = 0\nclass C(B): pass\n",
    ],
)
def test_a_named_tuple_takes_its_fields_in_order(tmp_path: Path, declaration: str) -> None:
    result = resolve_text(tmp_path, f"{declaration}C(1, 2)\n", "C")

    assert names(result, 2) == ("a", "b")
    assert isinstance(result, Binding)
    assert [parameter.has_default for parameter in result.parameters] == [False, True]


@pytest.mark.parametrize(
    ("text", "callee"),
    [
        ("from typing import cast\ncast(int, 1)\n", "cast"),
        ("import typing\ntyping.NewType('UserId', int)\n", "typing.NewType"),
        ("from collections import namedtuple\nnamedtuple('P', 'x y')\n", "namedtuple"),
    ],
)
def test_a_special_form_a_type_checker_reads_positionally_is_left_alone(tmp_path: Path, text: str, callee: str) -> None:
    assert reason(resolve_text(tmp_path, text, callee)) == Reason.SPECIAL_FORM


def test_a_typed_named_tuple_call_takes_its_fields_in_order(tmp_path: Path) -> None:
    text = "from typing import NamedTuple\nC = NamedTuple('C', [('a', int), ('b', int)])\nC(1, 2)\n"
    assert names(resolve_text(tmp_path, text, "C"), 2) == ("a", "b")


@pytest.mark.parametrize(
    "declaration",
    [
        "from collections import namedtuple\nC = namedtuple('C', 'a b', rename=True)\n",
        "from collections import namedtuple\nfields = 'a b'\nC = namedtuple('C', fields)\n",
        "from typing import NamedTuple\nclass B(NamedTuple):\n    a: int\nclass C(B):\n    def __init__(self, x, y): ...\n",
    ],
)
def test_a_named_tuple_that_is_not_literal_is_left_alone(tmp_path: Path, declaration: str) -> None:
    assert isinstance(resolve_text(tmp_path, f"{declaration}C(1, 2)\n", "C"), Skipped)


# Methods.

_BASE = """\
class Base:
    def _step(self, a, b): ...
    def run(self):
        self._step(1, 2)
"""


def test_a_method_called_through_self_binds_without_the_receiver(tmp_path: Path) -> None:
    result = resolve_text(tmp_path, _BASE, "self._step")

    assert names(result, 2) == ("a", "b")
    assert isinstance(result, Binding)
    assert result.callee == "main.Base._step"


def test_a_subclass_that_renames_a_parameter_makes_the_call_unsafe(tmp_path: Path) -> None:
    text = f"{_BASE}class Child(Base):\n    def _step(self, b, a): ...\n"
    assert reason(resolve_text(tmp_path, text, "self._step")) == Reason.DISPATCH_UNSAFE


def test_a_subclass_that_inherits_the_method_keeps_the_call_safe(tmp_path: Path) -> None:
    text = f"{_BASE}class Child(Base):\n    def other(self, b, a): ...\n"
    assert names(resolve_text(tmp_path, text, "self._step"), 2) == ("a", "b")


def test_a_mixin_ahead_of_the_class_in_a_subclass_makes_the_call_unsafe(tmp_path: Path) -> None:
    text = f"{_BASE}class Mixin:\n    def _step(self, x, y): ...\nclass Child(Mixin, Base): pass\n"
    assert reason(resolve_text(tmp_path, text, "self._step")) == Reason.DISPATCH_UNSAFE


def test_a_subclass_inside_a_function_is_checked_too(tmp_path: Path) -> None:
    text = f"{_BASE}def make():\n    class Child(Base):\n        def _step(self, b, a): ...\n    return Child\n"
    assert reason(resolve_text(tmp_path, text, "self._step")) == Reason.DISPATCH_UNSAFE


def test_a_subclass_with_an_unknown_base_that_defines_the_name_makes_the_call_unsafe(tmp_path: Path) -> None:
    text = f"{_BASE}def factory(): return object\nclass Other(factory()):\n    def _step(self, b, a): ...\n"
    assert reason(resolve_text(tmp_path, text, "self._step")) == Reason.DISPATCH_UNSAFE


@pytest.mark.parametrize(
    ("methods", "attribute", "expected"),
    [
        ("private", "run", False),
        ("private", "_step", True),
        ("private", "__step", True),
        ("private", "__init__", False),
        ("none", "_step", False),
        ("all", "run", True),
    ],
)
def test_the_methods_option_picks_the_names_a_dispatched_call_may_bind_to(
    tmp_path: Path, methods: MethodsMode, attribute: str, expected: bool
) -> None:
    text = f"class C:\n    def {attribute}(self, a, b): ...\n    def go(self):\n        self.{attribute}(1, 2)\n"
    result = resolve_text(tmp_path, text, f"self.{attribute}", methods=methods)

    assert isinstance(result, Binding) if expected else reason(result) == Reason.METHODS_DISABLED


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        (f"{_BASE}    def reset(self):\n        self._step = print\n", Reason.NAME_REBOUND),
        (
            "class C:\n    def _step(self, a, b): ...\n    def run(self):\n        self = other\n        self._step(1, 2)\n",
            Reason.RECEIVER_IS_VALUE,
        ),
        ("class C:\n    _step = print\n    def run(self):\n        self._step(1, 2)\n", Reason.ASSIGNED_VALUE),
        (
            "class C:\n    @property\n    def _step(self): ...\n    def run(self):\n        self._step(1, 2)\n",
            Reason.DECORATOR,
        ),
        ("class C:\n    def run(self):\n        self._step(1, 2)\n", Reason.UNBOUND_NAME),
        ("class C(dict):\n    def run(self):\n        self._step(1, 2)\n    _step = dict.get\n", Reason.ASSIGNED_VALUE),
        (
            "import sys\nif sys:\n    class C:\n        def _step(self, a, b): ...\n        def run(self):\n            self._step(1, 2)\n",
            Reason.RECEIVER_IS_VALUE,
        ),
        (
            "import _thread\nclass C(_thread.RLock):\n    def run(self):\n        self._step(1, 2)\n",
            Reason.UNREADABLE_MODULE,
        ),
    ],
)
def test_a_method_the_receiver_may_not_reach_is_left_alone(tmp_path: Path, text: str, expected: Reason) -> None:
    assert reason(resolve_text(tmp_path, text, "self._step")) == expected


def test_an_unreadable_base_after_the_definition_keeps_the_call(tmp_path: Path) -> None:
    text = "import _thread\nclass C(_thread.RLock):\n    def _step(self, a, b): ...\n    def run(self):\n        self._step(1, 2)\n"
    assert names(resolve_text(tmp_path, text, "self._step"), 2) == ("a", "b")


def test_a_call_through_cls_binds_to_a_class_method(tmp_path: Path) -> None:
    text = "class C:\n    @classmethod\n    def _make(cls, a, b): ...\n    @classmethod\n    def build(cls):\n        cls._make(1, 2)\n"
    assert names(resolve_text(tmp_path, text, "cls._make"), 2) == ("a", "b")


def test_a_call_inside_a_property_binds_through_self(tmp_path: Path) -> None:
    text = "class C:\n    def _step(self, a, b): ...\n    @property\n    def value(self):\n        return self._step(1, 2)\n"
    assert names(resolve_text(tmp_path, text, "self._step"), 2) == ("a", "b")


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ("class C:\n    @staticmethod\n    def m(a, b): ...\nC.m(1, 2)\n", ("a", "b")),
        ("class C:\n    @classmethod\n    def m(cls, a, b): ...\nC.m(1, 2)\n", ("a", "b")),
        ("class C:\n    def m(self, a): ...\nC.m(C(), 2)\n", ("self", "a")),
        ("class B:\n    @staticmethod\n    def m(a, b): ...\nclass C(B): pass\nC.m(1, 2)\n", ("a", "b")),
    ],
)
def test_a_method_read_from_the_class_binds_without_dispatch(
    tmp_path: Path, text: str, expected: tuple[str, ...]
) -> None:
    assert names(resolve_text(tmp_path, text, "C.m"), 2) == expected


def test_a_call_through_super_binds_to_the_next_definition(tmp_path: Path) -> None:
    text = "class B:\n    def __init__(self, a, b): ...\nclass C(B):\n    def __init__(self):\n        super().__init__(1, 2)\n"
    assert names(resolve_text(tmp_path, text, "super().__init__"), 2) == ("a", "b")


def test_a_call_through_super_binds_to_the_init_a_dataclass_writes(tmp_path: Path) -> None:
    text = "from dataclasses import dataclass\n@dataclass\nclass B:\n    a: int\n    b: int\nclass C(B):\n    def __init__(self):\n        super(C, self).__init__(1, 2)\n"
    assert names(resolve_text(tmp_path, text, "super(C, self).__init__"), 2) == ("a", "b")


def test_a_call_through_super_is_unsafe_when_a_subclass_puts_a_class_between(tmp_path: Path) -> None:
    text = (
        "class B:\n    def _step(self, a, b): ...\nclass C(B):\n    def _step(self, a, b):\n        super()._step(1, 2)\n"
        "class Other(B):\n    def _step(self, b, a): ...\nclass D(C, Other): pass\n"
    )

    assert reason(resolve_text(tmp_path, text, "super()._step")) == Reason.DISPATCH_UNSAFE


def test_a_call_through_super_in_a_nested_function_is_left_alone(tmp_path: Path) -> None:
    text = "class B:\n    def _step(self, a, b): ...\nclass C(B):\n    def _step(self, a, b):\n        def inner():\n            super()._step(1, 2)\n"
    assert reason(resolve_text(tmp_path, text, "super()._step")) == Reason.RECEIVER_IS_VALUE


def test_a_call_from_another_tree_raises(tmp_path: Path) -> None:
    write_tree(tmp_path, {"main.py": "def f(a, b): ...\n"})

    text = (tmp_path / "main.py").read_text(encoding="utf-8")
    context = ModuleContext.build(ast.parse(text), tmp_path / "main.py", text)
    call = find_call(ast.parse("f(1, 2)"), "f")

    with pytest.raises(ValueError, match="not part"):
        resolve_call(call, context, SourceIndex([tmp_path], installed=()), ALL)


@pytest.mark.parametrize(
    ("text", "callee", "receiver"),
    [
        ("class C:\n    def m(self, a): ...\nC.m(C(), 2)\n", "C.m", True),
        ("class C:\n    @staticmethod\n    def m(a, b): ...\nC.m(1, 2)\n", "C.m", False),
        ("class C:\n    @classmethod\n    def m(cls, a, b): ...\nC.m(1, 2)\n", "C.m", False),
        ("class C:\n    def m(self, a): ...\n    def run(self):\n        self.m(1)\n", "self.m", False),
        ("def m(a, b): ...\nm(1, 2)\n", "m", False),
    ],
)
def test_a_binding_says_whether_the_call_passes_the_receiver_itself(
    tmp_path: Path, text: str, callee: str, receiver: bool
) -> None:
    result = resolve_text(tmp_path, text, callee)

    assert isinstance(result, Binding)
    assert result.receiver is receiver
