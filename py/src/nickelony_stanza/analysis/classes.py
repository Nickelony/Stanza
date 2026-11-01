"""
The classes the resolver reads: their method resolution order, the members it finds, and the shapes of a constructor.

An entry of a method resolution order is a class read from source, a builtin class, or an opaque base.
An opaque base is one the resolver cannot read,
such as a class of a compiled module or of a source the options leave out.
It is *external* when it lives outside the project, because the resolver assumes no code outside the roots
subclasses a project class; an opaque base of the project itself could hide any class.

The constructor shapes it models:

- A plain `__init__` in the class body or inherited through the method resolution order.
- A dataclass, whose `__init__` takes its fields in declaration order across its dataclass bases,
  with `init`, `kw_only`, the `KW_ONLY` sentinel, `field(init=..., kw_only=...)`, `InitVar` and `ClassVar` applied.
- A named tuple, declared with `typing.NamedTuple` class syntax
  or built by `collections.namedtuple` or `typing.NamedTuple`.
"""

from __future__ import annotations

import abc
import ast
import builtins
import typing
from collections.abc import Callable
from dataclasses import dataclass, field

from nickelony_stanza.analysis.module_index import ModuleInfo
from nickelony_stanza.analysis.scopes import Namespace, Scope, collect_namespace, mangle
from nickelony_stanza.analysis.shapes import Parameter, ParameterKind, Reason, Skipped

Origin = tuple[str, str]

# The known names the resolver recognizes by the import that binds them, never by the bare name.
STATICMETHOD: Origin = ("builtins", "staticmethod")
CLASSMETHOD: Origin = ("builtins", "classmethod")

OVERLOADS = frozenset({("typing", "overload"), ("typing_extensions", "overload")})

TRANSPARENT_FUNCTION_DECORATORS = frozenset(
    {
        ("abc", "abstractmethod"),
        ("typing", "override"),
        ("typing_extensions", "override"),
        ("typing", "final"),
        ("typing_extensions", "final"),
        ("typing", "no_type_check"),
        ("typing_extensions", "no_type_check"),
        ("contextlib", "contextmanager"),
        ("contextlib", "asynccontextmanager"),
    }
)

DEPRECATED = frozenset({("warnings", "deprecated"), ("typing_extensions", "deprecated")})
PROPERTIES = frozenset({("builtins", "property"), ("functools", "cached_property")})
TRANSPARENT_CLASS_DECORATORS = frozenset({("typing", "final"), ("typing_extensions", "final")})

TOTAL_ORDERING: Origin = ("functools", "total_ordering")
DATACLASS: Origin = ("dataclasses", "dataclass")
FIELD: Origin = ("dataclasses", "field")
KW_ONLY: Origin = ("dataclasses", "KW_ONLY")

CLASS_VARS = frozenset({("typing", "ClassVar"), ("typing_extensions", "ClassVar")})
ABC_META: Origin = ("abc", "ABCMeta")

TYPED_NAMEDTUPLES = frozenset({("typing", "NamedTuple"), ("typing_extensions", "NamedTuple")})
NAMEDTUPLE_FACTORIES = TYPED_NAMEDTUPLES | {("collections", "namedtuple")}
UNCALLABLE_BASES = frozenset({("typing", "TypedDict"), ("typing_extensions", "TypedDict")})
PROTOCOLS = frozenset({("typing", "Protocol"), ("typing_extensions", "Protocol")})

RUNTIME_BASES: dict[Origin, type] = {
    ("typing", "Generic"): typing.Generic,
    ("typing_extensions", "Generic"): typing.Generic,
    ("typing", "Protocol"): typing.Protocol,
    ("typing_extensions", "Protocol"): typing.Protocol,
    ("abc", "ABC"): abc.ABC,
}

