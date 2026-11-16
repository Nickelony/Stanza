"""Questions about a statement that a rule or the container builder asks."""

from __future__ import annotations

import ast
import fnmatch
from collections.abc import Iterator

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


def read_imported_names(tree: ast.AST) -> frozenset[str]:
    """Reads the names the imports of a tree bind: `os` for `import os.path`, `Counter` for `from x import Counter`."""
    names: set[str] = set()

    for node in ast.walk(tree):
        if isinstance(node, ast.Import):
            names.update(alias.asname or alias.name.split(".")[0] for alias in node.names)
        elif isinstance(node, ast.ImportFrom):
            names.update(alias.asname or alias.name for alias in node.names if alias.name != "*")

    return frozenset(names)


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


def target_names(target: ast.AST) -> set[str]:
    """Reads binding targets without treating a member owner or subscript index as a write."""
    if isinstance(target, ast.Name) and isinstance(target.ctx, ast.Store):
        return {target.id}

    if isinstance(target, ast.Tuple | ast.List):
        return {name for element in target.elts for name in target_names(element)}

    return target_names(target.value) if isinstance(target, ast.Starred) else set()


def _lambda_bindings(node: ast.Lambda) -> set[str]:
    arguments = node.args
    names = {argument.arg for argument in (*arguments.posonlyargs, *arguments.args, *arguments.kwonlyargs)}

    names.update(argument.arg for argument in (arguments.vararg, arguments.kwarg) if argument is not None)
    pending: list[ast.AST] = [node.body]

    while pending:
        current = pending.pop()

        if isinstance(current, ast.Lambda):
            pending.extend(current.args.defaults)
            pending.extend(default for default in current.args.kw_defaults if default is not None)
        else:
            if isinstance(current, ast.NamedExpr):
                names.update(target_names(current.target))

            pending.extend(ast.iter_child_nodes(current))

    return names


def read_value_names(node: ast.expr) -> set[str]:
    """Reads outer value names, including free captures and defaults but excluding expression-local bindings."""
    names: set[str] = set()
    bound: dict[str, int] = {}
    pending: list[tuple[ast.AST | None, set[str]]] = [(node, set())]

    while pending:
        current, leaving = pending.pop()

        if current is None:
            for name in leaving:
                bound[name] -= 1

            continue

        if isinstance(current, ast.Name):
            if isinstance(current.ctx, ast.Load) and not bound.get(current.id, 0):
                names.add(current.id)

            continue

        local: set[str] = set()

        if isinstance(current, ast.Lambda):
            local = _lambda_bindings(current)
            outer = [*current.args.defaults, *(value for value in current.args.kw_defaults if value is not None)]

            children: list[ast.AST] = [current.body]
        elif isinstance(current, ast.ListComp | ast.SetComp | ast.DictComp | ast.GeneratorExp):
            local = {name for generator in current.generators for name in target_names(generator.target)}
            outer = [current.generators[0].iter]
            children = [current.key, current.value] if isinstance(current, ast.DictComp) else [current.elt]

            children.extend(generator.iter for generator in current.generators[1:])
            children.extend(generator.target for generator in current.generators)
            children.extend(condition for generator in current.generators for condition in generator.ifs)
        else:
            pending.extend((child, set()) for child in ast.iter_child_nodes(current))
            continue

        # The enclosing scope stays active for defaults and the first iterable, after the local body is read.
        pending.extend((child, set()) for child in outer)

        for name in local:
            bound[name] = bound.get(name, 0) + 1

        pending.append((None, local))
        pending.extend((child, set()) for child in children)

    return names


def executing_nodes(node: ast.AST) -> Iterator[ast.AST]:
    """Walks one statement scope, excluding nested definitions and comprehensions with their expression sites."""
    pending = [node]

    while pending:
        current = pending.pop()

        if isinstance(current, (*Definition, ast.Lambda, ast.GeneratorExp, ast.ListComp, ast.SetComp, ast.DictComp)):
            continue

        yield current
        pending.extend(ast.iter_child_nodes(current))


def read_name_binding(node: ast.AST | None) -> tuple[str, ast.expr] | None:
    """Reads the name and the value of an assignment to one plain name, annotated or not, or None for any other."""
    if isinstance(node, ast.Assign) and len(node.targets) == 1:
        target, value = node.targets[0], node.value
    elif isinstance(node, ast.AnnAssign) and node.value is not None:
        target, value = node.target, node.value
    else:
        return None

    return (target.id, value) if isinstance(target, ast.Name) else None


def bound_names(node: ast.AST | None) -> set[str]:
    """Reads the names an assignment binds, or none for any other statement."""
    if isinstance(node, ast.Assign):
        targets = node.targets
    elif isinstance(node, ast.AnnAssign | ast.AugAssign) and node.value is not None:
        targets = [node.target]
    else:
        return set()

    return {name for target in targets for name in target_names(target)}


def is_assertion(node: ast.AST | None, options: Options) -> bool:
    """Tells whether a statement is an `assert` or a call that matches `assertion-roots`, awaited or not."""
    if isinstance(node, ast.Assert):
        return True

    if not isinstance(node, ast.Expr):
        return False

    call = node.value.value if isinstance(node.value, ast.Await) else node.value

    if not isinstance(call, ast.Call):
        return False

    path = callee_path(call.func)
    return path is not None and matches_any(options.assertion_roots, path, callee_names(call.func))


def is_guard(node: ast.AST | None, options: Options) -> bool:
    """Tells whether a statement is a guard: an `if` that only jumps, an `assert`, or a call of a listed helper."""
    if isinstance(node, ast.If):
        return not node.orelse and len(node.body) == 1 and is_jump(node.body[0])

    if isinstance(node, ast.Assert):
        return True

    if not isinstance(node, ast.Expr):
        return False

    # A helper guards whether it is awaited or not.
    call = node.value.value if isinstance(node.value, ast.Await) else node.value

    if not isinstance(call, ast.Call):
        return False

    path = callee_path(call.func)
    return path is not None and matches_any(options.guard_roots, path, callee_names(call.func))


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
