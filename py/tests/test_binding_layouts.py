"""The call resolver across files: module search, packages, imports, stubs, sources and the version the engine passes."""

from __future__ import annotations

import ast
import importlib.machinery
import subprocess
import sys
from pathlib import Path

import pytest

from nickelony_stanza import Config, fix, lint
from nickelony_stanza.analysis import module_index
from nickelony_stanza.analysis.binding import (
    SOURCES,
    Binding,
    ModuleContext,
    Reason,
    ResolveOptions,
    Skipped,
    Source,
    SourceIndex,
    detect_roots,
    resolve_call,
)
from nickelony_stanza.analysis.module_index import differs_only_in_call_names
from nickelony_stanza.analysis.projects import Projects
from tests.test_binding import ALL, find_call, names, reason, resolve_in, write_tree

EXTENSION = importlib.machinery.EXTENSION_SUFFIXES[0]


def _project(tmp_path: Path, files: dict[str, str], *, installed: tuple[Path, ...] = ()) -> SourceIndex:
    write_tree(tmp_path, files)
    return SourceIndex([tmp_path], installed=installed)


def test_detect_roots_finds_the_project_directory_and_its_src_directory(tmp_path: Path) -> None:
    write_tree(
        tmp_path,
        {"pyproject.toml": "", "src/pkg/__init__.py": "", "src/pkg/sub/__init__.py": "", "src/pkg/sub/m.py": ""},
    )

    roots = detect_roots(tmp_path / "src" / "pkg" / "sub" / "m.py")
    normalized = [str(root).lower() for root in roots]

    assert normalized == [str(path.resolve()).lower() for path in (tmp_path, tmp_path / "src")]


def test_a_directory_without_an_init_file_inside_a_package_is_no_root(tmp_path: Path) -> None:
    write_tree(
        tmp_path,
        {
            "pyproject.toml": "",
            "src/pkg/__init__.py": "",
            "src/pkg/portion/shapes.py": "def draw(a, b): ...\n",
            "src/pkg/portion/main.py": "from .shapes import draw\ndraw(1, 2)\n",
        },
    )

    main = tmp_path / "src" / "pkg" / "portion" / "main.py"
    roots = detect_roots(main)

    assert [str(root).lower() for root in roots] == [
        str(path.resolve()).lower() for path in (tmp_path, tmp_path / "src")
    ]

    assert names(resolve_in(SourceIndex(roots, installed=()), main, "draw"), 2) == ("a", "b")


def test_a_src_layout_resolves_a_test_that_imports_the_package(tmp_path: Path) -> None:
    write_tree(
        tmp_path,
        {
            "pyproject.toml": "",
            "src/pkg/__init__.py": "",
            "src/pkg/mod.py": "def f(a, b): ...\n",
            "tests/test_mod.py": "from pkg.mod import f\nf(1, 2)\n",
        },
    )

    index = SourceIndex(detect_roots(tmp_path / "tests" / "test_mod.py"), installed=())
    result = resolve_in(index, tmp_path / "tests" / "test_mod.py", "f")

    assert names(result, 2) == ("a", "b")
    assert isinstance(result, Binding)
    assert (result.callee, result.source) == ("pkg.mod.f", "project")


@pytest.mark.parametrize(
    ("user", "callee"),
    [
        ("from .b import f\nf(1, 2)\n", "f"),
        ("from . import b\nb.f(1, 2)\n", "b.f"),
        ("from .. import top\ntop.g(1, 2)\n", "top.g"),
        ("import pkg.inner.b as m\nm.f(1, 2)\n", "m.f"),
        ("import pkg.inner.b\npkg.inner.b.f(1, 2)\n", "pkg.inner.b.f"),
        ("import pkg\npkg.f(1, 2)\n", "pkg.f"),
        ("from pkg import f\nf(1, 2)\n", "f"),
        ("from pkg.inner import f\nf(1, 2)\n", "f"),
    ],
)
def test_an_import_resolves_through_relative_names_submodules_and_re_exports(
    tmp_path: Path, user: str, callee: str
) -> None:
    index = _project(
        tmp_path,
        {
            "pkg/__init__.py": "from .inner import f\n",
            "pkg/top.py": "def g(a, b): ...\n",
            "pkg/inner/__init__.py": "from .b import *\n",
            "pkg/inner/b.py": "__all__ = ['f']\ndef f(a, b): ...\n",
            "pkg/inner/a.py": user,
        },
    )

    assert names(resolve_in(index, tmp_path / "pkg" / "inner" / "a.py", callee), 2) == ("a", "b")


