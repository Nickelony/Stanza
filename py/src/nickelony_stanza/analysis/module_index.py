"""
Finds the modules a file imports by searching directories, and parses each one once for a whole run.

The search never imports anything and never asks `importlib` for a spec,
because finding a submodule imports its parents.
It follows the order of the import system within each directory:

- A package directory with an `__init__` file wins over a module file of the same name.
- An extension module, such as a `.pyd` or `.so` file, wins over a source file, so a module that has one is unreadable.
- A directory without an `__init__` file is a portion of a namespace package,
  which only forms when no regular package or module of that name stands in any directory searched.
- A builtin module, a module found only as bytecode, and a module inside a zip archive are unreadable.

The project roots are searched first, then the directories of `sys.path` in order.
A top-level name that the roots and the standard library both define is unreadable,
because the interpreter's startup may have loaded the standard one first.
A module of another source that a root shadows is read from the root.

One index serves the files of one process, such as a flake8 worker.
A file the engine checks takes its tree from the index when the index holds the same text, so the file is parsed once.
A file the engine passes with text that differs from the disk replaces the disk version for the rest of the run,
and clears every cache built from the old version.
A text that differs from the version the index holds only by blank lines,
or by arguments of calls inside function bodies that a fix passed by name,
keeps that version and its caches, because the resolver reads neither.
"""

from __future__ import annotations

import ast
import hashlib
import importlib.machinery
import os
import sys
import tokenize
import warnings
from collections.abc import Hashable, Iterable, Sequence
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Literal

from nickelony_stanza.analysis.scopes import (
    ClassSite,
    ModuleScan,
    Namespace,
    Scope,
    ScopeTable,
    build_scope_table,
    collect_namespace,
    scan_module,
)
from nickelony_stanza.analysis.shapes import Reason, Skipped, Source
from nickelony_stanza.paths import is_broad_directory, is_skipped_directory

LocatedStatus = Literal["source", "namespace", "unreadable", "missing", "shadowed"]

_EXTENSION_SUFFIXES = tuple(importlib.machinery.EXTENSION_SUFFIXES)
_SOURCE_SUFFIXES = tuple(importlib.machinery.SOURCE_SUFFIXES)
_BYTECODE_SUFFIXES = tuple(importlib.machinery.BYTECODE_SUFFIXES)
_ROOT_MARKERS = ("pyproject.toml", "setup.py", "setup.cfg")

# The caps on the project scan, past which the checks that need every project module refuse a call.
# A parsed module holds about 40 times its source in memory: the 1,770 files and 35 MB of the standard library,
# tests included, hold 1.3 GB. The caps keep a scan near 2.5 GB, and take a project the size of Django whole.
MAX_PROJECT_FILES = 20_000
MAX_PROJECT_BYTES = 64_000_000

# How the project scan reads a root: the whole tree, its own files and the regular packages it holds, or its own files.
ScanMode = Literal["tree", "packages", "flat"]


@dataclass(frozen=True)
class Located:
    """Where the search found a module: a source file, the directories of a namespace package, or nothing readable."""

    status: LocatedStatus
    source: Source | None = None
    path: Path | None = None
    search: tuple[Path, ...] = ()
    is_package: bool = False
    stub: bool = False