# Every known name: the resolver stops following imports at one,
# and counts it only when no project module shadows its module.
KNOWN_ORIGINS = frozenset(
    {STATICMETHOD, CLASSMETHOD, TOTAL_ORDERING, DATACLASS, FIELD, KW_ONLY, ABC_META}
    | OVERLOADS
    | TRANSPARENT_FUNCTION_DECORATORS
    | DEPRECATED
    | PROPERTIES
    | TRANSPARENT_CLASS_DECORATORS
    | CLASS_VARS
    | NAMEDTUPLE_FACTORIES
    | UNCALLABLE_BASES
    | PROTOCOLS
    | set(RUNTIME_BASES)
)

# Builtin bases whose `__new__` ignores the arguments a subclass's `__init__` takes.
_SAFE_RUNTIME_BASES = frozenset({object, dict, list, set, typing.Generic, typing.Protocol, abc.ABC})
ORDER_METHODS = frozenset({"__lt__", "__le__", "__gt__", "__ge__"})

NAMEDTUPLE_MEMBERS = frozenset(
    {"_make", "_replace", "_asdict", "_fields", "_field_defaults", "__new__", "__getnewargs__"}
)


def is_safe_runtime_base(cls: type) -> bool:
    """Tells whether a builtin base lets a subclass's `__init__` alone decide how a call binds."""
    return cls in _SAFE_RUNTIME_BASES or issubclass(cls, BaseException)


@dataclass(frozen=True)
class Opaque:
    """A base the resolver cannot read, with the reason and whether it lives outside the project."""

    label: str
    reason: Reason
    external: bool


@dataclass(frozen=True)
class DataclassParams:
    """The `dataclass(...)` arguments that shape `__init__`."""

    init: bool
    kw_only: bool


@dataclass
class ClassFacts:
    """What the decorators, the metaclass and the bases of a class say about its shape."""

    dataclass: DataclassParams | None = None
    total_ordering: bool = False
    unknown_decorator: bool = False
    other_metaclass: bool = False
    is_protocol: bool = False
    is_namedtuple: bool = False
    is_uncallable: bool = False
    invalid: bool = False


@dataclass(eq=False)
class ClassInfo:
    """
    A class read from source, or the class a `namedtuple(...)` call builds.

    `key` identifies the class across two parses of one file, and `scope` is the scope its statement runs in.
    """

    key: tuple[str, int, int]
    module: ModuleInfo
    node: ast.ClassDef | None
    name: str
    qualname: str
    scope: Scope | None
    namedtuple_fields: tuple[Parameter, ...] | None = None
    facts: ClassFacts | None = None
    _namespace: Namespace | None = field(default=None, repr=False)

    @property
    def namespace(self) -> Namespace:
        """The bindings of the class body, keyed by the names the compiler stores after mangling."""
        if self._namespace is None:
            namespace = Namespace()

            if self.node is not None:
                for name, bindings in collect_namespace(self.node.body, is_module=False).bindings.items():
                    namespace.bindings[mangle(name, self.name)] = bindings

            self._namespace = namespace

        return self._namespace


Entry = ClassInfo | type | Opaque


@dataclass(frozen=True)
class Member:
    """A member a lookup finds: a method or nested class of a class, or the `__init__` a dataclass writes."""

    owner: ClassInfo
    node: ast.FunctionDef | ast.AsyncFunctionDef | ast.ClassDef | None
    overloads: tuple[ast.FunctionDef | ast.AsyncFunctionDef, ...] = ()

    def is_same(self, other: Member) -> bool:
        """Tells whether two lookups found the same definition, even through two parses of its file."""
        if self.owner.key != other.owner.key:
            return False

        if self.node is None or other.node is None:
            return self.node is None and other.node is None

        return (self.node.lineno, self.node.col_offset) == (other.node.lineno, other.node.col_offset)


def entry_key(entry: Entry) -> tuple[str, object]:
    """Keys an entry so the merge of two orders recognizes one class reached twice."""
    if isinstance(entry, ClassInfo):
        return ("class", entry.key)

    if isinstance(entry, Opaque):
        return ("opaque", entry.label)

    return ("runtime", id(entry))


