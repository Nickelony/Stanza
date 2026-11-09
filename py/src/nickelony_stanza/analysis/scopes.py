"""
The names a module, a class body or a function binds, read from the tree without running it.

A namespace records every binding of a module or a class body in statement order,
and marks a binding that a compound statement, such as `if`, `try`, `with` or a loop, may skip.
A scope table maps each call and each class of a module to the scope it runs in,
so a name a function binds is never taken for the module name it shadows.
"""

from __future__ import annotations

import ast
from collections import deque
from collections.abc import Callable, Iterator
from dataclasses import dataclass, field
from typing import Literal

BindingKind = Literal["def", "class", "import", "import_from", "star", "value", "other"]
ScopeKind = Literal["module", "class", "function", "lambda", "comprehension"]

_TryStar: type[ast.AST] = getattr(ast, "TryStar", ast.Try)

_Comprehension = (ast.ListComp, ast.SetComp, ast.GeneratorExp, ast.DictComp)
_FunctionDef = ast.FunctionDef | ast.AsyncFunctionDef


@dataclass(frozen=True, eq=False)
class NameBinding:
    """
    One statement that binds a name: a definition, an import, an assignment or a star import.

    `module` and `level` hold an import target as written, so a relative import resolves against the importing module.
    """

    kind: BindingKind
    node: ast.AST
    conditional: bool
    order: int
    module: str | None = None
    level: int = 0
    attribute: str | None = None
    value: ast.expr | None = None


@dataclass
class Namespace:
    """The bindings of a module or a class body, each name with its statements in source order."""

    bindings: dict[str, list[NameBinding]] = field(default_factory=dict)
    stars: list[NameBinding] = field(default_factory=list)
    declared_global: frozenset[str] = frozenset()
    all_names: tuple[str, ...] | None = None
    all_is_literal: bool = False

    def add(self, name: str, binding: NameBinding) -> None:
        """Records a binding of a name."""
        self.bindings.setdefault(name, []).append(binding)


def mangle(name: str, class_name: str | None) -> str:
    """Writes the name the compiler stores for `__name` inside a class, or the name unchanged."""
    if class_name is None or not name.startswith("__") or name.endswith("__"):
        return name

    stripped = class_name.lstrip("_")
    return f"_{stripped}{name}" if stripped else name


def target_names(target: ast.expr) -> Iterator[str]:
    """Lists the names an assignment target binds, through tuples, lists and starred targets."""
    if isinstance(target, ast.Name):
        yield target.id
    elif isinstance(target, ast.Tuple | ast.List):
        for element in target.elts:
            yield from target_names(element)
    elif isinstance(target, ast.Starred):
        yield from target_names(target.value)


def collect_namespace(body: list[ast.stmt], *, is_module: bool) -> Namespace:
    """Collects the bindings of a module or a class body, marking those a compound statement may skip."""
    namespace = Namespace()
    _NamespaceCollector(namespace).collect(body, conditional=False)

    if is_module:
        nodes = [node for statement in body for node in ast.walk(statement)]

        namespace.declared_global = frozenset(
            name for node in nodes if isinstance(node, ast.Global) for name in node.names
        )

        _read_all(namespace, mutated=any(_mutates_all(node) for node in nodes))

    return namespace


def _mutates_all(node: ast.AST) -> bool:
    return (
        isinstance(node, ast.Call)
        and isinstance(node.func, ast.Attribute)
        and isinstance(node.func.value, ast.Name)
        and node.func.value.id == "__all__"
    )


def _read_all(namespace: Namespace, *, mutated: bool) -> None:
    bindings = namespace.bindings.get("__all__", [])

    if not bindings:
        return

    binding = bindings[0]

    if mutated:
        return

    if len(bindings) == 1 and binding.kind == "value" and not binding.conditional and binding.value is not None:
        value = binding.value

        if isinstance(value, ast.List | ast.Tuple) and all(
            isinstance(element, ast.Constant) and isinstance(element.value, str) for element in value.elts
        ):
            namespace.all_names = tuple(
                str(element.value) for element in value.elts if isinstance(element, ast.Constant)
            )

            namespace.all_is_literal = True

            return

    namespace.all_names = None
    namespace.all_is_literal = False