@dataclass(eq=False)
class ModuleInfo:
    """A parsed module with its namespace; a namespace package has an empty tree and no path."""

    name: str
    path: Path | None
    source: Source
    is_package: bool
    search: tuple[Path, ...]
    tree: ast.Module
    stub: bool
    digest: str
    _namespace: Namespace | None = field(default=None, repr=False)
    _scan: ModuleScan | None = field(default=None, repr=False)
    _scopes: ScopeTable | None = field(default=None, repr=False)
    _module_scope: Scope | None = field(default=None, repr=False)

    @property
    def namespace(self) -> Namespace:
        """The names the module binds, read on first use because a module that is only scanned never needs them."""
        if self._namespace is None:
            self._namespace = collect_namespace(self.tree.body, is_module=True)

        return self._namespace

    @property
    def scan(self) -> ModuleScan:
        """What one walk of the tree finds: the stored and patched attributes and the classes, computed once."""
        if self._scan is None:
            self._scan = scan_module(self.tree)

        return self._scan

    @property
    def stored_attributes(self) -> frozenset[str]:
        """The attribute names the module assigns or deletes."""
        return self.scan.stored_attributes

    @property
    def patched_attributes(self) -> frozenset[str]:
        """The attribute names the module replaces on a module or a class it imports."""
        return self.scan.patched_attributes

    @property
    def class_sites(self) -> tuple[ClassSite, ...]:
        """The `class` statements of the module, nested ones included."""
        return self.scan.classes

    @property
    def scopes(self) -> ScopeTable:
        """The scope table of the module, computed once; a module that is only scanned needs it for nested classes."""
        if self._scopes is None:
            self._scopes = build_scope_table(self.tree)

        return self._scopes

    @property
    def module_scope(self) -> Scope:
        """The scope of the module itself, which a class at module level runs in, without reading the whole table."""
        if self._scopes is not None:
            return self._scopes.module

        if self._module_scope is None:
            self._module_scope = Scope("module", self.tree, None, None)

        return self._module_scope

    @property
    def assigns_path(self) -> bool:
        """Tells whether the module binds `__path__`, so its submodules may live anywhere."""
        return "__path__" in self.namespace.bindings


def normalize(path: Path) -> Path:
    """Writes a path in the one spelling the index keys its caches by."""
    return Path(os.path.normcase(os.path.abspath(path)))


def digest_of(text: str) -> str:
    """Hashes a text with its line endings normalized, so two readings of one file compare equal."""
    normalized = text.replace("\r\n", "\n").replace("\r", "\n")
    return hashlib.blake2b(normalized.encode("utf-8", "surrogatepass"), digest_size=16).hexdigest()


def read_source(path: Path) -> str | None:
    """Reads a source file in the encoding its cookie declares, or None when it cannot be read."""
    try:
        with path.open("rb") as stream:
            encoding, _ = tokenize.detect_encoding(stream.readline)
            stream.seek(0)

            return stream.read().decode(encoding)
    except (OSError, SyntaxError, UnicodeDecodeError, LookupError):
        return None


def parse_source(text: str, path: Path) -> ast.Module | None:
    """Parses a source text, or None when the running interpreter cannot parse it."""
    try:
        with warnings.catch_warnings():
            warnings.simplefilter("ignore")
            return ast.parse(text, str(path))
    except (SyntaxError, ValueError, RecursionError, MemoryError):
        return None


# The builtins that set an attribute named by an argument, which the stored attributes of a module depend on.
_ATTRIBUTE_BUILTINS = frozenset({"setattr", "delattr"})


def _same_except_names(old: object, new: object, *, in_body: bool) -> bool:
    if isinstance(old, list) and isinstance(new, list):
        return len(old) == len(new) and all(
            _same_except_names(one, other, in_body=in_body) for one, other in zip(old, new, strict=True)
        )

    if not isinstance(old, ast.AST) or type(old) is not type(new):
        return type(old) is type(new) and old == new

    assert isinstance(new, ast.AST)

    if isinstance(old, ast.Call) and isinstance(new, ast.Call):
        moved = len(old.args) - len(new.args)
        named = new.keywords[: max(moved, 0)]
        builtin = isinstance(old.func, ast.Name) and old.func.id in _ATTRIBUTE_BUILTINS

        if moved and (not in_body or builtin or moved < 0 or any(keyword.arg is None for keyword in named)):
            return False

        if len(new.keywords) != moved + len(old.keywords):
            return False

        return _same_except_names(
            [old.func, *old.args, *old.keywords],
            [new.func, *new.args, *(keyword.value for keyword in named), *new.keywords[moved:]],
            in_body=in_body,
        )

    for name in old._fields:
        inside = in_body or (name == "body" and isinstance(old, ast.FunctionDef | ast.AsyncFunctionDef | ast.Lambda))

        if not _same_except_names(getattr(old, name, None), getattr(new, name, None), in_body=inside):
            return False

    return True


def differs_only_in_call_names(old: ast.Module, new: ast.Module) -> bool:
    """
    Tells whether a tree is another as a fix leaves it: equal, or with calls inside function bodies passing a tail
    of their positional arguments by name instead, which changes no definition, binding or stored attribute.
    """
    try:
        return _same_except_names(old, new, in_body=False)
    except RecursionError:
        return False


