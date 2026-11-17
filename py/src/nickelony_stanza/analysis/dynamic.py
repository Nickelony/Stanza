"""
The code of a module that can change what a name or a function means without a statement that binds it.

One watch sees the functions a module gives another code object, defaults or signature, as `f.__code__ = g.__code__`.
Another sees every node of one walk of a module, then answers whether the module may rewrite its own namespace.
That is the case when it can write the dictionary `globals()` returns, run code in the namespace of the module,
or reach `exec`, `eval`, `globals`, `vars` or `locals` by a route the watch does not follow.
A use of `globals()` that only reads, as `globals()["name"]`, `globals().get(...)` or `"name" in globals()`,
is no write.
Code that reaches the module object by name, such as `sys.modules[__name__]`, is not seen here.
"""

from __future__ import annotations

import ast

# The functions that hand out the namespace of a module or run code in it, and the builtins table they come from.
_NAMESPACE_FUNCTIONS = frozenset({"exec", "eval", "globals", "vars", "locals"})
_WATCHED_NAMES = _NAMESPACE_FUNCTIONS | {"__builtins__", "builtins"}

# The node types the watches read, and the names of a `Name` they read, so a walk skips the rest without a call.
WATCHED_KINDS = frozenset({ast.Attribute, ast.Call, ast.Subscript, ast.Compare, ast.Import, ast.ImportFrom})
WATCHED_NAMES = _WATCHED_NAMES
_BUILTINS_BASES = frozenset({"builtins", "__builtins__"})

# The methods of a mapping that leave it as it is.
_READING_METHODS = frozenset({"get", "keys", "values", "items", "copy"})


class NamespaceWatch:
    """Collects, node by node, the uses of the namespace functions of one module."""

    def __init__(self) -> None:
        self.rewrites = False

        self._callees: set[int] = set()
        self._reads: set[int] = set()
        self._bases: set[int] = set()
        self._calls: list[tuple[ast.Call, bool]] = []
        self._names: list[tuple[ast.Name, bool]] = []
        self._attributes: list[tuple[ast.Attribute, bool]] = []

    def visit(self, node: ast.AST, *, in_function: bool) -> None:
        """Takes one node, with whether it stands in the body of a function or lambda."""
        if isinstance(node, ast.Name):
            if node.id in _WATCHED_NAMES and isinstance(node.ctx, ast.Load):
                self._names.append((node, in_function))
        elif isinstance(node, ast.Attribute):
            self._visit_attribute(node, in_function)
        elif isinstance(node, ast.Call):
            self._visit_call(node, in_function)
        elif isinstance(node, ast.Subscript):
            if isinstance(node.ctx, ast.Load) and isinstance(node.value, ast.Call):
                self._reads.add(id(node.value))
        elif isinstance(node, ast.Compare):
            if any(isinstance(operator, ast.In | ast.NotIn) for operator in node.ops):
                self._reads.update(
                    id(comparator) for comparator in node.comparators if isinstance(comparator, ast.Call)
                )
        elif isinstance(node, ast.Import):
            if any(alias.name == "builtins" and alias.asname not in (None, "builtins") for alias in node.names):
                self.rewrites = True
        elif (
            isinstance(node, ast.ImportFrom)
            and node.module == "builtins"
            and node.level == 0
            and any(alias.name == "*" or alias.name in _NAMESPACE_FUNCTIONS for alias in node.names)
        ):
            self.rewrites = True

    def _visit_attribute(self, node: ast.Attribute, in_function: bool) -> None:
        if not isinstance(node.ctx, ast.Load):
            return

        if isinstance(node.value, ast.Name):
            if node.value.id == "builtins":
                self._bases.add(id(node.value))
        elif node.attr in _READING_METHODS and isinstance(node.value, ast.Call):
            self._reads.add(id(node.value))

        if node.attr in _NAMESPACE_FUNCTIONS or node.attr == "__dict__":
            self._attributes.append((node, in_function))

    def _visit_call(self, node: ast.Call, in_function: bool) -> None:
        function = node.func

        if isinstance(function, ast.Name) and function.id == "getattr":
            if (
                len(node.args) > 1
                and isinstance(node.args[1], ast.Constant)
                and node.args[1].value in _NAMESPACE_FUNCTIONS
            ):
                self.rewrites = True
        elif _namespace_function(function) is not None:
            self._callees.add(id(function))
            self._calls.append((node, in_function))

    def finish(self) -> bool:
        """Settles whether the module may rewrite its own namespace, after every node has been taken."""
        self.rewrites = (
            self.rewrites
            or any(self._call_rewrites(call, in_function) for call, in_function in self._calls)
            or any(self._name_rewrites(name, in_function) for name, in_function in self._names)
            or any(self._attribute_rewrites(attribute, in_function) for attribute, in_function in self._attributes)
        )

        return self.rewrites

    def _call_rewrites(self, call: ast.Call, in_function: bool) -> bool:
        name = _namespace_function(call.func)

        if name is None:
            return False

        if name == "globals":
            return id(call) not in self._reads

        if name == "exec":
            return not _has_own_globals(call)

        if name == "eval":
            return not in_function and not _has_own_globals(call)

        return not in_function and not call.args

    def _name_rewrites(self, name: ast.Name, in_function: bool) -> bool:
        if id(name) in self._callees:
            return False

        if name.id == "builtins":
            return id(name) not in self._bases

        return _reads_function(name.id, in_function)

    def _attribute_rewrites(self, attribute: ast.Attribute, in_function: bool) -> bool:
        if not isinstance(attribute.value, ast.Name) or attribute.value.id not in _BUILTINS_BASES:
            return False

        if attribute.attr == "__dict__":
            return True

        return id(attribute) not in self._callees and _reads_function(attribute.attr, in_function)


