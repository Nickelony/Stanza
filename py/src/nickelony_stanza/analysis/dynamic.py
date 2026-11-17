"""
The code of a module that can change what a name or a function means without a statement that binds it.

One watch sees the functions a module gives another code object, as `f.__code__ = g.__code__`,
and whether it does so for a function it cannot name.
Another sees every node of one walk of a module, then answers whether the module may rewrite its own namespace.
That is the case when it can write the dictionary `globals()` returns, run code in the namespace of the module,
or reach `exec`, `eval`, `globals`, `vars` or `locals` by a route the watch does not follow.
A use of `globals()` that only reads, as `globals()["name"]`, `globals().get(...)`, `"name" in globals()`,
`dict(globals())` or `for name in globals()`, is no write.
Code that reaches the module object by name, such as `sys.modules[__name__]`, a frame, `__globals__`
or `object.__setattr__`, is not seen here.
"""

from __future__ import annotations

import ast

# The functions that hand out the namespace of a module or run code in it, and the builtins table they come from.
_NAMESPACE_FUNCTIONS = frozenset({"exec", "eval", "globals", "vars", "locals"})
_WATCHED_NAMES = _NAMESPACE_FUNCTIONS | {"__builtins__", "builtins"}

# The node types the watches read, and the names of a `Name` they read, so a walk skips the rest without a call.
WATCHED_KINDS = frozenset(
    {
        ast.Attribute,
        ast.Call,
        ast.Subscript,
        ast.Compare,
        ast.Import,
        ast.ImportFrom,
        ast.For,
        ast.AsyncFor,
        ast.comprehension,
    }
)

WATCHED_NAMES = _WATCHED_NAMES
_BUILTINS_BASES = frozenset({"builtins", "__builtins__"})

# The methods of a mapping that leave it as it is.
_READING_METHODS = frozenset({"get", "keys", "values", "items", "copy"})

# The functions that only read the mapping or the module they are given.
_READING_FUNCTIONS = frozenset({"__import__", "dir", "sorted", "list", "dict", "set", "len", "iter"})


