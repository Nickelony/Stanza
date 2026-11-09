"""
Settles the definition a callee expression names, and the parameters a call of it binds to, from source alone.

A name resolves when exactly one statement binds it, outside any `if`, `try`, `with`, loop or `match`,
and no `global` statement or later star import may rebind it.
A star import binds the names its module exports: those of a literal `__all__`, or else its public names.
A star import of a module the resolver cannot read hides every name it may bind,
so a definition after it still resolves and one before it does not.

A call through `self` or `cls` resolves through the method resolution order of the method's class,
and only when every project class that inherits that class reaches the same definition, the dispatch condition.
A call through `super()` checks the same condition on the classes after the method's class in each order.

- The condition reads only the project classes, because the resolver assumes no code outside the roots
  subclasses a project class: a plugin's subclass is not seen,
  and a method an installed or standard class defines is checked against the project's subclasses alone.
- A class built at run time with `type(...)`, an attribute set by `setattr` with a computed name,
  and the metaclass of a base the resolver cannot read are not seen.
- An attribute any project module assigns under the method's name, such as `self.name = ...`, refuses the call.
"""

from __future__ import annotations

import ast
import builtins
from dataclasses import dataclass
from functools import partial
from typing import Any, Literal

from nickelony_stanza.analysis.classes import (
    ABC_META,
    CLASSMETHOD,
    DATACLASS,
    DEPRECATED,
    KNOWN_ORIGINS,
    NAMEDTUPLE_FACTORIES,
    NAMEDTUPLE_MEMBERS,
    ORDER_METHODS,
    OVERLOADS,
    PROPERTIES,
    PROTOCOLS,
    RUNTIME_BASES,
    STATICMETHOD,
    TOTAL_ORDERING,
    TRANSPARENT_CLASS_DECORATORS,
    TRANSPARENT_FUNCTION_DECORATORS,
    TYPED_NAMEDTUPLES,
    UNCALLABLE_BASES,
    ClassFacts,
    ClassInfo,
    Entry,
    Member,
    Opaque,
    Origin,
    calls_base_init,
    class_attribute_names,
    dataclass_parameters,
    is_safe_runtime_base,
    merge_orders,
    read_dataclass_fields,
    read_dataclass_params,
    read_namedtuple_call_fields,
    read_namedtuple_class_fields,
    runtime_builtin,
)
from nickelony_stanza.analysis.module_index import ModuleInfo, SourceIndex
from nickelony_stanza.analysis.scopes import NameBinding, Scope, ScopeTable, mangle
from nickelony_stanza.analysis.shapes import (
    Parameter,
    ParameterKind,
    Reason,
    ResolveOptions,
    Skipped,
    Source,
    drop_receiver,
    parameters_of,
    positional_names,
    to_positional_only,
)

_DEPTH = 32
_IN_PROGRESS = Skipped(Reason.CLASS_SHAPE)

# The callees a type checker reads only from positional arguments, so a name would fail the type check.
_SPECIAL_FORMS = frozenset(
    f"{module}.{name}"
    for module in ("typing", "typing_extensions")
    for name in ("cast", "NewType", "TypeVar", "ParamSpec", "TypeVarTuple", "TypeAliasType", "NamedTuple", "TypedDict")
) | {"collections.namedtuple"}

# The dunder methods a type checker lets a call name the arguments of; it reads every other one as positional-only.
_NAMED_DUNDERS = frozenset({"__init__", "__new__", "__call__", "__post_init__", "__init_subclass__"})

# How a function is reached: by its name, through a class object, or through an instance that fills its first parameter.
Access = Literal["plain", "class", "instance"]
MethodKind = Literal["instance", "class", "static"]
FunctionNode = ast.FunctionDef | ast.AsyncFunctionDef


@dataclass(frozen=True)
class FunctionTarget:
    """A `def` a callee names, with the class it is read from and the `@overload` variants above it."""

    module: ModuleInfo
    node: FunctionNode
    owner: ClassInfo | None
    overloads: tuple[FunctionNode, ...]
    access: Access


@dataclass(frozen=True)
class RuntimeTarget:
    """A builtin a callee names, such as `len` or `dict`."""

    value: object


Target = ModuleInfo | FunctionTarget | ClassInfo | RuntimeTarget


@dataclass(frozen=True)
class Callee:
    """What a call resolves to: its display name, the source of its definition and its parameters."""

    name: str
    source: Source
    parameters: tuple[Parameter, ...]

    # True when the call passes the receiver as its first argument, as `Class.method(instance, ...)` does.
    receiver: bool = False


def dotted_parts(expression: ast.expr) -> list[str] | None:
    """Splits a name or an attribute chain, such as `a.b.c`, into its names, or None for any other expression."""
    parts: list[str] = []

    while isinstance(expression, ast.Attribute):
        parts.append(expression.attr)
        expression = expression.value

    if not isinstance(expression, ast.Name):
        return None

    parts.append(expression.id)
    return parts[::-1]