def detect_roots(path: Path) -> tuple[Path, ...]:
    """
    Detects the project roots of a file: the directory of its project file, a `src` directory beside it,
    and the directory above the outermost package that holds the file.

    A directory without an `__init__` file inside a package is a namespace portion of that package, not a root.
    """
    start = normalize(path)
    directory = start if start.is_dir() else start.parent

    roots: list[Path] = []

    for candidate in (directory, *directory.parents):
        if any((candidate / marker).is_file() for marker in _ROOT_MARKERS):
            roots.append(candidate)

            if (candidate / "src").is_dir():
                roots.append(candidate / "src")

            break

    top = directory

    while top != top.parent and (_is_package(top) or _is_package(top.parent)):
        top = top.parent

    roots.append(top)
    return tuple(dict.fromkeys(roots))


def _is_package(directory: Path) -> bool:
    return any((directory / f"__init__{suffix}").is_file() for suffix in _SOURCE_SUFFIXES)


def _scan_mode(root: Path, *, configured: bool) -> ScanMode:
    """Reads how the project scan reads a root: whole when a project file or the configuration backs it, else flat."""
    if is_broad_directory(root):
        return "flat"

    backed = configured or any((root / marker).is_file() for marker in _ROOT_MARKERS)

    if backed or (root.name == "src" and any((root.parent / marker).is_file() for marker in _ROOT_MARKERS)):
        return "tree"

    return "packages"


def _is_skipped_source_directory(directory: Path) -> bool:
    """Tells whether the project scan skips a directory: a hidden one, a cache, a build or a virtual environment."""
    name = directory.name

    if is_skipped_directory(name) or name.startswith(".") or name == "site-packages":
        return True

    return (directory / "pyvenv.cfg").is_file()


def _is_junction(entry: os.DirEntry[str]) -> bool:
    is_junction = getattr(entry, "is_junction", None)
    return bool(is_junction()) if is_junction is not None else False


def _is_under(path: Path, root: Path) -> bool:
    return path == root or root in path.parents


def _default_search() -> tuple[tuple[Path, ...], tuple[Path, ...]]:
    base = normalize(Path(sys.base_prefix))

    stdlib: list[Path] = []
    installed: list[Path] = []

    for entry in sys.path:
        if not entry:
            continue

        path = normalize(Path(entry))
        is_site = any(part in ("site-packages", "dist-packages") for part in path.parts)

        if _is_under(path, base) and not is_site:
            stdlib.append(path)
        else:
            installed.append(path)

    return tuple(stdlib), tuple(installed)