def merge_orders(sequences: list[list[Entry]]) -> list[Entry] | None:
    """Merges the orders of the bases as C3 does, or None when no consistent order exists."""
    pending = [list(sequence) for sequence in sequences if sequence]
    result: list[Entry] = []

    while pending:
        tails = {entry_key(entry) for sequence in pending for entry in sequence[1:]}
        head = next((sequence[0] for sequence in pending if entry_key(sequence[0]) not in tails), None)

        if head is None:
            return None

        key = entry_key(head)
        result.append(head)

        pending = [sequence[1:] if entry_key(sequence[0]) == key else sequence for sequence in pending]
        pending = [sequence for sequence in pending if sequence]

    return result


def runtime_builtin(name: str) -> type | None:
    """Finds the builtin class of a name, such as `dict` or `ValueError`."""
    value = getattr(builtins, name, None)
    return value if isinstance(value, type) else None


@dataclass(frozen=True)
class _Field:
    name: str
    has_default: bool
    kw_only: bool
    init: bool


OriginOf = Callable[[ast.expr], Origin | None]


def _literal_bool(node: ast.expr) -> bool | None:
    return node.value if isinstance(node, ast.Constant) and isinstance(node.value, bool) else None


def read_dataclass_params(decorator: ast.expr) -> DataclassParams | None:
    """Reads the `init` and `kw_only` arguments of `@dataclass(...)`, or None when one is not a literal."""
    if not isinstance(decorator, ast.Call):
        return DataclassParams(init=True, kw_only=False)

    if decorator.args:
        return None

    values = {"init": True, "kw_only": False}

    for keyword in decorator.keywords:
        if keyword.arg is None:
            return None

        if keyword.arg in values:
            literal = _literal_bool(keyword.value)

            if literal is None:
                return None

            values[keyword.arg] = literal

    return DataclassParams(init=values["init"], kw_only=values["kw_only"])


def _annotation_head(annotation: ast.expr) -> ast.expr | None:
    if isinstance(annotation, ast.Constant) and isinstance(annotation.value, str):
        try:
            annotation = ast.parse(annotation.value.strip(), mode="eval").body
        except SyntaxError:
            return None

    return annotation.value if isinstance(annotation, ast.Subscript) else annotation


def _field_call(value: ast.expr, origin_of: OriginOf) -> ast.Call | None:
    if isinstance(value, ast.Call) and origin_of(value.func) == FIELD:
        return value

    return None


def class_attribute_names(node: ast.ClassDef, is_dataclass: bool, origin_of: OriginOf) -> frozenset[str]:
    """Lists the attributes a class body leaves on the class, which a field without a value takes as its default."""
    names: set[str] = set()

    for statement in node.body:
        if isinstance(statement, ast.AnnAssign) and isinstance(statement.target, ast.Name):
            value = statement.value
            call = None if value is None or not is_dataclass else _field_call(value, origin_of)

            if value is not None and (call is None or any(keyword.arg == "default" for keyword in call.keywords)):
                names.add(statement.target.id)
            elif call is not None:
                names.discard(statement.target.id)
        elif isinstance(statement, ast.Assign):
            names.update(name for target in statement.targets for name in _assigned_names(target))
        elif isinstance(statement, ast.FunctionDef | ast.AsyncFunctionDef | ast.ClassDef):
            names.add(statement.name)

    return frozenset(names)