class _NamespaceCollector:
    def __init__(self, namespace: Namespace) -> None:
        self.namespace = namespace
        self.order = 0

    def collect(self, body: list[ast.stmt], *, conditional: bool) -> None:
        for statement in body:
            self.order += 1
            self._collect_statement(statement, conditional)

            if not isinstance(statement, _FunctionDef | ast.ClassDef):
                self._collect_walrus(statement)

    def _add(
        self,
        name: str,
        kind: BindingKind,
        node: ast.AST,
        conditional: bool,
        *,
        module: str | None = None,
        level: int = 0,
        attribute: str | None = None,
        value: ast.expr | None = None,
    ) -> None:
        binding = NameBinding(kind, node, conditional, self.order, module, level, attribute, value)
        self.namespace.add(name, binding)

    def _collect_statement(self, statement: ast.stmt, conditional: bool) -> None:
        if isinstance(statement, _FunctionDef):
            self._add(statement.name, "def", statement, conditional)
        elif isinstance(statement, ast.ClassDef):
            self._add(statement.name, "class", statement, conditional)
        elif isinstance(statement, ast.Import):
            self._collect_import(statement, conditional)
        elif isinstance(statement, ast.ImportFrom):
            self._collect_import_from(statement, conditional)
        elif isinstance(statement, ast.Assign | ast.AnnAssign | ast.AugAssign):
            self._collect_assignment(statement, conditional)
        elif isinstance(statement, ast.For | ast.AsyncFor | ast.While | ast.If):
            if isinstance(statement, ast.For | ast.AsyncFor):
                self._add_targets(statement.target, statement, True)

            self.collect(statement.body + statement.orelse, conditional=True)
        elif isinstance(statement, ast.With | ast.AsyncWith):
            for item in statement.items:
                if item.optional_vars is not None:
                    self._add_targets(item.optional_vars, statement, True)

            self.collect(statement.body, conditional=True)
        elif isinstance(statement, ast.Try | _TryStar):
            self._collect_try(statement)
        elif isinstance(statement, ast.Match):
            for case in statement.cases:
                for node in ast.walk(case.pattern):
                    self._collect_capture(node, statement)

                self.collect(case.body, conditional=True)
        elif isinstance(statement, ast.Delete):
            for target in statement.targets:
                self._add_targets(target, statement, True)
        elif type(statement).__name__ == "TypeAlias":
            name = getattr(statement, "name", None)

            if isinstance(name, ast.Name):
                self._add(name.id, "other", statement, conditional)

    def _collect_try(self, statement: ast.stmt) -> None:
        body: list[ast.stmt] = getattr(statement, "body", [])
        orelse: list[ast.stmt] = getattr(statement, "orelse", [])
        finalbody: list[ast.stmt] = getattr(statement, "finalbody", [])

        handlers: list[ast.ExceptHandler] = getattr(statement, "handlers", [])

        self.collect(body + orelse + finalbody, conditional=True)

        for handler in handlers:
            if handler.name:
                self._add(handler.name, "other", handler, True)

            self.collect(handler.body, conditional=True)

    def _collect_capture(self, node: ast.AST, statement: ast.stmt) -> None:
        if isinstance(node, ast.MatchAs | ast.MatchStar) and node.name:
            self._add(node.name, "other", statement, True)
        elif isinstance(node, ast.MatchMapping) and node.rest:
            self._add(node.rest, "other", statement, True)

    def _collect_import(self, statement: ast.Import, conditional: bool) -> None:
        for alias in statement.names:
            if alias.asname:
                self._add(alias.asname, "import", statement, conditional, module=alias.name)
            else:
                top = alias.name.split(".")[0]
                self._add(top, "import", statement, conditional, module=top)

    def _collect_import_from(self, statement: ast.ImportFrom, conditional: bool) -> None:
        for alias in statement.names:
            if alias.name == "*":
                binding = NameBinding("star", statement, conditional, self.order, statement.module, statement.level)
                self.namespace.stars.append(binding)

                continue

            self._add(
                alias.asname or alias.name,
                "import_from",
                statement,
                conditional,
                module=statement.module,
                level=statement.level,
                attribute=alias.name,
            )

    def _collect_assignment(self, statement: ast.Assign | ast.AnnAssign | ast.AugAssign, conditional: bool) -> None:
        if isinstance(statement, ast.AnnAssign) and statement.value is None:
            return

        targets = statement.targets if isinstance(statement, ast.Assign) else [statement.target]

        for target in targets:
            if isinstance(target, ast.Name) and not isinstance(statement, ast.AugAssign):
                self._add(target.id, "value", statement, conditional, value=statement.value)
            else:
                self._add_targets(target, statement, conditional)

    def _add_targets(self, target: ast.expr, statement: ast.AST, conditional: bool) -> None:
        for name in target_names(target):
            self._add(name, "other", statement, conditional)

    def _collect_walrus(self, statement: ast.stmt) -> None:
        stack: list[ast.AST] = [statement]

        while stack:
            node = stack.pop()

            if isinstance(node, ast.NamedExpr) and isinstance(node.target, ast.Name):
                self._add(node.target.id, "other", node, True)

            stack.extend(
                child
                for child in ast.iter_child_nodes(node)
                if not isinstance(child, _FunctionDef | ast.ClassDef | ast.Lambda)
            )