def test_a_relative_import_beyond_the_top_package_is_unreadable(tmp_path: Path) -> None:
    index = _project(tmp_path, {"pkg/__init__.py": "", "pkg/a.py": "from ... import x\nx.f(1, 2)\n"})
    assert reason(resolve_in(index, tmp_path / "pkg" / "a.py", "x.f")) == Reason.UNREADABLE_MODULE


def test_a_namespace_package_joins_its_portions_across_roots(tmp_path: Path) -> None:
    write_tree(
        tmp_path,
        {
            "one/ns/first.py": "def f(a, b): ...\n",
            "two/ns/second.py": "from ns import first\nimport ns.first\nfirst.f(1, 2)\nns.first.f(1, 2)\n",
        },
    )

    index = SourceIndex([tmp_path / "one", tmp_path / "two"], installed=())
    path = tmp_path / "two" / "ns" / "second.py"

    assert names(resolve_in(index, path, "first.f"), 2) == ("a", "b")
    assert names(resolve_in(index, path, "ns.first.f"), 2) == ("a", "b")


def test_a_package_directory_wins_over_a_module_file_and_a_module_file_over_a_namespace(tmp_path: Path) -> None:
    index = _project(
        tmp_path,
        {
            "both/__init__.py": "def f(a, b): ...\n",
            "both.py": "def f(x, y): ...\n",
            "half/inner.py": "",
            "half.py": "def f(a, b): ...\n",
            "main.py": "import both, half\nboth.f(1, 2)\nhalf.f(1, 2)\n",
        },
    )

    assert names(resolve_in(index, tmp_path / "main.py", "both.f"), 2) == ("a", "b")
    assert names(resolve_in(index, tmp_path / "main.py", "half.f"), 2) == ("a", "b")


@pytest.mark.parametrize(
    ("files", "expected"),
    [
        ({"mod.py": "def f(a, b): ...\n", "mod.pyi": "def f(x: int, y: int) -> None: ...\n"}, Reason.STUB_PRESENT),
        ({"mod.py": "def f(a, b): ...\n", f"mod{EXTENSION}": ""}, Reason.UNREADABLE_MODULE),
        ({"mod.py": "def f(a, b): ...\nfrom _accel import *\n", f"_accel{EXTENSION}": ""}, Reason.UNREADABLE_MODULE),
        (
            {
                "mod.py": "try:\n    from fast import f\nexcept ImportError:\n    from slow import f\n",
                "fast.py": "def f(a, b): ...\n",
                "slow.py": "def f(a, b): ...\n",
            },
            Reason.CONDITIONAL_BINDING,
        ),
        (
            {
                "mod.py": "from typing import TYPE_CHECKING\nif TYPE_CHECKING:\n    from slow import f\n",
                "slow.py": "def f(a, b): ...\n",
            },
            Reason.CONDITIONAL_BINDING,
        ),
        (
            {
                "mod.py": "from helpers import *\n",
                "helpers.py": "__all__ = ['g']\ndef f(a, b): ...\ndef g(a, b): ...\n",
            },
            Reason.UNBOUND_NAME,
        ),
        ({"mod/__init__.py": "f = 1\n", "mod/f.py": "def g(a, b): ...\n"}, Reason.NAME_REBOUND),
        (
            {"mod/__init__.py": "__path__ = ['elsewhere']\n", "mod/sub.py": "def f(a, b): ...\n"},
            Reason.UNREADABLE_MODULE,
        ),
    ],
)
def test_a_module_the_import_system_may_read_otherwise_is_left_alone(
    tmp_path: Path, files: dict[str, str], expected: Reason
) -> None:
    index = _project(tmp_path, {**files, "main.py": "import mod\nimport mod.sub\nmod.f(1, 2)\nmod.sub.f(1, 2)\n"})
    callee = "mod.sub.f" if "mod/sub.py" in files else "mod.f"

    assert reason(resolve_in(index, tmp_path / "main.py", callee)) == expected