class NamespaceWatch:
    """Collects, node by node, the uses of the namespace functions of one module."""

    def __init__(self) -> None:
        self.rewrites = False

        self._binds_builtins = False
        self._getattr_of_builtins = False
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
        elif isinstance(node, ast.For | ast.AsyncFor | ast.comprehension):
            if isinstance(node.iter, ast.Call):
                self._reads.add(id(node.iter))
        elif isinstance(node, ast.Import):
            self._visit_import(node)
        elif isinstance(node, ast.ImportFrom):
            self._visit_import_from(node)

    def _visit_import(self, node: ast.Import) -> None:
        for alias in node.names:
            if alias.name == "builtins":
                if alias.asname in (None, "builtins"):
                    self._binds_builtins = True
                else:
                    self.rewrites = True

    def _visit_import_from(self, node: ast.ImportFrom) -> None:
        if (
            node.module == "builtins"
            and node.level == 0
            and any(alias.name == "*" or alias.name in _NAMESPACE_FUNCTIONS for alias in node.names)
        ):
            self.rewrites = True

        if any((alias.asname or alias.name) == "builtins" for alias in node.names):
            self._binds_builtins = True

    def _visit_attribute(self, node: ast.Attribute, in_function: bool) -> None:
        if isinstance(node.value, ast.Name) and node.value.id == "builtins":
            self._bases.add(id(node.value))

        if not isinstance(node.ctx, ast.Load):
            return

        if isinstance(node.value, ast.Call) and node.attr in _READING_METHODS:
            self._reads.add(id(node.value))

        if node.attr in _NAMESPACE_FUNCTIONS or node.attr == "__dict__":
            self._attributes.append((node, in_function))

    def _visit_call(self, node: ast.Call, in_function: bool) -> None:
        function = node.func

        if isinstance(function, ast.Name) and function.id == "getattr":
            self._visit_getattr(node)
        elif isinstance(function, ast.Name) and function.id in _READING_FUNCTIONS:
            self._reads.update(id(argument) for argument in node.args if isinstance(argument, ast.Call))

            if function.id == "dir":
                self._bases.update(
                    id(argument)
                    for argument in node.args
                    if isinstance(argument, ast.Name) and argument.id == "builtins"
                )
        elif _namespace_function(function, builtins_bound=True) is not None:
            self._callees.add(id(function))
            self._calls.append((node, in_function))

    def _visit_getattr(self, node: ast.Call) -> None:
        """Reads a `getattr` that fetches a namespace function, which reaches one only when it asks the builtins."""
        if not (
            len(node.args) > 1
            and isinstance(node.args[1], ast.Constant)
            and node.args[1].value in _NAMESPACE_FUNCTIONS
            and isinstance(node.args[0], ast.Name)
        ):
            return

        if node.args[0].id == "__builtins__":
            self.rewrites = True
        elif node.args[0].id == "builtins":
            self._getattr_of_builtins = True

    def finish(self) -> bool:
        """Settles whether the module may rewrite its own namespace, after every node has been taken."""
        self.rewrites = (
            self.rewrites
            or (self._getattr_of_builtins and self._binds_builtins)
            or any(self._call_rewrites(call, in_function) for call, in_function in self._calls)
            or any(self._name_rewrites(name, in_function) for name, in_function in self._names)
            or any(self._attribute_rewrites(attribute, in_function) for attribute, in_function in self._attributes)
        )

        return self.rewrites

    def _call_rewrites(self, call: ast.Call, in_function: bool) -> bool:
        name = _namespace_function(call.func, builtins_bound=self._binds_builtins)

        if name is None:
            return False

        if name == "exec":
            return not _has_own_namespace(call)

        if name == "eval":
            return not in_function and not _has_own_namespace(call) and not _is_plain_expression(call)

        if id(call) in self._reads:
            return False

        return name == "globals" or (not in_function and not call.args)

    def _name_rewrites(self, name: ast.Name, in_function: bool) -> bool:
        if id(name) in self._callees:
            return False

        if name.id == "builtins":
            return self._binds_builtins and id(name) not in self._bases

        return _reads_function(name.id, in_function)

    def _attribute_rewrites(self, attribute: ast.Attribute, in_function: bool) -> bool:
        if not isinstance(attribute.value, ast.Name) or attribute.value.id not in _BUILTINS_BASES:
            return False

        if attribute.value.id == "builtins" and not self._binds_builtins:
            return False

        if attribute.attr == "__dict__":
            return True

        return id(attribute) not in self._callees and _reads_function(attribute.attr, in_function)


def _namespace_function(function: ast.expr, *, builtins_bound: bool) -> str | None:
    if isinstance(function, ast.Name) and function.id in _NAMESPACE_FUNCTIONS:
        return function.id

    if (
        isinstance(function, ast.Attribute)
        and function.attr in _NAMESPACE_FUNCTIONS
        and isinstance(function.value, ast.Name)
        and function.value.id in _BUILTINS_BASES
        and (builtins_bound or function.value.id != "builtins")
    ):
        return function.attr

    return None


def _reads_function(name: str, in_function: bool) -> bool:
    """Tells whether a read of a name, other than a call, hands out the namespace to code the watch cannot follow."""
    return name in {"exec", "globals", "__builtins__"} or (name in _NAMESPACE_FUNCTIONS and not in_function)


def _has_own_namespace(call: ast.Call) -> bool:
    """Tells whether an `exec` or `eval` call runs in globals it is given, which are not those of the module."""
    arguments = [*call.args[1:2], *(keyword.value for keyword in call.keywords if keyword.arg == "globals")]

    if len(arguments) != 1:
        return False

    argument = arguments[0]

    if isinstance(argument, ast.Starred) or (isinstance(argument, ast.Constant) and argument.value is None):
        return False

    return not (isinstance(argument, ast.Call) and _namespace_function(argument.func, builtins_bound=True) is not None)