@dataclass(eq=False)
class Scope:
    """
    A function, lambda, comprehension or class body, or the module, with the names it binds.

    `class_name` is the class whose name mangles `__name` in the code of this scope, which nested functions inherit.
    """

    kind: ScopeKind
    node: ast.AST
    parent: Scope | None
    class_name: str | None
    parameters: set[str] = field(default_factory=set)
    assigned: set[str] = field(default_factory=set)
    declared_global: set[str] = field(default_factory=set)
    declared_nonlocal: set[str] = field(default_factory=set)

    def binds(self, name: str) -> bool:
        """Tells whether the scope binds a name as its own local, which hides the name from the scopes around it."""
        if name in self.declared_global:
            return False

        return name in self.parameters or name in self.assigned or name in self.declared_nonlocal

    def find_binding_scope(self, name: str) -> Scope | None:
        """Finds the innermost scope that binds a name for code in this scope, or None when it is a module name."""
        current: Scope | None = self
        first = True

        while current is not None and current.kind != "module":
            if name in current.declared_global:
                return None

            if (first or current.kind != "class") and current.binds(name):
                return current

            first = False
            current = current.parent

        return None


@dataclass
class ScopeTable:
    """The scope of each call and each class of a module, keyed by the identity of the node."""

    module: Scope
    call_scopes: dict[int, Scope]
    class_scopes: dict[int, Scope]
    own_scopes: dict[int, Scope]

    def scope_of_call(self, call: ast.Call) -> Scope | None:
        """Finds the scope a call runs in, or None for a call of another tree."""
        return self.call_scopes.get(id(call))


def build_scope_table(tree: ast.Module) -> ScopeTable:
    """Reads the scopes of a module in one walk."""
    module = Scope("module", tree, None, None)
    table = ScopeTable(module, {}, {}, {})

    stack: list[tuple[ast.AST, Scope]] = [(statement, module) for statement in reversed(tree.body)]

    while stack:
        node, scope = stack.pop()
        stack.extend(reversed(list(_visit(node, scope, table))))

    return table


def _new_scope(kind: ScopeKind, node: ast.AST, parent: Scope, table: ScopeTable) -> Scope:
    class_name = node.name if isinstance(node, ast.ClassDef) else parent.class_name
    scope = Scope(kind, node, parent, class_name)

    table.own_scopes[id(node)] = scope
    return scope


def _add_parameters(scope: Scope, arguments: ast.arguments) -> None:
    for argument in (*arguments.posonlyargs, *arguments.args, *arguments.kwonlyargs, arguments.vararg, arguments.kwarg):
        if argument is not None:
            scope.parameters.add(argument.arg)


def _signature_parts(arguments: ast.arguments) -> Iterator[ast.AST]:
    yield from arguments.defaults
    yield from (default for default in arguments.kw_defaults if default is not None)

    for argument in (*arguments.posonlyargs, *arguments.args, *arguments.kwonlyargs, arguments.vararg, arguments.kwarg):
        if argument is not None and argument.annotation is not None:
            yield argument.annotation