def test_a_definition_after_an_unreadable_star_import_still_binds(tmp_path: Path) -> None:
    index = _project(
        tmp_path,
        {
            "mod.py": "from _accel import *\ndef f(a, b): ...\n",
            f"_accel{EXTENSION}": "",
            "main.py": "import mod\nmod.f(1, 2)\n",
        },
    )

    assert names(resolve_in(index, tmp_path / "main.py", "mod.f"), 2) == ("a", "b")


def test_a_star_import_of_a_readable_module_binds_its_public_names(tmp_path: Path) -> None:
    index = _project(
        tmp_path,
        {
            "helpers.py": "def f(a, b): ...\ndef _g(a, b): ...\n",
            "main.py": "from helpers import *\nf(1, 2)\n_g(1, 2)\n",
        },
    )

    assert names(resolve_in(index, tmp_path / "main.py", "f"), 2) == ("a", "b")
    assert reason(resolve_in(index, tmp_path / "main.py", "_g")) == Reason.UNBOUND_NAME


def test_a_project_module_that_shadows_the_standard_library_is_unreadable(tmp_path: Path) -> None:
    index = _project(
        tmp_path, {"textwrap.py": "def indent(a, b): ...\n", "main.py": "import textwrap\ntextwrap.indent(1, 2)\n"}
    )

    assert reason(resolve_in(index, tmp_path / "main.py", "textwrap.indent")) == Reason.UNREADABLE_MODULE


@pytest.mark.parametrize(
    ("sources", "expected"),
    [(SOURCES, "stdlib"), (frozenset({"project", "installed"}), Reason.SOURCE_DISABLED)],
)
def test_the_standard_library_binds_only_when_its_source_is_enabled(
    tmp_path: Path, sources: frozenset[Source], expected: str | Reason
) -> None:
    index = _project(tmp_path, {"main.py": "import textwrap\ntextwrap.indent('a', 'b')\n"})
    result = resolve_in(index, tmp_path / "main.py", "textwrap.indent", options=ResolveOptions(sources, "all"))

    if isinstance(expected, Reason):
        assert reason(result) == expected
    else:
        assert isinstance(result, Binding)
        assert (result.callee, result.source, result.names_for(2)) == ("textwrap.indent", expected, ("text", "prefix"))


def test_a_project_stub_still_refuses_its_module_after_a_dispatch_check_scans_the_project(tmp_path: Path) -> None:
    main = "from pkg.stubbed import f\n\nclass C:\n    def m(self, a, b): ...\n\n    def go(self):\n        self.m(1, 2)\n\nf(1, 2)\n"

    files = {
        "pkg/__init__.py": "",
        "pkg/stubbed.py": "def f(a, b): ...\n",
        "pkg/stubbed.pyi": "def f(x: int, y: int) -> None: ...\n",
        "main.py": main,
    }

    index = _project(tmp_path, files)

    assert names(resolve_in(index, tmp_path / "main.py", "self.m"), 2) == ("a", "b")
    assert reason(resolve_in(index, tmp_path / "main.py", "f")) == Reason.STUB_PRESENT


def test_the_cache_check_tells_a_literal_from_one_of_another_type() -> None:
    for old, new in (("x = 1", "x = True"), ("x = 1", "x = 1.0"), ("def f(a=0): ...", "def f(a=False): ...")):
        assert not differs_only_in_call_names(ast.parse(old), ast.parse(new))

    assert differs_only_in_call_names(ast.parse("def g():\n    f(1)\n"), ast.parse("def g():\n    f(a=1)\n"))


@pytest.mark.parametrize(
    ("files", "expected"),
    [
        ({"lib/vendor.py": "def f(a, b): ...\n"}, "installed"),
        ({"lib/vendor.py": "def f(a, b): ...\n", "lib/vendor.pyi": "def f(x, y): ...\n"}, Reason.STUB_PRESENT),
        ({"lib/vendor.py": "def f(a, b): ...\n", "lib/vendor-stubs/__init__.pyi": ""}, Reason.STUB_PRESENT),
    ],
)
def test_an_installed_module_binds_unless_a_stub_stands_beside_it(
    tmp_path: Path, files: dict[str, str], expected: str | Reason
) -> None:
    write_tree(tmp_path, {**files, "app/main.py": "import vendor\nvendor.f(1, 2)\n"})

    index = SourceIndex([tmp_path / "app"], installed=(tmp_path / "lib",))
    result = resolve_in(index, tmp_path / "app" / "main.py", "vendor.f")

    if isinstance(expected, Reason):
        assert reason(result) == expected
    else:
        assert isinstance(result, Binding)
        assert result.source == expected

        assert (
            reason(
                resolve_in(
                    index,
                    tmp_path / "app" / "main.py",
                    "vendor.f",
                    options=ResolveOptions(frozenset({"project"}), "all"),
                )
            )
            == Reason.SOURCE_DISABLED
        )