def _namespace_function(function: ast.expr) -> str | None:
    if isinstance(function, ast.Name) and function.id in _NAMESPACE_FUNCTIONS:
        return function.id

    if (
        isinstance(function, ast.Attribute)
        and function.attr in _NAMESPACE_FUNCTIONS
        and isinstance(function.value, ast.Name)
        and function.value.id in _BUILTINS_BASES
    ):
        return function.attr

    return None


def _reads_function(name: str, in_function: bool) -> bool:
    """Tells whether a read of a name, other than a call, hands out the namespace to code the watch cannot follow."""
    return name in {"exec", "globals", "__builtins__"} or (name in _NAMESPACE_FUNCTIONS and not in_function)


def _has_own_globals(call: ast.Call) -> bool:
    """Tells whether an `exec` or `eval` call names a dictionary display of its own for the globals it runs in."""
    arguments = [*call.args[1:2], *(keyword.value for keyword in call.keywords if keyword.arg == "globals")]
    return len(arguments) == 1 and isinstance(arguments[0], ast.Dict)


# The attributes of a function that decide the parameters its call binds, which a module may replace.
_SIGNATURE_ATTRIBUTES = frozenset({"__code__", "__defaults__", "__kwdefaults__", "__signature__"})
_ATTRIBUTE_SETTERS = frozenset({"setattr", "delattr"})

# The attribute copiers of `functools`, with the position of their `assigned` argument.
_COPIERS = {"update_wrapper": 2, "wraps": 1}

# Stands for any function, where an assignment names none that the watch can read.
EVERY_FUNCTION = "*"


class SignatureWatch:
    """Collects the names of the functions a module gives another code object, defaults or signature."""

    def __init__(self) -> None:
        self.names: set[str] = set()

    def visit(self, node: ast.AST) -> None:
        """Takes one node of the walk of a module."""
        if isinstance(node, ast.Attribute):
            if node.attr in _SIGNATURE_ATTRIBUTES and isinstance(node.ctx, ast.Store | ast.Del):
                self.names.add(_function_name(node.value))
        elif isinstance(node, ast.Call):
            self._visit_call(node)

    def _visit_call(self, call: ast.Call) -> None:
        function = call.func

        last = (
            function.id
            if isinstance(function, ast.Name)
            else function.attr
            if isinstance(function, ast.Attribute)
            else ""
        )

        if last in _ATTRIBUTE_SETTERS:
            if (
                len(call.args) > 1
                and isinstance(call.args[1], ast.Constant)
                and call.args[1].value in _SIGNATURE_ATTRIBUTES
            ):
                self.names.add(_function_name(call.args[0]))
        elif last in _COPIERS:
            self._visit_copier(call, _COPIERS[last])

    def _visit_copier(self, call: ast.Call, position: int) -> None:
        """Reads the `assigned` argument of `update_wrapper` or `wraps`, which names the attributes it copies."""
        keyword = next((keyword.value for keyword in call.keywords if keyword.arg == "assigned"), None)
        assigned = call.args[position] if len(call.args) > position else keyword

        if assigned is None:
            return

        if not isinstance(assigned, ast.List | ast.Tuple | ast.Set) or not all(
            isinstance(element, ast.Constant) and isinstance(element.value, str) for element in assigned.elts
        ):
            self.names.add(EVERY_FUNCTION)
        elif any(
            isinstance(element, ast.Constant) and element.value in _SIGNATURE_ATTRIBUTES for element in assigned.elts
        ):
            self.names.add(_function_name(call.args[0]) if call.args and position == 2 else EVERY_FUNCTION)


def _function_name(target: ast.expr) -> str:
    """Reads the last name of the function an attribute is set on, or every function when it is no name."""
    if isinstance(target, ast.Name):
        return target.id

    return target.attr if isinstance(target, ast.Attribute) else EVERY_FUNCTION