def read_dataclass_fields(
    node: ast.ClassDef,
    params: DataclassParams,
    origin_of: OriginOf,
    inherits: Callable[[str], bool | None],
) -> list[_Field] | Skipped:
    """
    Reads the fields a dataclass declares in its own body, in order, or why their shape is unknown.

    `inherits` tells whether a base leaves a class attribute of a name, or None when an unreadable base may.
    """
    if any(
        isinstance(inner, ast.AnnAssign)
        for statement in node.body
        if not isinstance(statement, ast.AnnAssign)
        for inner in _block(statement)
    ):
        return Skipped(Reason.CLASS_SHAPE)

    # The fields follow `__annotations__`: a name keeps its first position and takes its last annotation,
    # and its default is the value the class attribute holds last.
    annotations: dict[str, ast.expr] = {}
    values: dict[str, ast.expr | None] = {}

    for statement in node.body:
        if isinstance(statement, ast.AnnAssign) and isinstance(statement.target, ast.Name):
            annotations[statement.target.id] = statement.annotation

            if statement.value is not None:
                values[statement.target.id] = statement.value
        elif isinstance(statement, ast.Assign):
            values.update((name, statement.value) for target in statement.targets for name in _assigned_names(target))
        elif isinstance(statement, ast.FunctionDef | ast.AsyncFunctionDef | ast.ClassDef):
            values[statement.name] = None

    fields: list[_Field] = []
    kw_only = params.kw_only

    for name, annotation in annotations.items():
        head = _annotation_head(annotation)
        head_origin = None if head is None else origin_of(head)

        if head_origin in CLASS_VARS:
            fields.append(_Field(name, has_default=True, kw_only=False, init=False))
            continue

        if head_origin == KW_ONLY:
            kw_only = True
            continue

        assigned = name in values

        if not assigned:
            inherited = inherits(name)

            if inherited is None:
                return Skipped(Reason.CLASS_SHAPE)

            if inherited:
                fields.append(_Field(name, has_default=True, kw_only=kw_only, init=True))
                continue

        found = _read_field(name, assigned, values.get(name), kw_only, origin_of)

        if isinstance(found, Skipped):
            return found

        fields.append(found)

    return fields


def _assigned_names(target: ast.expr) -> list[str]:
    if isinstance(target, ast.Name):
        return [target.id]

    if isinstance(target, ast.Tuple | ast.List):
        return [name for element in target.elts for name in _assigned_names(element)]

    return []


def _block(statement: ast.stmt) -> list[ast.stmt]:
    if isinstance(statement, ast.FunctionDef | ast.AsyncFunctionDef | ast.ClassDef):
        return []

    inner: list[ast.stmt] = []

    for name in ("body", "orelse", "finalbody"):
        part = getattr(statement, name, None)

        if isinstance(part, list):
            inner.extend(child for child in part if isinstance(child, ast.stmt))

    for handler in getattr(statement, "handlers", []):
        inner.extend(handler.body)

    for case in getattr(statement, "cases", []):
        inner.extend(case.body)

    return [deep for child in inner for deep in (child, *_block(child))]


def _read_field(
    name: str, assigned: bool, value: ast.expr | None, kw_only: bool, origin_of: OriginOf
) -> _Field | Skipped:
    if not assigned:
        return _Field(name, has_default=False, kw_only=kw_only, init=True)

    call = None if value is None else _field_call(value, origin_of)

    if call is None:
        return _Field(name, has_default=True, kw_only=kw_only, init=True)

    if call.args:
        return Skipped(Reason.CLASS_SHAPE)

    has_default = False
    init = True

    for keyword in call.keywords:
        if keyword.arg is None:
            return Skipped(Reason.CLASS_SHAPE)

        if keyword.arg in ("default", "default_factory"):
            has_default = True
        elif keyword.arg in ("init", "kw_only"):
            literal = _literal_bool(keyword.value)

            if literal is None:
                return Skipped(Reason.CLASS_SHAPE)

            if keyword.arg == "init":
                init = literal
            else:
                kw_only = literal

    return _Field(name, has_default=has_default, kw_only=kw_only, init=init)


def dataclass_parameters(fields: list[_Field]) -> tuple[Parameter, ...]:
    """Writes the parameters of the `__init__` a dataclass generates from its fields, the keyword-only ones last."""
    ordered: dict[str, _Field] = {}

    for found in fields:
        ordered[found.name] = found

    included = [found for found in ordered.values() if found.init]

    positional = [
        Parameter(found.name, ParameterKind.POSITIONAL_OR_KEYWORD, found.has_default)
        for found in included
        if not found.kw_only
    ]

    keyword = [
        Parameter(found.name, ParameterKind.KEYWORD_ONLY, found.has_default) for found in included if found.kw_only
    ]

    return (*positional, *keyword)