def test_a_subclass_in_another_module_that_renames_a_parameter_makes_the_call_unsafe(tmp_path: Path) -> None:
    files = {
        "pkg/__init__.py": "",
        "pkg/base.py": "class Base:\n    def _step(self, a, b): ...\n    def run(self):\n        self._step(1, 2)\n",
        "pkg/child.py": "from pkg import base\nclass Child(base.Base):\n    def _step(self, b, a): ...\n",
    }

    index = _project(tmp_path, files)

    assert reason(resolve_in(index, tmp_path / "pkg" / "base.py", "self._step")) == Reason.DISPATCH_UNSAFE

    other = tmp_path / "other"

    write_tree(
        other,
        {**files, "pkg/child.py": "from pkg.base import Base\nclass Child(Base):\n    def _step(self, a, b): ...\n"},
    )

    assert (
        reason(resolve_in(SourceIndex([other], installed=()), other / "pkg" / "base.py", "self._step"))
        == Reason.DISPATCH_UNSAFE
    )

    write_tree(other, {"pkg/child.py": "from pkg.base import Base\nclass Child(Base):\n    pass\n"})

    assert names(resolve_in(SourceIndex([other], installed=()), other / "pkg" / "base.py", "self._step"), 2) == (
        "a",
        "b",
    )


def test_the_text_the_engine_passes_wins_over_the_disk_and_the_disk_returns_after(tmp_path: Path) -> None:
    index = _project(tmp_path, {"helpers.py": "def f(a, b): ...\n", "main.py": "from helpers import f\nf(1, 2)\n"})
    edited = "def f(x, y): ...\n"
    tree = ast.parse(edited)

    other = ModuleContext.build(
        ast.parse("from helpers import f\nf(1, 2)\n"), tmp_path / "main.py", "from helpers import f\nf(1, 2)\n"
    )

    assert names(resolve_in(index, tmp_path / "main.py", "f"), 2) == ("a", "b")

    index.adopt(tmp_path / "helpers.py", edited, tree)
    assert names(resolve_call(find_call(other.tree, "f"), other, index, ALL), 2) == ("x", "y")

    disk = (tmp_path / "helpers.py").read_text(encoding="utf-8")
    index.adopt(tmp_path / "helpers.py", disk, ast.parse(disk))
    assert names(resolve_call(find_call(other.tree, "f"), other, index, ALL), 2) == ("a", "b")


_SHAPES = "class Shape:\n    def _line(self, a, b, c, d): ...\n    def draw(self):\n        self._line(1, 2, 3, 4)\n"


def _count_clears(monkeypatch: pytest.MonkeyPatch) -> list[int]:
    clears: list[int] = []
    original = SourceIndex._clear_derived

    def spy(self: SourceIndex) -> None:
        clears.append(1)
        original(self)

    monkeypatch.setattr(SourceIndex, "_clear_derived", spy)
    return clears


def _resolve_text(index: SourceIndex, path: Path, text: str, callee: str) -> Binding | Skipped:
    tree = ast.parse(text)
    return resolve_call(find_call(tree, callee), ModuleContext.build(tree, path, text), index, ALL)