class SourceIndex:
    """
    Finds and parses the modules of one run, and holds the caches the resolver builds from them.

    Concurrent threads may parse a module twice, which wastes work but never mixes two versions of a cache.
    """

    def __init__(
        self,
        roots: Iterable[Path],
        *,
        stdlib: Sequence[Path] | None = None,
        installed: Sequence[Path] | None = None,
        configured: bool = False,
    ) -> None:
        default_stdlib, default_installed = _default_search() if stdlib is None or installed is None else ((), ())

        self.roots = tuple(dict.fromkeys(normalize(root) for root in roots if Path(root).is_dir()))

        # True when the configuration names the roots, so the project scan reads each as a whole tree.
        self.configured = configured

        stdlib_entries = tuple(normalize(path) for path in (default_stdlib if stdlib is None else stdlib))
        installed_entries = tuple(normalize(path) for path in (default_installed if installed is None else installed))

        self._entries: tuple[tuple[Path, Source], ...] = tuple(
            dict.fromkeys(
                [
                    *((path, "stdlib") for path in stdlib_entries if path not in self.roots),
                    *((path, "installed") for path in installed_entries if path not in self.roots),
                ]
            )
        )

        self._archives = frozenset(entry for entry, _ in self._entries if entry.is_file())
        self._listings: dict[Path, frozenset[str] | None] = {}
        self._located: dict[str, Located] = {}
        self._by_path: dict[Path, ModuleInfo | None] = {}
        self._overlay: dict[Path, ModuleInfo] = {}

        # The digests of the texts each path read as the version it holds, because they differ from it only by names.
        self._equivalent: dict[Path, set[str]] = {}
        self._namespaces: dict[str, ModuleInfo] = {}
        self._project: tuple[ModuleInfo, ...] | None = None
        self._project_complete = True
        self._memo: dict[str, dict[Hashable, Any]] = {}

    def memo(self, table: str) -> dict[Hashable, Any]:
        """Gives the resolver a cache of facts built from several modules, which a changed file clears."""
        return self._memo.setdefault(table, {})

    def module_name_for(self, path: Path) -> tuple[str, bool]:
        """Reads the dotted name of a file and whether it is a package, from the deepest root that holds it."""
        path = normalize(path)
        is_package = path.stem == "__init__"
        holders = [root for root in self.roots if root in path.parents]

        if holders:
            root = max(holders, key=lambda root: len(root.parts))
            parts = list(path.relative_to(root).with_suffix("").parts)
        else:
            parts = [path.stem]
            directory = path.parent

            while directory != directory.parent and (_is_package(directory) or _is_package(directory.parent)):
                parts.insert(0, directory.name)
                directory = directory.parent

        if is_package:
            parts = parts[:-1]

        return ".".join(parts), is_package

    def is_project_shadow(self, name: str) -> bool:
        """Tells whether the roots define a top-level module that hides the standard or installed one of this name."""
        located = self.locate(name.split(".")[0])

        if located.status == "shadowed":
            return True

        if located.source != "project" or located.path is None:
            return False

        return not any(_is_under(located.path, entry) for entry, source in self._entries if source == "stdlib")

    def held_module(self, path: Path, text: str) -> ModuleInfo | None:
        """Gives the module the index holds for a file when its text is the one given, so a caller shares its tree."""
        key = normalize(path)
        held = self._overlay.get(key) or self._by_path.get(key)

        return held if held is not None and held.digest == digest_of(text) else None

    def adopt(self, path: Path, text: str, tree: ast.Module, digest: str | None = None) -> ModuleInfo:
        """Reads the file a rule analyzes from the engine's text and tree, which wins over the disk for its path."""
        key = normalize(path)
        digest = digest_of(text) if digest is None else digest
        overlay = self._overlay.get(key)
        loaded = self._by_path.get(key)
        held = overlay or loaded

        if held is not None and digest in self._equivalent.get(key, ()):
            return held

        if overlay is not None and overlay.digest == digest:
            return overlay

        if loaded is not None and loaded.digest == digest:
            self._drop_overlay(key)
            return loaded

        # A fix that wrote blank lines or argument names keeps the definitions, so the version held and its caches stay.
        if held is not None and differs_only_in_call_names(held.tree, tree):
            self._equivalent.setdefault(key, set()).add(digest)
            return held

        name, is_package = self.module_name_for(key)
        info = self._build(name, key, "project", is_package, tree, digest)

        if key not in self._by_path:
            disk = read_source(key)

            if disk is not None and digest_of(disk) == digest:
                self._by_path[key] = info
                self._drop_overlay(key)

                return info

        self._overlay[key] = info
        self._equivalent.pop(key, None)
        self._clear_derived()

        return info

    def _drop_overlay(self, key: Path) -> None:
        if self._overlay.pop(key, None) is not None:
            self._equivalent.pop(key, None)
            self._clear_derived()

    def _clear_derived(self) -> None:
        self._memo.clear()
        self._project = None

    def get_module(self, name: str) -> ModuleInfo | Skipped:
        """Gets the parsed module of a dotted name, or why it cannot be read."""
        located = self.locate(name)

        if located.status in ("unreadable", "missing", "shadowed") or located.source is None:
            return Skipped(Reason.UNREADABLE_MODULE)

        if located.status == "namespace":
            return self._namespace(name, located)

        assert located.path is not None
        info = self._at_path(located.path, name, located.source, located.is_package, located.stub, located.search)

        return Skipped(Reason.UNREADABLE_MODULE) if info is None else info

    def _namespace(self, name: str, located: Located) -> ModuleInfo:
        info = self._namespaces.get(name)

        if info is None:
            tree = ast.Module(body=[], type_ignores=[])
            assert located.source is not None

            info = ModuleInfo(
                name, None, located.source, True, located.search, tree, located.stub, "", _namespace=Namespace()
            )

            self._namespaces[name] = info

        return info

    def _at_path(
        self,
        path: Path,
        name: str,
        source: Source,
        is_package: bool,
        stub: bool,
        search: tuple[Path, ...],
    ) -> ModuleInfo | None:
        # A project scan reads a module without the search that finds a stub beside it, so a later search adds it.
        held = self._overlay.get(path) or self._by_path.get(path)

        if held is not None:
            held.stub = held.stub or stub
            return held

        if path in self._by_path:
            return None

        text = read_source(path)
        tree = None if text is None else parse_source(text, path)

        info = (
            None if text is None or tree is None else self._build(name, path, source, is_package, tree, digest_of(text))
        )

        if info is not None:
            info.stub = stub
            info.search = search

        self._by_path[path] = info
        return info

    def _build(
        self, name: str, path: Path, source: Source, is_package: bool, tree: ast.Module, digest: str
    ) -> ModuleInfo:
        search = (path.parent,) if is_package else ()
        return ModuleInfo(name, path, source, is_package, search, tree, False, digest)

    def locate(self, name: str) -> Located:
        """Finds where a dotted module name lives, as the import system would, without importing anything."""
        located = self._located.get(name)

        if located is None:
            located = self._locate(name)
            self._located[name] = located

        return located

    def _locate(self, name: str) -> Located:
        parts = name.split(".")

        if not all(part.isidentifier() for part in parts):
            return Located("missing")

        if len(parts) == 1:
            return self._locate_top(name)

        parent = self.get_module(".".join(parts[:-1]))

        if isinstance(parent, Skipped) or not parent.is_package:
            return Located("missing" if isinstance(parent, ModuleInfo) else "unreadable")

        if parent.assigns_path:
            return Located("unreadable")

        found = self._search(parts[-1], [(directory, parent.source) for directory in parent.search])

        if found.status == "source" and parent.stub:
            return Located(found.status, found.source, found.path, found.search, found.is_package, True)

        return found

    def _locate_top(self, name: str) -> Located:
        if name in sys.builtin_module_names:
            return Located("unreadable")

        in_roots = self._search(name, [(root, "project") for root in self.roots], namespace=False)
        elsewhere = self._search(name, list(self._entries), namespace=False)
        stub = any(f"{name}-stubs" in (self._listing(entry) or frozenset()) for entry, _ in self._entries)

        if in_roots.status in ("source", "unreadable"):
            if (
                elsewhere.status in ("source", "unreadable")
                and elsewhere.source == "stdlib"
                and elsewhere.path != in_roots.path
            ):
                return Located("shadowed")

            return self._with_stub(in_roots, stub)

        if elsewhere.status in ("source", "unreadable"):
            return self._with_stub(elsewhere, stub)

        portions: list[tuple[Path, Source]] = [(root, "project") for root in self.roots]
        portions.extend(self._entries)

        return self._with_stub(self._search(name, portions), stub)

    @staticmethod
    def _with_stub(located: Located, stub: bool) -> Located:
        if not stub or located.source == "stdlib" or located.status not in ("source", "namespace"):
            return located

        return Located(located.status, located.source, located.path, located.search, located.is_package, True)

    def _search(self, name: str, entries: list[tuple[Path, Source]], *, namespace: bool = True) -> Located:
        portions: list[Path] = []
        portion_source: Source | None = None

        for directory, source in entries:
            if directory in self._archives:
                return Located("unreadable", source)

            found = self._search_directory(name, directory, source)

            if found is None:
                continue

            if found.status == "namespace":
                portions.extend(found.search)
                portion_source = portion_source or source

                continue

            return found

        if portions and namespace:
            return Located("namespace", portion_source, None, tuple(portions), True)

        return Located("missing")

    def _search_directory(self, name: str, directory: Path, source: Source) -> Located | None:
        listing = self._listing(directory)

        if listing is None:
            return None

        is_namespace = False

        if name in listing:
            package = directory / name
            inner = self._listing(package)

            if inner is not None:
                for suffix in (*_EXTENSION_SUFFIXES, *_SOURCE_SUFFIXES, *_BYTECODE_SUFFIXES):
                    if f"__init__{suffix}" in inner and (package / f"__init__{suffix}").is_file():
                        return self._found(package, "__init__", suffix, source, is_package=True, listing=inner)

                is_namespace = True

        for suffix in (*_EXTENSION_SUFFIXES, *_SOURCE_SUFFIXES, *_BYTECODE_SUFFIXES):
            if f"{name}{suffix}" in listing and (directory / f"{name}{suffix}").is_file():
                return self._found(directory, name, suffix, source, is_package=False, listing=listing)

        if is_namespace:
            return Located("namespace", source, None, (directory / name,), True)

        return None

    def _found(
        self, directory: Path, stem: str, suffix: str, source: Source, *, is_package: bool, listing: frozenset[str]
    ) -> Located:
        search = (directory,) if is_package else ()

        if suffix not in _SOURCE_SUFFIXES:
            return Located("unreadable", source, None, search, is_package)

        stub = source != "stdlib" and f"{stem}.pyi" in listing
        return Located("source", source, directory / f"{stem}{suffix}", search, is_package, stub)

    def _listing(self, directory: Path) -> frozenset[str] | None:
        if directory in self._listings:
            return self._listings[directory]

        try:
            listing: frozenset[str] | None = frozenset(os.listdir(directory))
        except OSError:
            listing = None

        self._listings[directory] = listing
        return listing

    @property
    def passed_caps(self) -> bool:
        """Tells whether a project scan already run stopped at the caps, without running one."""
        return not self._project_complete

    @property
    def is_project_complete(self) -> bool:
        """Tells whether the project scan read every source file of the roots, as it does unless they pass the caps."""
        self.project_modules()
        return self._project_complete

    def project_modules(self) -> tuple[ModuleInfo, ...]:
        """
        Parses every source file of the roots once, or none when the project passes the caps.

        A root backed by a project file is read as a whole tree. Any other root is read flat, with the regular packages
        it holds, and a root too broad to read, such as the home directory, is read flat alone.
        """
        if self._project is not None:
            return self._project

        paths = self._collect_project_paths()
        self._project_complete = paths is not None
        modules: list[ModuleInfo] = []

        for path in paths or ():
            name, is_package = self.module_name_for(path)
            info = self._at_path(path, name, "project", is_package, False, (path.parent,) if is_package else ())

            if info is not None:
                modules.append(info)

        if paths is not None:
            seen = set(paths)
            modules.extend(overlay for path, overlay in self._overlay.items() if path not in seen)

        self._project = tuple(modules)
        return self._project

    def _collect_project_paths(self) -> list[Path] | None:
        """Lists the source files the project scan reads, or None when they pass `MAX_PROJECT_FILES` or the byte cap."""
        found: dict[Path, None] = {}
        visited: set[tuple[int, int]] = set()
        size = 0

        for root in self.roots:
            mode = _scan_mode(root, configured=self.configured)
            stack: list[tuple[Path, bool]] = [(root, mode == "tree")]

            while stack:
                directory, whole = stack.pop()

                try:
                    status = os.stat(directory)
                    entries = sorted(os.scandir(directory), key=lambda entry: entry.name)
                except OSError:
                    continue

                # A junction or a hard link can lead back to a directory read already.
                if (status.st_dev, status.st_ino) in visited and status.st_ino:
                    continue

                visited.add((status.st_dev, status.st_ino))
                children: list[tuple[Path, bool]] = []

                for entry in entries:
                    try:
                        if entry.is_symlink() or _is_junction(entry):
                            continue

                        if entry.is_dir():
                            child = Path(entry.path)
                            descends = whole or (mode == "packages" and _is_package(child))

                            if descends and not _is_skipped_source_directory(child):
                                children.append((child, True))
                        elif entry.name.endswith(_SOURCE_SUFFIXES) and entry.is_file():
                            path = normalize(Path(entry.path))

                            if path not in found:
                                found[path] = None
                                size += entry.stat().st_size
                    except OSError:
                        continue

                    if len(found) > MAX_PROJECT_FILES or size > MAX_PROJECT_BYTES:
                        return None

                stack.extend(reversed(children))

        return list(found)