def read_namedtuple_class_fields(node: ast.ClassDef) -> tuple[Parameter, ...] | Skipped:
    """Reads the fields of a `typing.NamedTuple` class, which its body annotates in order."""
    if any(
        isinstance(inner, ast.AnnAssign)
        for statement in node.body
        if not isinstance(statement, ast.AnnAssign)
        for inner in _block(statement)
    ):
        return Skipped(Reason.CLASS_SHAPE)

    return tuple(
        Parameter(statement.target.id, ParameterKind.POSITIONAL_OR_KEYWORD, statement.value is not None)
        for statement in node.body
        if isinstance(statement, ast.AnnAssign) and isinstance(statement.target, ast.Name)
    )


def read_namedtuple_call_fields(call: ast.Call, origin: Origin) -> tuple[str, tuple[Parameter, ...]] | None:
    """Reads the type name and the fields of a `namedtuple(...)` or `NamedTuple(...)` call with literal arguments."""
    if len(call.args) < 1 or not isinstance(call.args[0], ast.Constant) or not isinstance(call.args[0].value, str):
        return None

    typename = call.args[0].value

    if origin in TYPED_NAMEDTUPLES:
        names = _typed_namedtuple_names(call)
        defaults = 0
    else:
        read = _namedtuple_names(call)

        if read is None:
            return None

        names, defaults = read

    if names is None or not all(name.isidentifier() and not name.startswith("_") for name in names):
        return None

    first_default = len(names) - defaults

    parameters = tuple(
        Parameter(name, ParameterKind.POSITIONAL_OR_KEYWORD, index >= first_default) for index, name in enumerate(names)
    )

    return typename, parameters


def _string_list(node: ast.expr) -> list[str] | None:
    if isinstance(node, ast.Constant) and isinstance(node.value, str):
        return node.value.replace(",", " ").split()

    if isinstance(node, ast.List | ast.Tuple) and all(
        isinstance(element, ast.Constant) and isinstance(element.value, str) for element in node.elts
    ):
        return [str(element.value) for element in node.elts if isinstance(element, ast.Constant)]

    return None


def _namedtuple_names(call: ast.Call) -> tuple[list[str] | None, int] | None:
    if len(call.args) != 2:
        return None

    defaults = 0

    for keyword in call.keywords:
        if keyword.arg == "rename":
            if _literal_bool(keyword.value) is not False:
                return None
        elif keyword.arg == "defaults":
            if isinstance(keyword.value, ast.Constant) and keyword.value.value is None:
                continue

            if not isinstance(keyword.value, ast.List | ast.Tuple) or any(
                isinstance(e, ast.Starred) for e in keyword.value.elts
            ):
                return None

            defaults = len(keyword.value.elts)
        elif keyword.arg != "module":
            return None

    names = _string_list(call.args[1])

    if names is not None and defaults > len(names):
        return None

    return names, defaults


def _typed_namedtuple_names(call: ast.Call) -> list[str] | None:
    if len(call.args) == 1 and call.keywords and all(keyword.arg is not None for keyword in call.keywords):
        return [str(keyword.arg) for keyword in call.keywords]

    if len(call.args) != 2 or call.keywords:
        return None

    fields = call.args[1]

    if not isinstance(fields, ast.List | ast.Tuple):
        return None

    names: list[str] = []

    for element in fields.elts:
        if not isinstance(element, ast.Tuple) or len(element.elts) != 2:
            return None

        first = element.elts[0]

        if not isinstance(first, ast.Constant) or not isinstance(first.value, str):
            return None

        names.append(first.value)

    return names


def calls_base_init(node: ast.FunctionDef | ast.AsyncFunctionDef) -> bool:
    """Tells whether an `__init__` body calls `super().__init__(...)` as a statement of its own, not under a branch."""
    for statement in node.body:
        if not isinstance(statement, ast.Expr) or not isinstance(statement.value, ast.Call):
            continue

        func = statement.value.func

        if (
            isinstance(func, ast.Attribute)
            and func.attr == "__init__"
            and isinstance(func.value, ast.Call)
            and isinstance(func.value.func, ast.Name)
            and func.value.func.id == "super"
        ):
            return True

    return False