@pytest.mark.parametrize(
    "edited",
    [
        "\n\n" + _SHAPES.replace("    def draw", "\n    def draw"),
        _SHAPES.replace("self._line(1, 2, 3, 4)", "self._line(a=1, b=2, c=3, d=4)"),
        _SHAPES.replace("self._line(1, 2, 3, 4)", "self._line(1,\n            b=2, c=3, d=4)"),
    ],
)
def test_a_text_that_differs_by_blank_lines_or_argument_names_keeps_the_caches(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, edited: str
) -> None:
    index = _project(tmp_path, {"shapes.py": _SHAPES})
    path = tmp_path / "shapes.py"

    assert names(_resolve_text(index, path, _SHAPES, "self._line"), 4) == ("a", "b", "c", "d")

    cached = dict(index.memo("classes"))
    clears = _count_clears(monkeypatch)

    assert isinstance(_resolve_text(index, path, edited, "self._line"), Binding)
    assert clears == []
    assert index.memo("classes") == cached


@pytest.mark.parametrize(
    ("edited", "expected"),
    [
        (_SHAPES.replace("def _line(self, a, b, c, d)", "def _line(self, w, x, y, z)"), ("w", "x", "y", "z")),
        (_SHAPES + "class Square(Shape):\n    def _line(self, a, b, c, d): ...\n", Reason.DISPATCH_UNSAFE),
        (_SHAPES.replace("class Shape:", "@decorate\nclass Shape:"), Reason.DECORATOR),
        (_SHAPES + "s = Shape()\nprint(s, 1, 2, sep=3)", ("a", "b", "c", "d")),
    ],
)
def test_a_text_that_changes_a_definition_clears_the_caches_and_resolves_anew(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, edited: str, expected: object
) -> None:
    index = _project(tmp_path, {"shapes.py": _SHAPES})
    path = tmp_path / "shapes.py"

    assert names(_resolve_text(index, path, _SHAPES, "self._line"), 4) == ("a", "b", "c", "d")

    clears = _count_clears(monkeypatch)
    result = _resolve_text(index, path, edited, "self._line")

    assert clears == [1]
    assert (names(result, 4) if isinstance(expected, tuple) else reason(result)) == expected


