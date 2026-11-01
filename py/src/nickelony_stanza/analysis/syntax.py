"""Questions about a statement that a rule or the container builder asks."""

from __future__ import annotations

import ast
import fnmatch

from nickelony_stanza.options import Options

Definition = (ast.FunctionDef, ast.AsyncFunctionDef, ast.ClassDef)
Jump = (ast.Return, ast.Raise, ast.Break, ast.Continue)
Import = (ast.Import, ast.ImportFrom)
_TryStar = getattr(ast, "TryStar", ast.Try)


def is_definition(node: ast.AST | None) -> bool:
    """Tells whether a node is a function or class, decorated or not."""
    return isinstance(node, Definition)


def is_import(node: ast.AST | None) -> bool:
    """Tells whether a node is an `import` or `from ... import` statement."""
    return isinstance(node, Import)


def is_jump(node: ast.AST | None) -> bool:
    """Tells whether a node is a `return`, `raise`, `break` or `continue`."""
    return isinstance(node, Jump)


def is_docstring_statement(node: ast.AST | None) -> bool:
    """Tells whether a node is a string expression, which is a docstring when it opens a body."""
    return isinstance(node, ast.Expr) and isinstance(node.value, ast.Constant) and isinstance(node.value.value, str)


def trailing_block(node: ast.AST) -> list[ast.stmt]:
    """Reads the last block of statements a compound statement writes, or an empty list for a simple one."""
    if isinstance(node, ast.If | ast.For | ast.AsyncFor | ast.While):
        return node.orelse or node.body

    if isinstance(node, ast.With | ast.AsyncWith):
        return node.body

    if isinstance(node, ast.Try | _TryStar):
        return node.finalbody or node.orelse or (node.handlers[-1].body if node.handlers else node.body)

    if isinstance(node, ast.Match):
        return node.cases[-1].body if node.cases else []

    return []


def ends_with_definition(node: ast.AST) -> bool:
    """Tells whether a statement is a function or class, or ends in one, which a formatter follows with blank lines."""
    if is_definition(node):
        return True

    block = trailing_block(node)
    return bool(block) and ends_with_definition(block[-1])


def callee_path(node: ast.expr) -> str | None:
    """Writes the callee of a call as a dotted path, such as `logger.info` or `session.get().json`."""
    if isinstance(node, ast.Name):
        return node.id

    if isinstance(node, ast.Attribute):
        owner = callee_path(node.value)
        return None if owner is None else f"{owner}.{node.attr}"

    if isinstance(node, ast.Call):
        owner = callee_path(node.func)
        return None if owner is None else f"{owner}()"

    if isinstance(node, ast.Subscript):
        owner = callee_path(node.value)
        return None if owner is None else f"{owner}[]"

    return None


def callee_names(node: ast.expr) -> list[str]:
    """Lists the names along a callee path, from the root to the called attribute."""
    if isinstance(node, ast.Name):
        return [node.id]

    if isinstance(node, ast.Attribute):
        return [*callee_names(node.value), node.attr]

    if isinstance(node, ast.Call):
        return callee_names(node.func)

    if isinstance(node, ast.Subscript):
        return callee_names(node.value)

    return []


def matches_any(patterns: tuple[str, ...], path: str, names: list[str]) -> bool:
    """Tells whether a pattern matches the dotted path of a call or any single name along it."""
    clean = path.replace("()", "").replace("[]", "")

    return any(
        fnmatch.fnmatchcase(clean, pattern) or any(fnmatch.fnmatchcase(name, pattern) for name in names)
        for pattern in patterns
    )


def is_guard(node: ast.AST | None, options: Options) -> bool:
    """Tells whether a statement is a guard: an `if` that only jumps, an `assert`, or a call of a listed helper."""
    if isinstance(node, ast.If):
        return not node.orelse and len(node.body) == 1 and is_jump(node.body[0])

    if isinstance(node, ast.Assert):
        return True

    if isinstance(node, ast.Expr) and isinstance(node.value, ast.Call):
        path = callee_path(node.value.func)
        return path is not None and matches_any(options.guard_roots, path, callee_names(node.value.func))

    return False


def is_base_call(node: ast.AST | None) -> bool:
    """Tells whether a statement calls a member through `super()`, as in `super().load()` or `await super().load()`."""
    if not isinstance(node, ast.Expr):
        return False

    value = node.value.value if isinstance(node.value, ast.Await) else node.value
    current = value.func if isinstance(value, ast.Call) else None

    while current is not None:
        if isinstance(current, ast.Call) and isinstance(current.func, ast.Name) and current.func.id == "super":
            return True

        current = current.func if isinstance(current, ast.Call) else getattr(current, "value", None)

    return False
