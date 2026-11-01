"""
Tells which parameter each argument of a call binds to, without importing or running any code.

`resolve_call` and `FileResolver` settle the callee of a call
from the source of the file, the project and the libraries it may read,
and give either the parameters the call binds to or the reason it leaves the call alone.
It binds a call only when the definition it reads is the one the interpreter runs,
so every gap in what it can see leaves the call unresolved.

- A call with `*args` or `**kwargs`, a positional argument that lands in `*args`,
  more positional arguments than the callee takes, or a positional argument whose parameter a keyword also names
  is left alone.
- A positional-only parameter or a mangled one, such as `__key`, cannot be named,
  so `Binding.names_for` gives None for the argument that fills it.
- A callee wrapped by a decorator the resolver does not see through, such as `functools.lru_cache`, is left alone,
  because the wrapper may bind its arguments in another way.
- A callee read through a module that a `.pyi` stub stands beside is left alone, because a type checker reads the stub.
- A callee that a project module replaces on the module or class it imports, as `monkeypatch.setattr` does,
  is left alone, because the double may take other names.
- A form a type checker reads only from positional arguments, such as `typing.cast`, is left alone,
  and the parameters of a dunder method other than `__init__`, `__new__`, `__call__`, `__post_init__`
  and `__init_subclass__` are read as positional-only, as a type checker reads them.
- `ResolveOptions.sources` names the sources a resolution may read, and `ResolveOptions.methods`
  the methods a call through `self`, `cls` or `super()` may bind to.
"""

from __future__ import annotations

import ast
from dataclasses import dataclass, field
from pathlib import Path

from nickelony_stanza.analysis.module_index import SourceIndex, detect_roots, digest_of
from nickelony_stanza.analysis.resolver import Resolver
from nickelony_stanza.analysis.scopes import ScopeTable, build_scope_table
from nickelony_stanza.analysis.shapes import (
    POSITIONAL_KINDS,
    SOURCES,
    MethodsMode,
    Parameter,
    ParameterKind,
    Reason,
    ResolveOptions,
    Skipped,
    Source,
)

__all__ = [
    "SOURCES",
    "Binding",
    "FileResolver",
    "MethodsMode",
    "ModuleContext",
    "Parameter",
    "ParameterKind",
    "Reason",
    "ResolveOptions",
    "Skipped",
    "Source",
    "SourceIndex",
    "detect_roots",
    "resolve_call",
]


@dataclass(frozen=True)
class Binding:
    """The parameters a call binds to, in declaration order, with the dotted name and the source of the callee."""

    callee: str
    source: Source
    parameters: tuple[Parameter, ...]

    # True when the first argument is the receiver of a method read from its class, as in `Class.method(instance, x)`.
    receiver: bool = False

    def names_for(self, positional_arguments: int) -> tuple[str | None, ...]:
        """Lists the name each of the first positional arguments could take, or None where it cannot take one."""
        positional = [parameter for parameter in self.parameters if parameter.kind in POSITIONAL_KINDS]

        return tuple(
            positional[index].name if index < len(positional) and positional[index].can_be_named else None
            for index in range(positional_arguments)
        )


@dataclass
class ModuleContext:
    """The file a rule analyzes: its tree, its path and its text; the scope of each of its calls is read on demand."""

    tree: ast.Module
    path: Path
    text: str
    digest: str = field(repr=False)
    _scopes: ScopeTable | None = field(default=None, repr=False)

    @classmethod
    def build(cls, tree: ast.Module, path: Path, text: str) -> ModuleContext:
        """Reads a parsed file; the text lets the index tell whether the disk holds the same version."""
        return cls(tree, path, text, digest_of(text))

    @property
    def scopes(self) -> ScopeTable:
        """The scope table of the tree, built once."""
        if self._scopes is None:
            self._scopes = build_scope_table(self.tree)

        return self._scopes


class FileResolver:
    """
    Resolves the calls of one file against an index, which adopts the file and gives the resolver its module once.

    A file whose tree the index holds already, as the engine's does, shares the one scope table of that module.
    """

    def __init__(self, context: ModuleContext, index: SourceIndex, options: ResolveOptions) -> None:
        self._context = context
        self._index = index
        self._options = options
        self._ready: tuple[Resolver, ScopeTable] | None = None

    def resolve(self, call: ast.Call) -> Binding | Skipped:
        """
        Resolves the parameters a call of the context's tree binds to, or why it leaves the call alone.

        A call node from another tree raises `ValueError`, because its scope is unknown.
        """
        if any(isinstance(argument, ast.Starred) for argument in call.args) or any(
            keyword.arg is None for keyword in call.keywords
        ):
            return Skipped(Reason.CALL_SHAPE)

        resolver, scopes = self._prepare()
        scope = scopes.scope_of_call(call)

        if scope is None:
            raise ValueError("the call is not part of the context's tree")

        callee = resolver.resolve(call, scope)

        if isinstance(callee, Skipped):
            return callee

        positional = [parameter for parameter in callee.parameters if parameter.kind in POSITIONAL_KINDS]

        if len(call.args) > len(positional):
            return Skipped(Reason.CALL_SHAPE)

        keywords = {keyword.arg for keyword in call.keywords}

        if any(parameter.name in keywords for parameter in positional[: len(call.args)]):
            return Skipped(Reason.CALL_SHAPE)

        return Binding(callee.name, callee.source, callee.parameters, callee.receiver)

    def _prepare(self) -> tuple[Resolver, ScopeTable]:
        if self._ready is None:
            context = self._context
            module = self._index.adopt(context.path, context.text, context.tree, context.digest)
            scopes = module.scopes if module.tree is context.tree else context.scopes

            self._ready = (Resolver(self._index, self._options, module, scopes), scopes)

        return self._ready


def resolve_call(
    call: ast.Call, context: ModuleContext, index: SourceIndex, options: ResolveOptions
) -> Binding | Skipped:
    """
    Resolves the parameters a call of the context's tree binds to, or why it leaves the call alone.

    A call node from another tree raises `ValueError`, because its scope is unknown.
    A caller that resolves several calls of one file uses a `FileResolver`, which adopts the file once.
    """
    return FileResolver(context, index, options).resolve(call)