def _visit(node: ast.AST, scope: Scope, table: ScopeTable) -> Iterator[tuple[ast.AST, Scope]]:
    if isinstance(node, _FunctionDef):
        scope.assigned.add(node.name)
        yield from ((part, scope) for part in (*node.decorator_list, *_signature_parts(node.args)))

        if node.returns is not None:
            yield node.returns, scope

        inner = _new_scope("function", node, scope, table)
        _add_parameters(inner, node.args)
        yield from ((statement, inner) for statement in node.body)
    elif isinstance(node, ast.Lambda):
        yield from ((part, scope) for part in _signature_parts(node.args))
        inner = _new_scope("lambda", node, scope, table)
        _add_parameters(inner, node.args)
        yield node.body, inner
    elif isinstance(node, ast.ClassDef):
        scope.assigned.add(node.name)
        table.class_scopes[id(node)] = scope
        yield from ((part, scope) for part in (*node.decorator_list, *node.bases, *node.keywords))
        inner = _new_scope("class", node, scope, table)
        yield from ((statement, inner) for statement in node.body)
    elif isinstance(node, _Comprehension):
        yield from _visit_comprehension(node, scope, table)
    else:
        _record(node, scope, table)
        yield from ((child, scope) for child in ast.iter_child_nodes(node))


def _visit_comprehension(node: ast.AST, scope: Scope, table: ScopeTable) -> Iterator[tuple[ast.AST, Scope]]:
    generators: list[ast.comprehension] = getattr(node, "generators", [])
    inner = _new_scope("comprehension", node, scope, table)

    for index, generator in enumerate(generators):
        yield generator.iter, scope if index == 0 else inner
        yield generator.target, inner
        yield from ((condition, inner) for condition in generator.ifs)

    for name in ("elt", "key", "value"):
        part = getattr(node, name, None)

        if isinstance(part, ast.AST):
            yield part, inner


def _record(node: ast.AST, scope: Scope, table: ScopeTable) -> None:
    if isinstance(node, ast.Call):
        table.call_scopes[id(node)] = scope
    elif isinstance(node, ast.Name) and isinstance(node.ctx, ast.Store | ast.Del):
        scope.assigned.add(node.id)
    elif isinstance(node, ast.NamedExpr) and isinstance(node.target, ast.Name):
        _walrus_scope(scope).assigned.add(node.target.id)
    elif isinstance(node, ast.Global):
        scope.declared_global.update(node.names)
    elif isinstance(node, ast.Nonlocal):
        scope.declared_nonlocal.update(node.names)
    elif isinstance(node, ast.Import | ast.ImportFrom):
        scope.assigned.update((alias.asname or alias.name).split(".")[0] for alias in node.names if alias.name != "*")
    elif isinstance(node, ast.ExceptHandler | ast.MatchAs | ast.MatchStar) and node.name:
        scope.assigned.add(node.name)
    elif isinstance(node, ast.MatchMapping) and node.rest:
        scope.assigned.add(node.rest)


def _walrus_scope(scope: Scope) -> Scope:
    current = scope

    while current.kind == "comprehension" and current.parent is not None:
        current = current.parent

    return current


@dataclass(frozen=True)
class ClassSite:
    """A `class` statement of a module, with its qualified name and whether it runs in the scope of the module."""

    node: ast.ClassDef
    qualname: str
    at_module_level: bool


@dataclass(frozen=True)
class ModuleScan:
    """What one walk of a module finds: the attributes it stores or patches, and its `class` statements."""

    stored_attributes: frozenset[str]
    patched_attributes: frozenset[str]
    classes: tuple[ClassSite, ...]
    by_node: dict[int, ClassSite] = field(repr=False, compare=False)


# The node types that hold no children and no facts a scan reads: the contexts and the operators.
_INERT = frozenset(
    kind
    for kind in vars(ast).values()
    if isinstance(kind, type) and issubclass(kind, (ast.expr_context, ast.operator, ast.boolop, ast.unaryop, ast.cmpop))
)

# The calls that replace an attribute of their first argument by name, read by the last name of their callee,
# such as `setattr`, pytest's `monkeypatch.setattr` and `mock.patch.object`; `patch` alone takes a dotted target.
_ATTRIBUTE_SETTERS = frozenset({"setattr", "delattr"})
_PATCHERS = frozenset({"patch"})

# The last names of the callees that may replace an attribute by name, which a scan keeps to read after the walk.
_REPLACERS = _ATTRIBUTE_SETTERS | _PATCHERS | {"object"}