def _is_plain_expression(call: ast.Call) -> bool:
    """Tells whether an `eval` call holds a constant text that binds no name, since only `:=` can bind one."""
    code = call.args[0] if call.args else None
    return isinstance(code, ast.Constant) and isinstance(code.value, str) and ":=" not in code.value


# The attribute of a function that decides the parameters its call binds: only a code object carries their names.
_CODE_ATTRIBUTE = "__code__"
_ATTRIBUTE_SETTERS = frozenset({"setattr", "delattr"})

# The attribute copiers of `functools`, with the position of their `assigned` argument.
_COPIERS = {"update_wrapper": 2, "wraps": 1}

# The default lists of `functools`, which copy no code object.
_DEFAULT_COPIES = frozenset({"WRAPPER_ASSIGNMENTS", "WRAPPER_UPDATES"})


class SignatureWatch:
    """Collects the names of the functions a module gives another code object, and whether one has no name to read."""

    def __init__(self) -> None:
        self.names: set[str] = set()
        self.unreadable = False

    def visit(self, node: ast.AST) -> None:
        """Takes one node of the walk of a module."""
        if isinstance(node, ast.Attribute):
            if node.attr == _CODE_ATTRIBUTE and isinstance(node.ctx, ast.Store | ast.Del):
                self._record(node.value)
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
            if call.args and len(call.args) > 1 and _is_text(call.args[1], _CODE_ATTRIBUTE):
                self._record(call.args[0])
        elif last in _COPIERS:
            self._visit_copier(call, _COPIERS[last])

    def _visit_copier(self, call: ast.Call, position: int) -> None:
        """Reads the `assigned` argument of `update_wrapper` or `wraps`, which names the attributes it copies."""
        if any(isinstance(argument, ast.Starred) for argument in call.args) or any(
            keyword.arg is None for keyword in call.keywords
        ):
            self.unreadable = True
            return

        keyword = next((keyword.value for keyword in call.keywords if keyword.arg == "assigned"), None)
        assigned = call.args[position] if len(call.args) > position else keyword

        if assigned is None:
            return

        copied = _copied_attributes(assigned)

        if copied is None:
            self.unreadable = True
        elif _CODE_ATTRIBUTE in copied:
            # `update_wrapper` names the function it changes first, but `wraps` hands it on to the decorated one.
            if position == 2 and call.args:
                self._record(call.args[0])
            else:
                self.unreadable = True

    def _record(self, target: ast.expr) -> None:
        """Notes the last name of the function an attribute is set on, or that the function has no name to read."""
        if isinstance(target, ast.Name):
            self.names.add(target.id)
        elif isinstance(target, ast.Attribute):
            self.names.add(target.attr)
        else:
            self.unreadable = True


def _is_text(node: ast.expr, text: str) -> bool:
    return isinstance(node, ast.Constant) and node.value == text


def _copied_attributes(assigned: ast.expr) -> frozenset[str] | None:
    """
    Reads the attribute names an `assigned` argument lists, or `None` when it is computed.

    The defaults of `functools`, as a name or an attribute, copy no code object, and a literal list may join them.
    """
    if isinstance(assigned, ast.Name):
        return frozenset() if assigned.id in _DEFAULT_COPIES else None

    if isinstance(assigned, ast.Attribute):
        return frozenset() if assigned.attr in _DEFAULT_COPIES else None

    if isinstance(assigned, ast.List | ast.Tuple | ast.Set):
        names = [
            element.value
            for element in assigned.elts
            if isinstance(element, ast.Constant) and isinstance(element.value, str)
        ]

        return frozenset(names) if len(names) == len(assigned.elts) else None

    if isinstance(assigned, ast.BinOp) and isinstance(assigned.op, ast.Add):
        left = _copied_attributes(assigned.left)
        right = _copied_attributes(assigned.right)

        return None if left is None or right is None else left | right

    return None