def test_a_module_level_call_that_takes_names_clears_the_caches(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    text = "from collections import namedtuple\nP = namedtuple('P', 'x y', rename=False)\ndef f(a, b): ...\nf(1, 2)\n"
    index = _project(tmp_path, {"main.py": text})
    path = tmp_path / "main.py"

    assert isinstance(_resolve_text(index, path, text, "f"), Binding)

    clears = _count_clears(monkeypatch)
    _resolve_text(index, path, text.replace("f(1, 2)", "f(a=1, b=2)"), "f")

    assert clears == [1]


def test_a_multi_pass_fix_keeps_the_caches_from_one_pass_to_the_next(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    # The second method passes identifiers of the parameters' names, so each pass resolves it and names nothing.
    matched = "    def same(self, a, b, c, d):\n        self._line(a, b, c, d)\n"

    text = (
        _SHAPES.replace(
            "        self._line(1, 2, 3, 4)", "        a = 1\n        b = 2\n        self._line(1, 2, 3, 4)"
        )
        + matched
    )

    write_tree(tmp_path, {"shapes.py": text})

    projects = Projects([tmp_path])
    config = Config.create("none", enable=["ZA3501", "ZA3002"])
    clears = _count_clears(monkeypatch)
    result = fix(text, config, path=tmp_path / "shapes.py", projects=projects)

    assert result.text == text.replace("        self._line(1, 2, 3, 4)", "\n        self._line(a=1, b=2, c=3, d=4)")
    assert clears == []
    assert lint(result.text, config, path=tmp_path / "shapes.py", projects=projects) == []


def test_the_dotted_name_of_a_file_comes_from_the_deepest_root(tmp_path: Path) -> None:
    write_tree(
        tmp_path, {"src/pkg/__init__.py": "", "src/pkg/mod.py": "", "loose/a/__init__.py": "", "loose/a/b.py": ""}
    )

    index = SourceIndex([tmp_path, tmp_path / "src"], installed=())

    assert index.module_name_for(tmp_path / "src" / "pkg" / "mod.py") == ("pkg.mod", False)
    assert index.module_name_for(tmp_path / "src" / "pkg" / "__init__.py") == ("pkg", True)
    assert SourceIndex([], installed=()).module_name_for(tmp_path / "loose" / "a" / "b.py") == ("a.b", False)


# The project scan.

_CALLER = "class Box:\n    def grow(self, a, b): ...\n\n    def double(self):\n        self.grow(1, 2)\n"

# A module that assigns `grow` as an attribute, which refuses the call through `self` wherever the scan reads it.
_SHADOW = "def reset(box):\n    box.grow = None\n"


def _dispatch(index: SourceIndex, main: Path) -> Binding | Skipped:
    return resolve_in(index, main, "self.grow")


def test_a_root_without_a_project_file_is_read_flat_with_the_packages_it_holds(tmp_path: Path) -> None:
    write_tree(tmp_path, {"script.py": _CALLER, "other/deep/shadow.py": _SHADOW})

    assert names(_dispatch(SourceIndex([tmp_path], installed=()), tmp_path / "script.py"), 2) == ("a", "b")

    for files in ({"sibling.py": _SHADOW}, {"pkg/__init__.py": "", "pkg/sub/shadow.py": _SHADOW}):
        write_tree(tmp_path, files)
        assert reason(_dispatch(SourceIndex([tmp_path], installed=()), tmp_path / "script.py")) == Reason.NAME_REBOUND


@pytest.mark.parametrize(
    "junk",
    [".venv/shadow.py", "node_modules/shadow.py", "build/shadow.py", "site-packages/shadow.py", ".hidden/shadow.py"],
)
def test_the_scan_of_a_project_skips_caches_builds_and_environments(tmp_path: Path, junk: str) -> None:
    write_tree(tmp_path, {"pyproject.toml": "", "app/main.py": _CALLER, junk: _SHADOW, "env/pyvenv.cfg": ""})
    write_tree(tmp_path, {"env/lib/shadow.py": _SHADOW})

    assert names(_dispatch(SourceIndex([tmp_path], installed=()), tmp_path / "app" / "main.py"), 2) == ("a", "b")

    write_tree(tmp_path, {"app/nested/shadow.py": _SHADOW})
    assert reason(_dispatch(SourceIndex([tmp_path], installed=()), tmp_path / "app" / "main.py")) == Reason.NAME_REBOUND


def _link(link: Path, target: Path) -> bool:
    """Links a directory: a symbolic link, else a junction on Windows, which needs no privilege."""
    try:
        link.symlink_to(target, target_is_directory=True)
        return True
    except OSError:
        if sys.platform != "win32":
            return False

    result = subprocess.run(["cmd", "/c", "mklink", "/J", str(link), str(target)], capture_output=True, check=False)
    return result.returncode == 0


def test_the_scan_follows_no_directory_link(tmp_path: Path) -> None:
    write_tree(tmp_path, {"pyproject.toml": "", "main.py": _CALLER, "outside/shadow.py": _SHADOW})

    (tmp_path / "project").mkdir()

    for name, target in (("loop", tmp_path / "project"), ("outside", tmp_path / "outside")):
        if not _link(tmp_path / "project" / name, target):
            pytest.skip("this system creates no directory links")

    write_tree(tmp_path / "project", {"pyproject.toml": "", "main.py": _CALLER})
    main = tmp_path / "project" / "main.py"

    assert names(_dispatch(SourceIndex([tmp_path / "project"], installed=()), main), 2) == ("a", "b")


def test_the_home_directory_is_read_flat_even_with_a_project_file(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    write_tree(tmp_path, {"pyproject.toml": "", "script.py": _CALLER, "projects/app/shadow.py": _SHADOW})
    monkeypatch.setattr(Path, "home", classmethod(lambda cls: tmp_path))

    assert names(_dispatch(SourceIndex([tmp_path], installed=()), tmp_path / "script.py"), 2) == ("a", "b")


@pytest.mark.parametrize("cap", ["MAX_PROJECT_FILES", "MAX_PROJECT_BYTES"])
def test_a_project_past_the_scan_caps_refuses_every_call(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, cap: str
) -> None:
    write_tree(
        tmp_path, {"pyproject.toml": "", "main.py": _CALLER + "def f(a, b): ...\nf(1, 2)\n", "b.py": "", "c.py": ""}
    )

    monkeypatch.setattr(module_index, cap, 2 if cap == "MAX_PROJECT_FILES" else 100)

    index = SourceIndex([tmp_path], installed=())

    assert reason(_dispatch(index, tmp_path / "main.py")) == Reason.SCAN_TOO_LARGE
    assert reason(resolve_in(index, tmp_path / "main.py", "f")) == Reason.SCAN_TOO_LARGE