def scan_module(tree: ast.Module) -> ModuleScan:
    """
    Finds in one walk the attribute names a module assigns or deletes, mangled as the compiler stores them,
    the attribute names it replaces on a module or a class it imports, as a test double does,
    and its `class` statements.

    The replaced names are `lib.name = ...`, `setattr(lib, "name", ...)`, `monkeypatch.setattr(lib, "name", ...)`,
    `patch.object(lib, "name")` and a dotted target such as `patch("pkg.lib.name")`, where an import binds `lib`.
    The classes come in the order `ast.walk` finds them, each with the name the interpreter gives it.
    """
    stored: set[str] = set()
    imported: set[str] = set()

    stores: list[ast.Attribute] = []
    calls: list[ast.Call] = []
    sites: list[ClassSite] = []

    # Each node travels with the class that mangles its attributes, the prefix of qualified names
    # and whether a def or class holds it.
    queue: deque[tuple[ast.AST, str | None, str, bool]] = deque([(tree, None, "", False)])

    while queue:
        node, class_name, prefix, local = queue.popleft()
        kind = type(node)

        if isinstance(node, ast.Attribute):
            if isinstance(node.ctx, ast.Store | ast.Del):
                stored.add(mangle(node.attr, class_name))
                stores.append(node)
        elif isinstance(node, ast.Call):
            if (named := _literal_attribute(node)) is not None:
                stored.add(named)

            if _read_last_name(node.func) in _REPLACERS:
                calls.append(node)
        elif isinstance(node, ast.Import | ast.ImportFrom):
            imported.update((alias.asname or alias.name).split(".")[0] for alias in node.names)
        elif isinstance(node, ast.ClassDef):
            qualname = prefix + node.name
            sites.append(ClassSite(node, qualname, not local))
            class_name, prefix, local = node.name, f"{qualname}.", True
        elif isinstance(node, _FunctionDef):
            prefix, local = f"{prefix}{node.name}.<locals>.", True

        for name in kind._fields:
            value = getattr(node, name, None)

            if isinstance(value, ast.AST):
                if type(value) not in _INERT:
                    queue.append((value, class_name, prefix, local))
            elif isinstance(value, list):
                queue.extend(
                    (item, class_name, prefix, local)
                    for item in value
                    if isinstance(item, ast.AST) and type(item) not in _INERT
                )

    def is_imported(expression: ast.expr) -> bool:
        while isinstance(expression, ast.Attribute | ast.Subscript):
            expression = expression.value

        return isinstance(expression, ast.Name) and expression.id in imported

    patched = {store.attr for store in stores if is_imported(store.value)}

    for call in calls:
        patched.update(_read_patched_names(call, is_imported))

    classes = tuple(sites)
    return ModuleScan(frozenset(stored), frozenset(patched), classes, {id(site.node): site for site in classes})


def _literal_attribute(node: ast.AST) -> str | None:
    """Reads the attribute a `setattr` or `delattr` call names by a string literal."""
    if (
        not isinstance(node, ast.Call)
        or not isinstance(node.func, ast.Name)
        or node.func.id not in ("setattr", "delattr")
    ):
        return None

    if len(node.args) < 2 or not isinstance(node.args[1], ast.Constant) or not isinstance(node.args[1].value, str):
        return None

    return node.args[1].value


def _read_patched_names(call: ast.Call, is_imported: Callable[[ast.expr], bool]) -> list[str]:
    function = call.func
    last = _read_last_name(function)
    arguments = [*call.args, *(keyword.value for keyword in call.keywords)]
    strings = [arg.value for arg in arguments[:2] if isinstance(arg, ast.Constant) and isinstance(arg.value, str)]

    is_patch_object = (
        last == "object" and isinstance(function, ast.Attribute) and _read_last_name(function.value) == "patch"
    )

    if (last in _ATTRIBUTE_SETTERS or is_patch_object) and arguments and is_imported(arguments[0]) and strings:
        return [strings[0]]

    if last in (_ATTRIBUTE_SETTERS | _PATCHERS) and strings and "." in strings[0]:
        return [strings[0].rpartition(".")[2]]

    return []


def _read_last_name(expression: ast.expr) -> str:
    if isinstance(expression, ast.Attribute):
        return expression.attr

    return expression.id if isinstance(expression, ast.Name) else ""
