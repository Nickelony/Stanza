"""The parameters a call binds to, and why a call is left unresolved."""

from __future__ import annotations

import ast
import enum
from dataclasses import dataclass
from typing import Literal

# Where a module comes from: the file and the project roots, the standard library, or the other `sys.path` directories.
Source = Literal["project", "stdlib", "installed"]
SOURCES: frozenset[Source] = frozenset({"project", "stdlib", "installed"})

# Which methods a call through `self`, `cls` or `super()` may reach: every name, the `_private` ones, or none.
MethodsMode = Literal["all", "private", "none"]


@dataclass(frozen=True)
class ResolveOptions:
    """The sources a resolution may read and the methods it may bind a dispatched call to."""

    sources: frozenset[Source]
    methods: MethodsMode


class Reason(enum.Enum):
    """Why the resolver leaves a call unresolved."""

    # The callee is an attribute of a runtime value, a call result, a subscript or a lambda.
    RECEIVER_IS_VALUE = "receiver_is_value"

    # A function, lambda, comprehension or class body binds the callee name, so the module name it shadows is not read.
    LOCAL_BINDING = "local_binding"

    # The name is bound more than once, declared `global`, assigned as an attribute, or shadowed by a star import.
    NAME_REBOUND = "name_rebound"

    # The binding sits in an `if`, `try`, `with`, loop or `match`, or a `del` may remove it.
    CONDITIONAL_BINDING = "conditional_binding"

    # The name is bound to a value the resolver does not follow, such as `functools.partial(...)` or a lambda.
    ASSIGNED_VALUE = "assigned_value"

    # Nothing binds the name where the resolver looks: a module attribute, a class member or a global.
    UNBOUND_NAME = "unbound_name"

    # The class has a shape whose constructor or members the resolver does not model.
    CLASS_SHAPE = "class_shape"

    # The call cannot be named as written: an unpacked argument, a surplus argument or one that lands in `*args`.
    CALL_SHAPE = "call_shape"

    # The callee is a builtin or lives in compiled code, such as `len` or a member of `dict`.
    BUILTIN_OR_C = "builtin_or_c"

    # The module is missing, compiled, builtin, sourceless, unparsable or shadowed by another of the same name.
    UNREADABLE_MODULE = "unreadable_module"

    # A decorator the resolver does not see through wraps the callee or its class.
    DECORATOR = "decorator"

    # A class that inherits the method's class reaches another definition of the method.
    DISPATCH_UNSAFE = "dispatch_unsafe"

    # A `.pyi` stub stands beside a module the callee is read through.
    STUB_PRESENT = "stub_present"

    # The callee, or a module the resolver reads on the way, is in a source the options leave out.
    SOURCE_DISABLED = "source_disabled"

    # The call reaches a method through `self`, `cls` or `super()`, and the `methods` option excludes that name.
    METHODS_DISABLED = "methods_disabled"

    # The callee is a form a type checker reads only from positional arguments, such as `typing.cast`.
    SPECIAL_FORM = "special_form"

    # The project holds more files or source than the scan reads, so no project-wide check can clear the call.
    SCAN_TOO_LARGE = "scan_too_large"


@dataclass(frozen=True)
class Skipped:
    """A call, a name or a class the resolver leaves unresolved, with the reason."""

    reason: Reason


class ParameterKind(enum.Enum):
    """How an argument binds to a parameter, as `inspect.Parameter.kind` says."""

    POSITIONAL_ONLY = "positional_only"
    POSITIONAL_OR_KEYWORD = "positional_or_keyword"
    VAR_POSITIONAL = "var_positional"
    KEYWORD_ONLY = "keyword_only"
    VAR_KEYWORD = "var_keyword"


POSITIONAL_KINDS = frozenset({ParameterKind.POSITIONAL_ONLY, ParameterKind.POSITIONAL_OR_KEYWORD})


@dataclass(frozen=True)
class Parameter:
    """A parameter of the callee, in declaration order."""

    name: str
    kind: ParameterKind
    has_default: bool

    @property
    def can_be_named(self) -> bool:
        """Tells whether an argument can bind by name, which a positional-only or mangled parameter forbids."""
        if self.kind != ParameterKind.POSITIONAL_OR_KEYWORD:
            return False

        return not (self.name.startswith("__") and not self.name.endswith("__"))


def parameters_of(arguments: ast.arguments) -> tuple[Parameter, ...]:
    """Reads the parameters a `def` or `lambda` declares, with which of them have a default."""
    positional = [*arguments.posonlyargs, *arguments.args]
    first_default = len(positional) - len(arguments.defaults)

    parameters: list[Parameter] = []

    for index, argument in enumerate(positional):
        kind = (
            ParameterKind.POSITIONAL_ONLY if index < len(arguments.posonlyargs) else ParameterKind.POSITIONAL_OR_KEYWORD
        )

        parameters.append(Parameter(argument.arg, kind, index >= first_default))

    if arguments.vararg is not None:
        parameters.append(Parameter(arguments.vararg.arg, ParameterKind.VAR_POSITIONAL, False))

    for argument, default in zip(arguments.kwonlyargs, arguments.kw_defaults, strict=True):
        parameters.append(Parameter(argument.arg, ParameterKind.KEYWORD_ONLY, default is not None))

    if arguments.kwarg is not None:
        parameters.append(Parameter(arguments.kwarg.arg, ParameterKind.VAR_KEYWORD, False))

    return tuple(parameters)


def drop_receiver(parameters: tuple[Parameter, ...]) -> tuple[Parameter, ...] | Skipped:
    """Drops the parameter a bound method fills with its receiver, which `*args` takes when no positional one exists."""
    if parameters and parameters[0].kind in POSITIONAL_KINDS:
        return parameters[1:]

    if any(parameter.kind == ParameterKind.VAR_POSITIONAL for parameter in parameters):
        return parameters

    return Skipped(Reason.CALL_SHAPE)


def to_positional_only(parameters: tuple[Parameter, ...]) -> tuple[Parameter, ...]:
    """Makes every parameter that takes a positional argument positional-only, so no argument of it takes a name."""
    return tuple(
        Parameter(parameter.name, ParameterKind.POSITIONAL_ONLY, parameter.has_default)
        if parameter.kind == ParameterKind.POSITIONAL_OR_KEYWORD
        else parameter
        for parameter in parameters
    )


def positional_names(parameters: tuple[Parameter, ...]) -> tuple[str, ...]:
    """Lists the names of the parameters a positional argument can fill, in order."""
    return tuple(parameter.name for parameter in parameters if parameter.kind in POSITIONAL_KINDS)