class Resolver:
    """Resolves the calls of one module against one index; every cache it fills lives in the index."""

    def __init__(self, index: SourceIndex, options: ResolveOptions, module: ModuleInfo, scopes: ScopeTable) -> None:
        self.index = index
        self.options = options
        self.module = module
        self.scopes = scopes

        tag = ",".join(sorted(options.sources))

        self._classes: dict[Any, Any] = index.memo("classes")
        self._facts: dict[Any, Any] = index.memo(f"facts:{tag}")
        self._orders: dict[Any, Any] = index.memo(f"orders:{tag}")
        self._dispatch: dict[Any, Any] = index.memo(f"dispatch:{tag}")
        self._project: dict[Any, Any] = index.memo("project-classes")

    # Modules.

    def import_module(self, name: str | None) -> ModuleInfo | Skipped:
        """Reads a module by its dotted name, refusing one the options leave out or one a stub stands beside."""
        if name is None:
            return Skipped(Reason.UNREADABLE_MODULE)

        info = self.index.get_module(name)

        if isinstance(info, Skipped):
            return info

        if info.path is not None and info.path == self.module.path:
            return self.module

        if info.source not in self.options.sources:
            return Skipped(Reason.SOURCE_DISABLED)

        if info.stub:
            return Skipped(Reason.STUB_PRESENT)

        return info

    @staticmethod
    def absolute(module: ModuleInfo, target: str | None, level: int) -> str | None:
        """Writes the absolute name of an import, resolving a relative one against the importing module."""
        if level == 0:
            return target

        parts = module.name.split(".") if module.name else []

        if not module.is_package:
            parts = parts[:-1]

        if level - 1 >= len(parts):
            return None

        base = parts[: len(parts) - (level - 1)]
        return ".".join([*base, *(target.split(".") if target else [])])

    # Names.

    def module_attribute(self, module: ModuleInfo, name: str, depth: int = 0) -> Target | Skipped:
        """Resolves a name in the namespace of a module, then among its submodules when it is a package."""
        if depth > _DEPTH:
            return Skipped(Reason.UNBOUND_NAME)

        namespace = module.namespace

        if name in namespace.declared_global:
            return Skipped(Reason.NAME_REBOUND)

        variants, explicit = self.split_overloads(module, namespace.bindings.get(name, []), None)

        if explicit:
            return self._explicit_attribute(module, name, explicit, variants, depth)

        if variants:
            return Skipped(Reason.NAME_REBOUND)

        exporters: list[NameBinding] = []

        for star in namespace.stars:
            verdict = self.star_exports(module, star, name, depth)

            if isinstance(verdict, Skipped):
                return verdict

            if verdict:
                exporters.append(star)

        if len(exporters) > 1:
            return Skipped(Reason.NAME_REBOUND)

        if exporters:
            if exporters[0].conditional:
                return Skipped(Reason.CONDITIONAL_BINDING)

            target = self.import_module(self.absolute(module, exporters[0].module, exporters[0].level))
            return target if isinstance(target, Skipped) else self.module_attribute(target, name, depth + 1)

        if module.is_package and self.index.locate(f"{module.name}.{name}").status != "missing":
            return self.import_module(f"{module.name}.{name}")

        return Skipped(Reason.UNBOUND_NAME)

    def _explicit_attribute(
        self,
        module: ModuleInfo,
        name: str,
        explicit: list[NameBinding],
        variants: tuple[FunctionNode, ...],
        depth: int,
    ) -> Target | Skipped:
        if any(binding.conditional for binding in explicit):
            return Skipped(Reason.CONDITIONAL_BINDING)

        if (
            len({(binding.module, binding.kind) if binding.kind == "import" else id(binding) for binding in explicit})
            > 1
        ):
            return Skipped(Reason.NAME_REBOUND)

        binding = explicit[-1]

        for star in module.namespace.stars:
            if star.order > binding.order:
                verdict = self.star_exports(module, star, name, depth)

                if isinstance(verdict, Skipped):
                    return verdict

                if verdict:
                    return Skipped(Reason.NAME_REBOUND)

        target = self.follow(module, binding, variants, depth)
        submodule = f"{module.name}.{name}"
        is_submodule = isinstance(target, ModuleInfo) and target.name == submodule

        if module.is_package and not is_submodule and self.index.locate(submodule).status != "missing":
            return Skipped(Reason.NAME_REBOUND)

        return target

    def star_exports(self, module: ModuleInfo, star: NameBinding, name: str, depth: int) -> bool | Skipped:
        """Tells whether a star import binds a name, or why that cannot be known."""
        target = self.import_module(self.absolute(module, star.module, star.level))

        if isinstance(target, Skipped):
            return target

        namespace = target.namespace

        if namespace.all_is_literal:
            return name in (namespace.all_names or ())

        if "__all__" in namespace.bindings:
            possible = self.possibly_binds(target, name, depth + 1)
            return Skipped(Reason.NAME_REBOUND) if possible is True else possible

        if name.startswith("_"):
            return False

        return self.possibly_binds(target, name, depth + 1)

    def possibly_binds(self, module: ModuleInfo, name: str, depth: int) -> bool | Skipped:
        """Tells whether any statement of a module may bind a name, its own star imports included."""
        if depth > _DEPTH:
            return Skipped(Reason.UNBOUND_NAME)

        if name in module.namespace.bindings:
            return True

        for star in module.namespace.stars:
            verdict = self.star_exports(module, star, name, depth)

            if verdict is not False:
                return verdict

        return False

    def follow(
        self,
        module: ModuleInfo,
        binding: NameBinding,
        variants: tuple[FunctionNode, ...],
        depth: int,
    ) -> Target | Skipped:
        """Resolves what one binding statement binds."""
        node = binding.node

        if binding.kind == "def" and isinstance(node, FunctionNode):
            return FunctionTarget(module, node, None, variants, "plain")

        if binding.kind == "class" and isinstance(node, ast.ClassDef):
            return self.class_info(module, node)

        if binding.kind == "import":
            return self.import_module(binding.module)

        if binding.kind == "import_from":
            return self._follow_import_from(module, binding, depth)

        if binding.kind == "value" and binding.value is not None:
            return self._follow_value(module, binding.value, depth)

        return Skipped(Reason.ASSIGNED_VALUE)

    def _follow_import_from(self, module: ModuleInfo, binding: NameBinding, depth: int) -> Target | Skipped:
        base = self.absolute(module, binding.module, binding.level)

        if base is None or binding.attribute is None:
            return Skipped(Reason.UNREADABLE_MODULE)

        if base == module.name:
            return (
                self.import_module(f"{base}.{binding.attribute}") if module.is_package else Skipped(Reason.UNBOUND_NAME)
            )

        target = self.import_module(base)
        return target if isinstance(target, Skipped) else self.module_attribute(target, binding.attribute, depth + 1)

    def _follow_value(self, module: ModuleInfo, value: ast.expr, depth: int) -> Target | Skipped:
        if isinstance(value, ast.Call):
            origin = self.origin(module, value.func)

            if origin in NAMEDTUPLE_FACTORIES:
                assert origin is not None
                return self.namedtuple_info(module, value, origin)

            return Skipped(Reason.ASSIGNED_VALUE)

        if isinstance(value, ast.Name | ast.Attribute):
            return self.expression(module, None, value, depth + 1)

        return Skipped(Reason.ASSIGNED_VALUE)

    def global_name(self, module: ModuleInfo, name: str, depth: int = 0) -> Target | Skipped:
        """Resolves a name code at module level reads: a module name, else a builtin."""
        target = self.module_attribute(module, name, depth)

        if isinstance(target, Skipped) and target.reason == Reason.UNBOUND_NAME and hasattr(builtins, name):
            return RuntimeTarget(getattr(builtins, name))

        return target

    def expression(
        self, module: ModuleInfo, scope: Scope | None, expression: ast.expr, depth: int = 0
    ) -> Target | Skipped:
        """Resolves a name, an attribute chain or a subscripted class, as code in a scope reads it."""
        if depth > _DEPTH:
            return Skipped(Reason.UNBOUND_NAME)

        class_name = scope.class_name if scope is not None else None

        if isinstance(expression, ast.Name):
            if scope is not None and scope.find_binding_scope(expression.id) is not None:
                return Skipped(Reason.LOCAL_BINDING)

            return self.global_name(module, mangle(expression.id, class_name), depth)

        if isinstance(expression, ast.Attribute | ast.Subscript):
            base = self.expression(module, scope, expression.value, depth + 1)

            if isinstance(base, Skipped):
                return Skipped(Reason.RECEIVER_IS_VALUE) if base.reason == Reason.LOCAL_BINDING else base

            if isinstance(expression, ast.Subscript):
                return base if isinstance(base, ClassInfo) else Skipped(Reason.RECEIVER_IS_VALUE)

            attribute = mangle(expression.attr, class_name)

            if isinstance(base, ModuleInfo):
                return self.module_attribute(base, attribute, depth + 1)

            if isinstance(base, ClassInfo):
                return self.class_attribute(base, attribute)

            if isinstance(base, RuntimeTarget):
                return Skipped(Reason.BUILTIN_OR_C)

        return Skipped(Reason.RECEIVER_IS_VALUE)

    # Origins.

    def origin(
        self,
        module: ModuleInfo,
        expression: ast.expr,
        scope: Scope | None = None,
        owner: ClassInfo | None = None,
    ) -> Origin | None:
        """Finds the module and name an expression was imported as, such as `("typing", "final")`."""
        parts = dotted_parts(expression)

        if parts is None:
            return None

        if owner is not None and (
            parts[0] in owner.namespace.bindings or mangle(parts[0], owner.name) in owner.namespace.bindings
        ):
            return None

        if scope is not None and scope.find_binding_scope(parts[0]) is not None:
            return None

        return self._origin_of_parts(module, parts, 0)

    def _origin_of_parts(self, module: ModuleInfo, parts: list[str], depth: int) -> Origin | None:
        if depth > _DEPTH:
            return None

        root = parts[0]
        namespace = module.namespace

        if root in namespace.declared_global:
            return None

        bindings = namespace.bindings.get(root, [])

        if not bindings:
            return self._origin_of_unbound(module, parts, depth)

        if len(bindings) != 1 or bindings[0].conditional:
            return None

        binding = bindings[0]

        if binding.kind == "import" and binding.module is not None and len(parts) > 1:
            return self._known_or_follow(".".join([binding.module, *parts[1:-1]]), parts[-1], depth)

        if binding.kind == "import_from" and binding.attribute is not None:
            base = self.absolute(module, binding.module, binding.level)

            if base is None:
                return None

            if len(parts) == 1:
                return self._known_or_follow(base, binding.attribute, depth)

            return self._known_or_follow(".".join([base, binding.attribute, *parts[1:-1]]), parts[-1], depth)

        if binding.kind == "value" and binding.value is not None:
            value_parts = dotted_parts(binding.value)
            return None if value_parts is None else self._origin_of_parts(module, [*value_parts, *parts[1:]], depth + 1)

        if binding.kind in ("def", "class") and len(parts) == 1:
            return (module.name, root)

        return None

    def _origin_of_unbound(self, module: ModuleInfo, parts: list[str], depth: int) -> Origin | None:
        root = parts[0]

        for star in module.namespace.stars:
            verdict = self.star_exports(module, star, root, depth)

            if verdict is False:
                continue

            if verdict is not True or star.conditional:
                return None

            target = self.import_module(self.absolute(module, star.module, star.level))
            return None if isinstance(target, Skipped) else self._origin_of_parts(target, parts, depth + 1)

        return ("builtins", root) if len(parts) == 1 and hasattr(builtins, root) else None

    def _known_or_follow(self, module_name: str, attribute: str, depth: int) -> Origin | None:
        origin = (module_name, attribute)

        if origin in KNOWN_ORIGINS:
            return None if self.index.is_project_shadow(module_name) else origin

        target = self.import_module(module_name)

        if isinstance(target, Skipped):
            return origin

        bindings = target.namespace.bindings.get(attribute, [])
        reexported = len(bindings) == 1 and bindings[0].kind in ("import", "import_from", "value")

        if reexported or (not bindings and target.namespace.stars):
            deeper = self._origin_of_parts(target, [attribute], depth + 1)

            if deeper is not None:
                return deeper

        return origin

    def is_external(self, origin: Origin | None) -> bool:
        """Tells whether a name comes from outside the project, so no class it names can inherit a project class."""
        if origin is None or origin[0] == "builtins":
            return origin is not None

        located = self.index.locate(origin[0])
        return located.status == "missing" or located.source not in (None, "project")

    # Decorators and overloads.

    def decorator_origin(self, module: ModuleInfo, decorator: ast.expr, owner: ClassInfo | None) -> Origin | None:
        """Finds the origin of a decorator; a decorator call counts only for `deprecated("...")`."""
        scope = owner.scope if owner is not None else None
        namespace_owner = owner

        if isinstance(decorator, ast.Call):
            origin = self.origin(module, decorator.func, scope, namespace_owner)
            return origin if origin in DEPRECATED else None

        return self.origin(module, decorator, scope, namespace_owner)

    def split_overloads(
        self,
        module: ModuleInfo,
        bindings: list[NameBinding],
        owner: ClassInfo | None,
    ) -> tuple[tuple[FunctionNode, ...], list[NameBinding]]:
        """Separates the `@overload` variants of a name from the bindings that give it a value."""
        variants: list[FunctionNode] = []
        rest: list[NameBinding] = []

        for binding in bindings:
            node = binding.node

            if isinstance(node, FunctionNode) and binding.kind == "def" and self._is_overload(module, node, owner):
                variants.append(node)
            else:
                rest.append(binding)

        return tuple(variants), rest

    def _is_overload(self, module: ModuleInfo, node: FunctionNode, owner: ClassInfo | None) -> bool:
        return any(self.decorator_origin(module, decorator, owner) in OVERLOADS for decorator in node.decorator_list)

    def method_kind(self, module: ModuleInfo, node: FunctionNode, owner: ClassInfo | None) -> MethodKind | Skipped:
        """Reads whether a method takes an instance, a class or no receiver, refusing an unknown decorator."""
        kind: MethodKind = "instance"

        for decorator in node.decorator_list:
            origin = self.decorator_origin(module, decorator, owner)

            if origin == STATICMETHOD:
                kind = "static"
            elif origin == CLASSMETHOD:
                kind = "class"
            elif origin not in TRANSPARENT_FUNCTION_DECORATORS and origin not in DEPRECATED:
                return Skipped(Reason.DECORATOR)

        return kind

    def caller_kind(self, node: FunctionNode, owner: ClassInfo) -> MethodKind | Skipped:
        """Reads what the first parameter of the method that makes a call holds, allowing a property and its setter."""
        kind: MethodKind = "instance"

        for decorator in node.decorator_list:
            origin = self.decorator_origin(owner.module, decorator, owner)

            if origin == STATICMETHOD:
                kind = "static"
            elif origin == CLASSMETHOD:
                kind = "class"
            elif origin in PROPERTIES or self._is_property_accessor(decorator, owner):
                continue
            elif origin not in TRANSPARENT_FUNCTION_DECORATORS and origin not in DEPRECATED and origin not in OVERLOADS:
                return Skipped(Reason.DECORATOR)

        return kind

    @staticmethod
    def _is_property_accessor(decorator: ast.expr, owner: ClassInfo) -> bool:
        return (
            isinstance(decorator, ast.Attribute)
            and decorator.attr in ("setter", "getter", "deleter")
            and isinstance(decorator.value, ast.Name)
            and decorator.value.id in owner.namespace.bindings
        )

    # Classes.

    def class_info(self, module: ModuleInfo, node: ast.ClassDef) -> ClassInfo:
        """Gets the class a `class` statement of a module defines."""
        key = (str(module.path) if module.path is not None else module.name, node.lineno, node.col_offset)
        info: ClassInfo | None = self._classes.get(key)

        if info is None:
            site = module.scan.by_node[id(node)]

            # A class at module level runs in the scope of the module, which needs no table of the whole tree.
            scope = module.module_scope if site.at_module_level else module.scopes.class_scopes.get(id(node))
            info = ClassInfo(key, module, node, node.name, site.qualname, scope)

            self._classes[key] = info

        return info

    def class_at(self, module: ModuleInfo, tree: ast.Module, node: ast.ClassDef) -> ClassInfo | None:
        """
        Finds the module-level class of a module that stands where a class of another parse of its file stands.

        The class is found by its place among the statements, because the parse may be of a text whose lines moved.
        A tree the module holds itself needs no search.
        """
        if tree is module.tree:
            return self.class_info(module, node)

        index = next((index for index, statement in enumerate(tree.body) if statement is node), None)

        if index is None or index >= len(module.tree.body):
            return None

        statement = module.tree.body[index]

        if not isinstance(statement, ast.ClassDef) or statement.name != node.name:
            return None

        return self.class_info(module, statement)

    def namedtuple_info(self, module: ModuleInfo, call: ast.Call, origin: Origin) -> ClassInfo | Skipped:
        """Gets the class a `namedtuple(...)` or `NamedTuple(...)` call builds, when its arguments are literals."""
        key = (str(module.path) if module.path is not None else module.name, call.lineno, call.col_offset)
        info: ClassInfo | None = self._classes.get(key)

        if info is None:
            read = read_namedtuple_call_fields(call, origin)

            if read is None:
                return Skipped(Reason.CLASS_SHAPE)

            name, fields = read
            info = ClassInfo(key, module, None, name, name, None, fields, ClassFacts(is_namedtuple=True))

            self._classes[key] = info

        return info

    def facts(self, info: ClassInfo) -> ClassFacts:
        """Reads what the decorators, metaclass and bases of a class say about its shape, once per class."""
        found: ClassFacts | None = self._facts.get(info.key)

        if found is not None:
            return found

        facts = info.facts or ClassFacts()

        if info.node is not None:
            self._read_decorators(info, info.node, facts)
            self._read_keywords(info, info.node, facts)
            self._read_bases(info, info.node, facts)

        self._facts[info.key] = facts
        return facts

    def _read_decorators(self, info: ClassInfo, node: ast.ClassDef, facts: ClassFacts) -> None:
        for decorator in node.decorator_list:
            function = decorator.func if isinstance(decorator, ast.Call) else decorator
            origin = self.origin(info.module, function, info.scope)

            if origin == DATACLASS:
                params = read_dataclass_params(decorator)

                facts.dataclass = params
                facts.invalid = facts.invalid or params is None
            elif origin == TOTAL_ORDERING and not isinstance(decorator, ast.Call):
                facts.total_ordering = True
            elif origin not in TRANSPARENT_CLASS_DECORATORS or isinstance(decorator, ast.Call):
                facts.unknown_decorator = True

    def _read_keywords(self, info: ClassInfo, node: ast.ClassDef, facts: ClassFacts) -> None:
        for keyword in node.keywords:
            if keyword.arg is None:
                facts.invalid = True
            elif keyword.arg == "metaclass" and self.origin(info.module, keyword.value, info.scope) != ABC_META:
                facts.other_metaclass = True

    def _read_bases(self, info: ClassInfo, node: ast.ClassDef, facts: ClassFacts) -> None:
        for base in node.bases:
            expression = base.value if isinstance(base, ast.Subscript) else base
            origin = self.origin(info.module, expression, info.scope)

            if origin in PROTOCOLS:
                facts.is_protocol = True
            elif origin in TYPED_NAMEDTUPLES:
                facts.is_namedtuple = True
            elif origin in UNCALLABLE_BASES:
                facts.is_uncallable = True

        if facts.is_namedtuple:
            fields = read_namedtuple_class_fields(node)

            if isinstance(fields, Skipped):
                facts.invalid = True
            else:
                info.namedtuple_fields = fields

    def order(self, info: ClassInfo) -> tuple[Entry, ...] | Skipped:
        """Computes the method resolution order of a class as C3 does, with an opaque entry for each unreadable base."""
        cached: tuple[Entry, ...] | Skipped | None = self._orders.get(info.key)

        if cached is not None:
            return cached

        self._orders[info.key] = _IN_PROGRESS
        result = self._compute_order(info)
        self._orders[info.key] = result

        return result

    def _compute_order(self, info: ClassInfo) -> tuple[Entry, ...] | Skipped:
        facts = self.facts(info)

        if info.node is None:
            return (info, tuple, object)

        if facts.is_namedtuple:
            generic = any(
                self.origin(info.module, base.value if isinstance(base, ast.Subscript) else base, info.scope)
                in (("typing", "Generic"), ("typing_extensions", "Generic"))
                for base in info.node.bases
            )

            return (info, tuple, *([RUNTIME_BASES[("typing", "Generic")]] if generic else []), object)

        lists = [list(self._base_order(info, base)) for base in info.node.bases] or [[object]]
        merged = merge_orders([*lists, [sequence[0] for sequence in lists]])

        return Skipped(Reason.CLASS_SHAPE) if merged is None else (info, *merged)

    def _base_order(self, info: ClassInfo, base: ast.expr) -> tuple[Entry, ...]:
        expression = base.value if isinstance(base, ast.Subscript) else base
        label = f"{info.key}:{ast.unparse(expression)}"

        if isinstance(expression, ast.Call):
            origin = self.origin(info.module, expression.func, info.scope)

            if origin in NAMEDTUPLE_FACTORIES:
                assert origin is not None

                built = self.namedtuple_info(info.module, expression, origin)

                if isinstance(built, ClassInfo):
                    order = self.order(built)
                    return order if not isinstance(order, Skipped) else (Opaque(label, Reason.CLASS_SHAPE, False),)

            return (Opaque(label, Reason.CLASS_SHAPE, self.is_external(origin)),)

        origin = self.origin(info.module, expression, info.scope)

        if origin in RUNTIME_BASES:
            assert origin is not None
            return tuple(RUNTIME_BASES[origin].__mro__)

        if origin is not None and origin[0] == "builtins":
            runtime = runtime_builtin(origin[1])

            if runtime is not None:
                return tuple(runtime.__mro__)

        if origin is not None and origin in KNOWN_ORIGINS:
            return (Opaque(f"{origin[0]}.{origin[1]}", Reason.CLASS_SHAPE, True),)

        target = self.expression(info.module, info.scope, expression)

        if isinstance(target, ClassInfo):
            order = self.order(target)

            if isinstance(order, Skipped):
                return (Opaque(label, order.reason, target.module.source != "project"),)

            return order

        reason = target.reason if isinstance(target, Skipped) else Reason.CLASS_SHAPE
        return (Opaque(label, reason, self.is_external(origin)),)

    def check_order(self, order: tuple[Entry, ...]) -> Skipped | None:
        """Refuses an order whose classes a metaclass, an unknown decorator or an attribute hook may change."""
        for index, entry in enumerate(order):
            if not isinstance(entry, ClassInfo):
                continue

            facts = self.facts(entry)

            if facts.invalid or facts.other_metaclass:
                return Skipped(Reason.CLASS_SHAPE)

            if facts.unknown_decorator:
                return Skipped(Reason.DECORATOR)

            bindings = entry.namespace.bindings

            if "__getattribute__" in bindings or (index > 0 and "__init_subclass__" in bindings):
                return Skipped(Reason.CLASS_SHAPE)

        return None

    def class_member(self, info: ClassInfo, name: str) -> Member | Skipped | None:
        """Finds a member a class defines itself or has a decorator generate, or None when it defines none."""
        facts = self.facts(info)
        bindings = info.namespace.bindings.get(name)

        if bindings:
            variants, rest = self.split_overloads(info.module, bindings, info)

            if any(binding.conditional for binding in rest):
                return Skipped(Reason.CONDITIONAL_BINDING)

            if len(rest) != 1:
                return Skipped(Reason.NAME_REBOUND)

            binding = rest[0]

            if isinstance(binding.node, FunctionNode | ast.ClassDef) and binding.kind in ("def", "class"):
                return Member(info, binding.node, variants if binding.kind == "def" else ())

            return Skipped(Reason.ASSIGNED_VALUE)

        if facts.is_namedtuple:
            generated = name in NAMEDTUPLE_MEMBERS or name in {field.name for field in info.namedtuple_fields or ()}

            return (
                Skipped(Reason.BUILTIN_OR_C) if generated or (name.startswith("__") and name.endswith("__")) else None
            )

        if facts.dataclass is not None:
            if name == "__init__":
                return Member(info, None) if facts.dataclass.init else None

            if name.startswith("__") and name.endswith("__"):
                return Skipped(Reason.BUILTIN_OR_C)

        if facts.total_ordering and name in ORDER_METHODS:
            return Skipped(Reason.BUILTIN_OR_C)

        return None

    def find_member(self, order: tuple[Entry, ...], name: str, start: int = 0) -> Member | Skipped:
        """Looks a member up along an order from a position; an opaque class ahead of the definition hides it."""
        problem = self.check_order(order)

        if problem is not None:
            return problem

        has_opaque = any(isinstance(entry, Opaque) for entry in order)

        for entry in order[start:]:
            if isinstance(entry, Opaque):
                return Skipped(entry.reason)

            if isinstance(entry, type):
                if name in vars(entry):
                    return Skipped(Reason.BUILTIN_OR_C)

                continue

            if has_opaque and entry.module.source != "project":
                return Skipped(Reason.CLASS_SHAPE)

            found = self.class_member(entry, name)

            if found is not None:
                return found

        return Skipped(Reason.UNBOUND_NAME)

    def class_attribute(self, info: ClassInfo, name: str) -> Target | Skipped:
        """Resolves an attribute read from a class object, such as `Base.helper` or `Outer.Inner`."""
        order = self.order(info)

        if isinstance(order, Skipped):
            return order

        member = self.find_member(order, name)

        if isinstance(member, Skipped):
            return member

        stored = self.check_stored(order, name, everywhere=False)

        if stored is not None:
            return stored

        if isinstance(member.node, ast.ClassDef):
            return self.class_info(member.owner.module, member.node)

        if member.node is None:
            return Skipped(Reason.BUILTIN_OR_C)

        return FunctionTarget(member.owner.module, member.node, member.owner, member.overloads, "class")

    def constructor(self, info: ClassInfo) -> tuple[Parameter, ...] | Skipped:
        """Reads the parameters a call of a class binds to, after the receiver, or why its shape is not modeled."""
        facts = self.facts(info)

        if facts.is_protocol or facts.is_uncallable:
            return Skipped(Reason.CLASS_SHAPE)

        order = self.order(info)

        if isinstance(order, Skipped):
            return order

        problem = self._check_constructor_order(order)

        if problem is not None:
            return problem

        for index, entry in enumerate(order):
            if isinstance(entry, type):
                return Skipped(Reason.BUILTIN_OR_C)

            if not isinstance(entry, ClassInfo):
                return Skipped(Reason.CLASS_SHAPE)

            if entry.namedtuple_fields is not None:
                return entry.namedtuple_fields

            if index > 0 and self.facts(entry).is_protocol:
                return Skipped(Reason.CLASS_SHAPE)

            member = self.class_member(entry, "__init__")

            if member is None:
                continue

            if isinstance(member, Skipped):
                return member

            return self._init_parameters(order, index, member)

        return Skipped(Reason.BUILTIN_OR_C)

    def _check_constructor_order(self, order: tuple[Entry, ...]) -> Skipped | None:
        problem = self.check_order(order)

        if problem is not None:
            return problem

        is_namedtuple = any(isinstance(entry, ClassInfo) and entry.namedtuple_fields is not None for entry in order)

        for entry in order:
            if isinstance(entry, Opaque):
                return Skipped(entry.reason)

            if isinstance(entry, type):
                if not is_safe_runtime_base(entry) and not (is_namedtuple and entry is tuple):
                    return Skipped(Reason.BUILTIN_OR_C)
            elif entry.namedtuple_fields is None and "__new__" in entry.namespace.bindings:
                return Skipped(Reason.CLASS_SHAPE)

        return None

    def _init_parameters(self, order: tuple[Entry, ...], index: int, member: Member) -> tuple[Parameter, ...] | Skipped:
        if any(isinstance(entry, ClassInfo) and entry.namedtuple_fields is not None for entry in order[index + 1 :]):
            return Skipped(Reason.CLASS_SHAPE)

        if member.node is None:
            return self.dataclass_init(member.owner)

        if not isinstance(member.node, FunctionNode):
            return Skipped(Reason.CLASS_SHAPE)

        is_exception = any(isinstance(entry, type) and issubclass(entry, BaseException) for entry in order)

        if is_exception and not calls_base_init(member.node):
            return Skipped(Reason.CLASS_SHAPE)

        target = FunctionTarget(member.owner.module, member.node, member.owner, member.overloads, "instance")
        return self.function_parameters(target)

    def dataclass_init(self, info: ClassInfo) -> tuple[Parameter, ...] | Skipped:
        """Writes the parameters of the `__init__` a dataclass generates from its fields and those of its bases."""
        order = self.order(info)

        if isinstance(order, Skipped):
            return order

        fields = []

        for entry in reversed(order):
            if not isinstance(entry, ClassInfo) or entry.node is None:
                continue

            params = self.facts(entry).dataclass

            if params is None:
                continue

            origin_of = partial(self._annotation_origin, entry)
            own = read_dataclass_fields(entry.node, params, origin_of, partial(self._inherits_attribute, entry))

            if isinstance(own, Skipped):
                return own

            fields.extend(own)

        return dataclass_parameters(fields)

    def _annotation_origin(self, owner: ClassInfo, expression: ast.expr) -> Origin | None:
        return self.origin(owner.module, expression, owner.scope, owner)

    def _inherits_attribute(self, info: ClassInfo, name: str) -> bool | None:
        order = self.order(info)

        if isinstance(order, Skipped):
            return None

        for entry in order[1:]:
            if isinstance(entry, Opaque):
                return None

            if isinstance(entry, type):
                if name in vars(entry):
                    return True

                continue

            if entry.node is None:
                if name in {field.name for field in entry.namedtuple_fields or ()}:
                    return True

                continue

            is_dataclass = self.facts(entry).dataclass is not None

            if name in class_attribute_names(entry.node, is_dataclass, partial(self._annotation_origin, entry)):
                return True

        return False

    # Dispatch.

    def project_classes(self) -> tuple[ClassInfo, ...]:
        """Lists every class of every project module, nested ones included, once per run."""
        cached: tuple[ClassInfo, ...] | None = self._project.get("classes")

        if cached is None:
            cached = tuple(info for module in self.index.project_modules() for info in self._classes_of(module))
            self._project["classes"] = cached

        return cached if self._in_project() else (*cached, *self._classes_of(self.module))

    def _in_project(self) -> bool:
        """Tells whether the analyzed file is one of the project modules, so the project-wide caches already hold it."""
        paths: frozenset[object] | None = self._project.get("paths")

        if paths is None:
            paths = frozenset(module.path for module in self.index.project_modules())
            self._project["paths"] = paths

        return self.module.path in paths

    def _project_key(self, name: str) -> tuple[str, str]:
        return (name, "" if self._in_project() else str(self.module.path))

    def _classes_of(self, module: ModuleInfo) -> tuple[ClassInfo, ...]:
        return tuple(self.class_info(module, site.node) for site in module.class_sites)

    def check_dispatch(self, owner: ClassInfo, name: str, member: Member, *, after: bool) -> Skipped | None:
        """Refuses a dispatched call when any project class that inherits the owner reaches another definition."""
        key = (owner.key, name, after, member.owner.key)

        if key in self._dispatch:
            cached: Skipped | None = self._dispatch[key]
            return cached

        heirs, unknown = self._hierarchy()
        result = None

        for order in heirs.get(owner.key, ()):
            position = next(
                index for index, entry in enumerate(order) if isinstance(entry, ClassInfo) and entry.key == owner.key
            )

            found = self.find_member(order, name, position + 1 if after else 0)

            if not isinstance(found, Member) or not found.is_same(member):
                result = Skipped(Reason.DISPATCH_UNSAFE)
                break

        if result is None and any(self._defines_other(info, name, member) for info in unknown):
            result = Skipped(Reason.DISPATCH_UNSAFE)

        self._dispatch[key] = result
        return result

    def _hierarchy(self) -> tuple[dict[tuple[str, int, int], list[tuple[Entry, ...]]], tuple[ClassInfo, ...]]:
        """
        Indexes the orders of the project classes by each class they inherit, once per run,
        and lists the classes an order with an opaque project base or no order at all may hide an heir behind.
        """
        key = self._project_key("hierarchy")

        cached: tuple[dict[tuple[str, int, int], list[tuple[Entry, ...]]], tuple[ClassInfo, ...]] | None = (
            self._dispatch.get(key)
        )

        if cached is not None:
            return cached

        heirs: dict[tuple[str, int, int], list[tuple[Entry, ...]]] = {}
        unknown: list[ClassInfo] = []

        for subclass in self.project_classes():
            order = self.order(subclass)

            if isinstance(order, Skipped):
                unknown.append(subclass)
                continue

            if any(isinstance(entry, Opaque) and not entry.external for entry in order):
                unknown.extend(entry for entry in order if isinstance(entry, ClassInfo))

            for entry in order:
                if isinstance(entry, ClassInfo):
                    heirs.setdefault(entry.key, []).append(order)

        result = (heirs, tuple(dict.fromkeys(unknown)))
        self._dispatch[key] = result

        return result

    def _defines_other(self, info: ClassInfo, name: str, member: Member) -> bool:
        found = self.class_member(info, name)
        return found is not None and (isinstance(found, Skipped) or not found.is_same(member))

    def check_stored(self, order: tuple[Entry, ...], name: str, *, everywhere: bool) -> Skipped | None:
        """Refuses a member that code assigns as an attribute, such as `self.name = ...`, which would shadow it."""
        modules = [self.module, *(entry.module for entry in order if isinstance(entry, ClassInfo))]

        if any(name in module.stored_attributes for module in modules):
            return Skipped(Reason.NAME_REBOUND)

        if everywhere and name in self._project_stored():
            return Skipped(Reason.NAME_REBOUND)

        return None

    def _project_stored(self) -> frozenset[str]:
        key = self._project_key("stored")
        cached: frozenset[str] | None = self._project.get(key)

        if cached is None:
            modules = [*self.index.project_modules(), self.module]
            cached = frozenset(name for module in modules for name in module.stored_attributes)

            self._project[key] = cached

        return cached

    # Calls.

    def allows_method(self, name: str) -> bool:
        """Tells whether the `methods` option lets a dispatched call bind to a method of this name."""
        if self.options.methods == "all":
            return True

        if self.options.methods == "none":
            return False

        return name.startswith("_") and not (name.startswith("__") and name.endswith("__"))

    def resolve(self, call: ast.Call, scope: Scope) -> Callee | Skipped:
        """
        Resolves the callee of a call made in a scope of the module.

        A special form a type checker reads only from positional arguments, such as `typing.cast`, is refused.
        A callee whose name, or the name the call writes, any project module replaces on an imported module or class,
        as a test double does, is refused, because the replacement may take other parameter names.
        Every call is refused when the project passes the caps of the scan, because that check reads every module.
        """
        if not self.index.is_project_complete:
            return Skipped(Reason.SCAN_TOO_LARGE)

        callee = self._resolve_callee(call, scope)

        if isinstance(callee, Skipped):
            return callee

        if callee.name in _SPECIAL_FORMS:
            return Skipped(Reason.SPECIAL_FORM)

        function = call.func
        written = function.attr if isinstance(function, ast.Attribute) else getattr(function, "id", None)
        patched = self._project_patched()

        if callee.name.rpartition(".")[2] in patched or written in patched:
            return Skipped(Reason.NAME_REBOUND)

        return callee

    def _project_patched(self) -> frozenset[str]:
        key = self._project_key("patched")
        cached: frozenset[str] | None = self._project.get(key)

        if cached is None:
            modules = [*self.index.project_modules(), self.module]
            cached = frozenset(name for module in modules for name in module.patched_attributes)

            self._project[key] = cached

        return cached

    def _resolve_callee(self, call: ast.Call, scope: Scope) -> Callee | Skipped:
        function = call.func

        if isinstance(function, ast.Attribute):
            value = function.value

            if isinstance(value, ast.Call) and isinstance(value.func, ast.Name) and value.func.id == "super":
                return self._super_call(scope, function, value)

            if isinstance(value, ast.Name):
                receiver = self._receiver_of(scope, value)

                if receiver is not None:
                    return (
                        receiver
                        if isinstance(receiver, Skipped)
                        else self._receiver_call(scope, *receiver, function.attr)
                    )

        target = self.expression(self.module, scope, function)
        return target if isinstance(target, Skipped) else self.callee_of(target)

    def _method_scope(self, scope: Scope, name: str) -> tuple[Scope, ClassInfo] | Skipped | None:
        binder = scope.find_binding_scope(name)

        if binder is None or binder.kind != "function" or not isinstance(binder.node, FunctionNode):
            return None

        positional = [*binder.node.args.posonlyargs, *binder.node.args.args]

        if not positional or positional[0].arg != name:
            return None

        holder = binder.parent

        if holder is None or holder.kind != "class" or not isinstance(holder.node, ast.ClassDef):
            return None

        if name in binder.assigned or name in binder.declared_nonlocal:
            return Skipped(Reason.RECEIVER_IS_VALUE)

        tree = self.scopes.module.node

        if holder.parent is not self.scopes.module or not isinstance(tree, ast.Module) or holder.node not in tree.body:
            return Skipped(Reason.RECEIVER_IS_VALUE)

        info = self.class_at(self.module, tree, holder.node)
        return Skipped(Reason.RECEIVER_IS_VALUE) if info is None else (binder, info)

    def _receiver_of(self, scope: Scope, root: ast.Name) -> tuple[ClassInfo, MethodKind] | Skipped | None:
        found = self._method_scope(scope, root.id)

        if found is None or isinstance(found, Skipped):
            return found

        binder, info = found
        assert isinstance(binder.node, FunctionNode)
        kind = self.caller_kind(binder.node, info)

        if isinstance(kind, Skipped):
            return kind

        return None if kind == "static" else (info, kind)

    def _receiver_call(self, scope: Scope, info: ClassInfo, kind: MethodKind, attribute: str) -> Callee | Skipped:
        if not self.allows_method(attribute):
            return Skipped(Reason.METHODS_DISABLED)

        return self._dispatched(info, mangle(attribute, scope.class_name), kind, after=False)

    def _super_call(self, scope: Scope, function: ast.Attribute, inner: ast.Call) -> Callee | Skipped:
        if scope.find_binding_scope("super") is not None or not isinstance(
            self.global_name(self.module, "super"), RuntimeTarget
        ):
            return Skipped(Reason.RECEIVER_IS_VALUE)

        if scope.kind != "function" or not isinstance(scope.node, FunctionNode) or inner.keywords:
            return Skipped(Reason.RECEIVER_IS_VALUE)

        positional = [*scope.node.args.posonlyargs, *scope.node.args.args]

        if not positional:
            return Skipped(Reason.RECEIVER_IS_VALUE)

        found = self._method_scope(scope, positional[0].arg)

        if found is None or isinstance(found, Skipped) or found[0] is not scope:
            return found if isinstance(found, Skipped) else Skipped(Reason.RECEIVER_IS_VALUE)

        info = found[1]

        if inner.args and not self._names_own_class(inner.args, info, positional[0].arg):
            return Skipped(Reason.RECEIVER_IS_VALUE)

        kind = self.caller_kind(scope.node, info)

        if isinstance(kind, Skipped):
            return kind

        if kind == "static":
            return Skipped(Reason.RECEIVER_IS_VALUE)

        if not self.allows_method(function.attr):
            return Skipped(Reason.METHODS_DISABLED)

        return self._dispatched(info, mangle(function.attr, scope.class_name), kind, after=True)

    def _names_own_class(self, arguments: list[ast.expr], info: ClassInfo, receiver: str) -> bool:
        if len(arguments) != 2 or not isinstance(arguments[0], ast.Name) or not isinstance(arguments[1], ast.Name):
            return False

        named = self.global_name(self.module, arguments[0].id)
        return isinstance(named, ClassInfo) and named.key == info.key and arguments[1].id == receiver

    def _dispatched(self, info: ClassInfo, name: str, kind: MethodKind, *, after: bool) -> Callee | Skipped:
        order = self.order(info)

        if isinstance(order, Skipped):
            return order

        member = self.find_member(order, name, 1 if after else 0)

        if isinstance(member, Skipped):
            return member

        for problem in (
            self.check_stored(order, name, everywhere=True),
            self.check_dispatch(info, name, member, after=after),
        ):
            if problem is not None:
                return problem

        return self._member_callee(member, "instance" if kind == "instance" else "class")

    def _member_callee(self, member: Member, access: Access) -> Callee | Skipped:
        node = member.node

        if isinstance(node, ast.ClassDef):
            return self.callee_of(self.class_info(member.owner.module, node))

        if node is None:
            parameters = self.dataclass_init(member.owner)

            if isinstance(parameters, Skipped):
                return parameters

            return self._gate(
                Callee(
                    self._display(member.owner.module, f"{member.owner.qualname}.__init__"),
                    member.owner.module.source,
                    parameters,
                )
            )

        return self.callee_of(FunctionTarget(member.owner.module, node, member.owner, member.overloads, access))

    def callee_of(self, target: Target) -> Callee | Skipped:
        """Reads the parameters of what a callee names, refusing a module, a builtin and a disabled source."""
        if isinstance(target, FunctionTarget):
            parameters = self.function_parameters(target)
            qualname = target.node.name if target.owner is None else f"{target.owner.qualname}.{target.node.name}"
            module = target.module
        elif isinstance(target, ClassInfo):
            parameters = self.constructor(target)
            qualname = target.qualname
            module = target.module
        elif isinstance(target, RuntimeTarget):
            return Skipped(Reason.BUILTIN_OR_C)
        else:
            return Skipped(Reason.RECEIVER_IS_VALUE)

        if isinstance(parameters, Skipped):
            return parameters

        receiver = isinstance(target, FunctionTarget) and self._passes_receiver(target)

        return self._gate(Callee(self._display(module, qualname), module.source, parameters, receiver))

    def _passes_receiver(self, target: FunctionTarget) -> bool:
        return (
            target.owner is not None
            and target.access == "class"
            and self.method_kind(target.module, target.node, target.owner) == "instance"
        )

    def _gate(self, callee: Callee) -> Callee | Skipped:
        return callee if callee.source in self.options.sources else Skipped(Reason.SOURCE_DISABLED)

    @staticmethod
    def _display(module: ModuleInfo, qualname: str) -> str:
        return f"{module.name}.{qualname}" if module.name else qualname

    def function_parameters(self, target: FunctionTarget) -> tuple[Parameter, ...] | Skipped:
        """Reads the parameters a call of a function binds to, after any receiver its access fills."""
        kind = self.method_kind(target.module, target.node, target.owner)

        if isinstance(kind, Skipped):
            return kind

        parameters = parameters_of(target.node.args)
        name = target.node.name

        if target.owner is not None and name.startswith("__") and name.endswith("__") and name not in _NAMED_DUNDERS:
            parameters = to_positional_only(parameters)

        names = positional_names(parameters)

        for variant in target.overloads:
            variant_parameters = parameters_of(variant.args)
            variant_names = positional_names(variant_parameters)

            if any(parameter.kind == ParameterKind.POSITIONAL_ONLY for parameter in variant_parameters):
                return Skipped(Reason.DECORATOR)

            if variant_names != names[: len(variant_names)]:
                return Skipped(Reason.DECORATOR)

        if target.owner is None and kind == "class":
            return Skipped(Reason.DECORATOR)

        if kind == "class" or (kind == "instance" and target.access == "instance"):
            return drop_receiver(parameters)

        return parameters
