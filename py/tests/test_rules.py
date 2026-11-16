"""One case for each behavior of each rule: the text before, the text after, and the clean texts."""

from __future__ import annotations

import pytest

from nickelony_stanza import Options
from tests.helpers import Case, case, check_case, fix_with

CASES = [
    # ZA3017 separate-file-header
    case(
        "header: the shebang, the cookie and the docstring",
        "ZA3017",
        '#!/usr/bin/env python\n# -*- coding: utf-8 -*-\n"""Doc."""\nimport os\n',
        '#!/usr/bin/env python\n# -*- coding: utf-8 -*-\n\n"""Doc."""\n\nimport os\n',
    ),
    case(
        "header: a cookie above code",
        "ZA3017",
        "# -*- coding: utf-8 -*-\nimport os\n",
        "# -*- coding: utf-8 -*-\n\nimport os\n",
    ),
    case(
        "header: a cookie on the second line",
        "ZA3017",
        "# header\n# coding: latin-1\nx = 1\n",
        "# header\n# coding: latin-1\n\nx = 1\n",
    ),
    case(
        "header: a license comment is no preamble",
        "ZA3017",
        '# Copyright\n"""Doc."""\nx = 1\n',
        '# Copyright\n"""Doc."""\n\nx = 1\n',
    ),
    case(
        "header: the docstring above a comment",
        "ZA3017",
        '"""Doc."""\n# note\nx = 1\n',
        '"""Doc."""\n\n# note\nx = 1\n',
    ),
    case("header: a docstring that already has its blank line", "ZA3017", '"""Doc."""\n\nx = 1\n'),
    case(
        "header: a string below the first statement is no docstring",
        "ZA3017",
        'x = 1\n"""About x."""\ny = 2\n',
    ),
    case("header: a docstring and nothing else", "ZA3017", '"""Doc."""\n'),
    case("header: a shebang that stands alone", "ZA3017", "#!/usr/bin/env python\n"),
    # ZA3015 separate-import-section
    case(
        "imports: the section above code",
        "ZA3015",
        "import os\nimport sys\nx = 1\n",
        "import os\nimport sys\n\nx = 1\n",
    ),
    case(
        "imports: a section in a function",
        "ZA3015",
        "def f():\n    import os\n    return os\n",
        "def f():\n    import os\n\n    return os\n",
    ),
    case(
        "imports: a comment below the section",
        "ZA3015",
        "import os\n# note\nx = 1\n",
        "import os\n\n# note\nx = 1\n",
    ),
    case(
        "imports: a section in the middle of a file",
        "ZA3015",
        "x = 1\nimport os\ny = 2\n",
        "x = 1\nimport os\n\ny = 2\n",
    ),
    case("imports: a section above a function", "ZA3015", "import os\ndef f():\n    pass\n"),
    case("imports: a section that already has its blank line", "ZA3015", "import os\n\nx = 1\n"),
    case("imports: only imports", "ZA3015", "import os\nfrom sys import path\n"),
    # ZA3001 separate-multi-line-constructs
    case(
        "multi-line: a block between two statements",
        "ZA3001",
        "a = 1\nif a:\n    b()\nc = 2\n",
        "a = 1\n\nif a:\n    b()\n\nc = 2\n",
    ),
    case(
        "multi-line: a call that spans lines",
        "ZA3001",
        "x = foo(\n    1,\n)\ny = 2\n",
        "x = foo(\n    1,\n)\n\ny = 2\n",
    ),
    case(
        "multi-line: two multi-line imports stay together",
        "ZA3001",
        "from a import (\n    b,\n)\nfrom c import (\n    d,\n)\n",
    ),
    case("multi-line: a function neighbor belongs to the formatter", "ZA3001", "x = 1\ndef f():\n    pass\n"),
    case(
        "multi-line: a class body",
        "ZA3001",
        "class A:\n    x = foo(\n        1\n    )\n    y = 2\n",
        "class A:\n    x = foo(\n        1\n    )\n\n    y = 2\n",
    ),
    case(
        "multi-line: the cases of a match belong to the case rule",
        "ZA3001",
        "match v:\n    case 1:\n        a()\n    case 2:\n        b()\n",
    ),
    case("multi-line: statements that share a line", "ZA3001", "a = 1; b = foo(\n    2\n)\n"),
    case(
        "multi-line: a string statement",
        "ZA3001",
        'x = 1\ny = """\ntext\n"""\nz = 2\n',
        'x = 1\n\ny = """\ntext\n"""\n\nz = 2\n',
    ),
    case("multi-line: single-line neighbors", "ZA3001", "a = 1\nb = 2\n"),
    case(
        "multi-line: a try statement",
        "ZA3001",
        "x = 1\ntry:\n    f()\nexcept E:\n    g()\ny = 2\n",
        "x = 1\n\ntry:\n    f()\nexcept E:\n    g()\n\ny = 2\n",
    ),
    # ZA3002 separate-declaration-runs
    case("declarations: a run above a call", "ZA3002", "x = 1\ny = 2\nprint(x)\n", "x = 1\ny = 2\n\nprint(x)\n"),
    case("declarations: a lone declaration stays tight", "ZA3002", "x = 1\nprint(x)\n"),
    case(
        "declarations: annotated and plain bindings are one kind",
        "ZA3002",
        "a: int = 1\nb = 2\nprint(b)\n",
        "a: int = 1\nb = 2\n\nprint(b)\n",
    ),
    case(
        "declarations: a type alias is a kind of its own",
        "ZA3002",
        "type A = int\ntype B = str\nc = 1\nd = 2\n",
        "type A = int\ntype B = str\n\nc = 1\nd = 2\n",
    ),
    case(
        "declarations: unpacking is a declaration",
        "ZA3002",
        "a, b = 1, 2\nc = 3\nprint(c)\n",
        "a, b = 1, 2\nc = 3\n\nprint(c)\n",
    ),
    case(
        "declarations: global names",
        "ZA3002",
        "def f():\n    global a\n    global b\n    a = 1\n",
        "def f():\n    global a\n    global b\n\n    a = 1\n",
    ),
    case(
        "declarations: a class body is left to the member rule",
        "ZA3002",
        "class A:\n    x = 1\n    y = 2\n    print(x)\n",
    ),
    case(
        "declarations: a run in a function",
        "ZA3002",
        "def f():\n    a = 1\n    b = 2\n    return a + b\n",
        "def f():\n    a = 1\n    b = 2\n\n    return a + b\n",
    ),
    # ZA3008 separate-assignment-runs
    case(
        "assignments: attribute assignments above a call",
        "ZA3008",
        "def f(self):\n    self.a = 1\n    self.b = 2\n    super().f()\n",
        "def f(self):\n    self.a = 1\n    self.b = 2\n\n    super().f()\n",
    ),
    case(
        "assignments: augmented assignments",
        "ZA3008",
        "total += 1\ncount += 1\nprint(total)\n",
        "total += 1\ncount += 1\n\nprint(total)\n",
    ),
    case(
        "assignments: item assignments",
        "ZA3008",
        "d['a'] = 1\nd['b'] = 2\nx = 3\n",
        "d['a'] = 1\nd['b'] = 2\n\nx = 3\n",
    ),
    case("assignments: a lone assignment", "ZA3008", "self.a = 1\nprint(1)\n"),
    case("assignments: names are declarations", "ZA3008", "a = 1\nb = 2\nprint(a)\n"),
    # ZA3004 separate-invocation-runs
    case(
        "invocations: a run of one helper",
        "ZA3004",
        "print(a)\nprint(b)\nlog(c)\n",
        "print(a)\nprint(b)\n\nlog(c)\n",
    ),
    case("invocations: different callee paths", "ZA3004", "logger.info(a)\nlogger.debug(b)\nrun()\n"),
    case(
        "invocations: a chain is part of the path",
        "ZA3004",
        "a.b().c()\na.b().c()\nrun()\n",
        "a.b().c()\na.b().c()\n\nrun()\n",
    ),
    case(
        "invocations: an awaited call without the await rule",
        "ZA3004",
        "await asyncio.sleep(1)\nawait asyncio.sleep(2)\nrun()\n",
        "await asyncio.sleep(1)\nawait asyncio.sleep(2)\n\nrun()\n",
    ),
    case("invocations: a call with no callee path joins no run", "ZA3004", "(a or b)()\n(a or b)()\nrun()\n"),
    # ZA3005 separate-assertion-runs
    case(
        "assertions: assert statements",
        "ZA3005",
        "result = f()\nassert result\nassert other\ncleanup()\n",
        "result = f()\n\nassert result\nassert other\n\ncleanup()\n",
    ),
    case(
        "assertions: unittest methods",
        "ZA3005",
        "x = f()\nself.assertEqual(x, 1)\nself.assertTrue(x)\nrun()\n",
        "x = f()\n\nself.assertEqual(x, 1)\nself.assertTrue(x)\n\nrun()\n",
    ),
    case(
        "assertions: mock assertions",
        "ZA3005",
        "m.call()\nm.assert_called_once()\nm.assert_called_with(1)\n",
        "m.call()\n\nm.assert_called_once()\nm.assert_called_with(1)\n",
    ),
    case("assertions: a lone assertion", "ZA3005", "x = f()\nassert x\nrun()\n"),
    case(
        "assertions: custom roots",
        "ZA3005",
        "x = f()\nverify(x)\nverify(y)\nrun()\n",
        "x = f()\n\nverify(x)\nverify(y)\n\nrun()\n",
        Options(assertion_roots=("verify",)),
    ),
    case(
        "assertions: raises contexts stay one run",
        "ZA3005",
        "x = 1\nwith pytest.raises(E):\n    f()\nwith pytest.raises(E):\n    g()\nrun()\n",
        "x = 1\n\nwith pytest.raises(E):\n    f()\nwith pytest.raises(E):\n    g()\n\nrun()\n",
    ),
    # ZA3006 separate-await-runs
    case(
        "awaits: a run of await statements",
        "ZA3006",
        "await a()\nawait b()\nprint(1)\n",
        "await a()\nawait b()\n\nprint(1)\n",
    ),
    case(
        "awaits: an awaited declaration is no await statement",
        "ZA3006",
        "x = await a()\ny = await b()\nprint(1)\n",
    ),
    case(
        "awaits: an awaited assertion stays an assertion",
        "ZA3006",
        "await a()\nawait b()\nassert_ok()\nawait c()\n",
        "await a()\nawait b()\n\nassert_ok()\nawait c()\n",
    ),
    # ZA3020 separate-yield-runs
    case(
        "yields: a run of yield statements",
        "ZA3020",
        "def g():\n    yield a\n    yield from b\n    print(1)\n",
        "def g():\n    yield a\n    yield from b\n\n    print(1)\n",
    ),
    case(
        "yields: a run that follows a stanza",
        "ZA3020",
        "def g():\n    print(1)\n    yield a\n    yield\n",
        "def g():\n    print(1)\n\n    yield a\n    yield\n",
    ),
    case("yields: one yield is no run", "ZA3020", "def g():\n    print(1)\n    yield a\n    print(2)\n"),
    case(
        "yields: a yield that binds a name is a declaration",
        "ZA3020",
        "def g():\n    a = yield\n    b = yield\n    print(1)\n",
    ),
    case(
        "yields: a noqa on the statement below the gap silences the finding",
        "ZA3020",
        "def g():\n    yield a\n    yield b\n    print(1)  # noqa: ZA3020\n",
    ),
    # ZA3009 separate-jump-statements
    case(
        "jumps: a return below a stanza",
        "ZA3009",
        "def f():\n    a = load()\n    validate(a)\n    a.close()\n    return a\n",
        "def f():\n    a = load()\n    validate(a)\n    a.close()\n\n    return a\n",
    ),
    case("jumps: one line above stays tight", "ZA3009", "def f():\n    a = load()\n    return a\n"),
    case(
        "jumps: a raise",
        "ZA3009",
        "def f():\n    a = 1\n    b = 2\n    raise E\n",
        "def f():\n    a = 1\n    b = 2\n\n    raise E\n",
    ),
    case(
        "jumps: continue in a loop",
        "ZA3009",
        "for x in y:\n    a = 1\n    b = 2\n    continue\n",
        "for x in y:\n    a = 1\n    b = 2\n\n    continue\n",
    ),
    case("jumps: a jump that opens its block", "ZA3009", "def f():\n    return 1\n"),
    case(
        "jumps: a statement of several lines above",
        "ZA3009",
        "def f():\n    x = g(\n        1\n    )\n    return x\n",
        "def f():\n    x = g(\n        1\n    )\n\n    return x\n",
    ),
    case(
        "jumps: a comment above the jump moves with it",
        "ZA3009",
        "def f():\n    x = 1\n    y = 2\n    # done\n    return x\n",
        "def f():\n    x = 1\n    y = 2\n\n    # done\n    return x\n",
    ),
    # ZA3011 separate-guard-clauses
    case(
        "guards: two guards then code",
        "ZA3011",
        "def f(a, b):\n    if a is None:\n        raise ValueError\n    if b is None:\n        raise ValueError\n    return a\n",
        "def f(a, b):\n    if a is None:\n        raise ValueError\n    if b is None:\n        raise ValueError\n\n    return a\n",
    ),
    case(
        "guards: a docstring opens the function",
        "ZA3011",
        'def f(a):\n    """Doc."""\n    if a is None:\n        return\n    use(a)\n',
        'def f(a):\n    """Doc."""\n    if a is None:\n        return\n\n    use(a)\n',
    ),
    case(
        "guards: an else makes it no guard",
        "ZA3011",
        "def f(a):\n    if a:\n        return 1\n    else:\n        return 2\n    x()\n",
    ),
    case(
        "guards: a loop body",
        "ZA3011",
        "for x in y:\n    if x:\n        continue\n    use(x)\n",
        "for x in y:\n    if x:\n        continue\n\n    use(x)\n",
    ),
    case(
        "guards: a helper call",
        "ZA3011",
        "def f(a):\n    check(a)\n    use(a)\n    stop(a)\n",
        "def f(a):\n    check(a)\n\n    use(a)\n    stop(a)\n",
        Options(guard_roots=("check",)),
    ),
    case(
        "guards: a helper call that is not listed",
        "ZA3011",
        "def f(a):\n    check(a)\n    use(a)\n    stop(a)\n",
    ),
    case(
        "guards: an assert statement",
        "ZA3011",
        "def f(a):\n    assert a\n    use(a)\n    stop(a)\n",
        "def f(a):\n    assert a\n\n    use(a)\n    stop(a)\n",
    ),
    case(
        "guards: a chain of asserts and an if",
        "ZA3011",
        "def f(a):\n    assert a\n    assert a.b\n    if a.c:\n        return\n    use(a)\n    stop(a)\n",
        "def f(a):\n    assert a\n    assert a.b\n    if a.c:\n        return\n\n    use(a)\n    stop(a)\n",
    ),
    case(
        "guards: an assert that follows other code",
        "ZA3011",
        "def f(a):\n    x = 1\n    assert a\n    use(a)\n",
    ),
    case(
        "guards: a guard that follows other code",
        "ZA3011",
        "def f(a):\n    x = 1\n    if a:\n        return\n    use(a)\n",
    ),
    case("guards: a module is no block", "ZA3011", "if a:\n    raise E\nuse(a)\n"),
    # ZA3012 separate-commented-siblings
    case("comments: a comment above a sibling", "ZA3012", "a = 1\n# note\nb = 2\n", "a = 1\n\n# note\nb = 2\n"),
    case("comments: a noqa line", "ZA3012", "a = 1\n# noqa: E501\nb = 2\n"),
    case("comments: a multi-line neighbor", "ZA3012", "a = 1\n# note\nb = foo(\n    1\n)\n"),
    case("comments: a detached comment", "ZA3012", "a = 1\n\n# note\n\nb = 2\n"),
    case(
        "comments: two comment lines move together",
        "ZA3012",
        "a = 1\n# one\n# two\nb = 2\n",
        "a = 1\n\n# one\n# two\nb = 2\n",
    ),
    case(
        "comments: a class body",
        "ZA3012",
        "class A:\n    a = 1\n    # note\n    b = 2\n",
        "class A:\n    a = 1\n\n    # note\n    b = 2\n",
    ),
    case("comments: a trailing comment is not a heading", "ZA3012", "a = 1  # note\nb = 2\n"),
    case("comments: a deeper comment trails the block above", "ZA3012", "if x:\n    a()\n    # trails\nb = 2\n"),
    # ZA3013 separate-member-groups
    case(
        "members: the class docstring",
        "ZA3013",
        'class A:\n    """Doc."""\n    x = 1\n',
        'class A:\n    """Doc."""\n\n    x = 1\n',
    ),
    case(
        "members: fields and attributes",
        "ZA3013",
        "class A:\n    x: int\n    y: int\n    Z = 1\n    W = 2\n",
        "class A:\n    x: int\n    y: int\n\n    Z = 1\n    W = 2\n",
    ),
    case(
        "members: one run of enum members",
        "ZA3013",
        "class C(Enum):\n    RED = 1\n    GREEN = 2\n    BLUE = 3\n",
    ),
    case(
        "members: a lone attribute",
        "ZA3013",
        "class A:\n    x: int\n    y: int\n    z = 1\n    f()\n",
        "class A:\n    x: int\n    y: int\n\n    z = 1\n    f()\n",
    ),
    # ZA3051 keep-two-units-together
    case("pair: two single-line statements", "ZA3051", "x = f()\n\nreturn x\n", "x = f()\nreturn x\n"),
    case(
        "pair: in a function",
        "ZA3051",
        "def f():\n    x = 1\n\n    return x\n",
        "def f():\n    x = 1\n    return x\n",
    ),
    case("pair: several blank lines", "ZA3051", "a = 1\n\n\n\nb = 2\n", "a = 1\nb = 2\n"),
    case("pair: a comment between keeps the blank line", "ZA3051", "a = 1\n\n# note\nb = 2\n"),
    case("pair: a trailing comment keeps the blank line", "ZA3051", "a = 1  # note\n\nb = 2\n"),
    case("pair: a multi-line unit keeps the blank line", "ZA3051", "a = foo(\n    1\n)\n\nb = 2\n"),
    case("pair: three units are no pair", "ZA3051", "a = 1\n\nb = 2\nc = 3\n"),
    case("pair: an import keeps the blank line", "ZA3051", "import os\n\nx = 1\n"),
    case("pair: a function keeps the blank line", "ZA3051", "def f(): ...\n\n\nx = 1\n"),
    case("pair: the module docstring keeps the blank line", "ZA3051", '"""Doc."""\n\nx = 1\n'),
    case("pair: the class docstring keeps the blank line", "ZA3051", 'class A:\n    """Doc."""\n\n    x = 1\n'),
    case(
        "pair: a function docstring loses it",
        "ZA3051",
        'def f():\n    """Doc."""\n\n    return 1\n',
        'def f():\n    """Doc."""\n    return 1\n',
    ),
    case(
        "pair: a function docstring is no unit of the two statements below it",
        "ZA3051",
        'def f():\n    """Doc."""\n    x = 1\n\n    return x\n',
        'def f():\n    """Doc."""\n    x = 1\n    return x\n',
    ),
    case(
        "pair: the line below a function docstring stays when two statements follow",
        "ZA3051",
        'def f():\n    """Doc."""\n\n    x = 1\n\n    return x\n',
        'def f():\n    """Doc."""\n\n    x = 1\n    return x\n',
    ),
    case(
        "pair: a multi-line function docstring is no unit either",
        "ZA3051",
        'async def f():\n    """Doc.\n\n    More.\n    """\n    x = await g()\n\n    return x\n',
        'async def f():\n    """Doc.\n\n    More.\n    """\n    x = await g()\n    return x\n',
    ),
    case(
        "pair: a method docstring is no unit",
        "ZA3051",
        'class A:\n    def f(self):\n        """Doc."""\n        x = 1\n\n        return x\n',
        'class A:\n    def f(self):\n        """Doc."""\n        x = 1\n        return x\n',
    ),
    case(
        "pair: a comment between the statements below a docstring keeps the blank line",
        "ZA3051",
        'def f():\n    """Doc."""\n    x = 1\n\n    # note\n    return x\n',
    ),
    case(
        "pair: a multi-line statement below a docstring keeps the blank line",
        "ZA3051",
        'def f():\n    """Doc."""\n    x = 1\n\n    return g(\n        x\n    )\n',
    ),
    case(
        "pair: a function below a docstring keeps the blank line",
        "ZA3051",
        'def f():\n    """Doc."""\n    x = 1\n\n    def g(): ...\n',
    ),
    case(
        "pair: an import below a docstring keeps the blank line",
        "ZA3051",
        'def f():\n    """Doc."""\n    import os\n\n    return os\n',
    ),
    case(
        "pair: three statements below a docstring are no pair",
        "ZA3051",
        'def f():\n    """Doc."""\n    x = 1\n\n    y = 2\n    return x\n',
    ),
    case(
        "pair: a second string below a docstring is a statement",
        "ZA3051",
        'def f():\n    """Doc."""\n    """Not a doc."""\n\n    return 1\n',
        'def f():\n    """Doc."""\n    """Not a doc."""\n    return 1\n',
    ),
    case(
        "pair: the class docstring keeps the blank line when two statements follow",
        "ZA3051",
        'class A:\n    """Doc."""\n\n    x = 1\n\n    y = 2\n',
    ),
    case(
        "pair: the module docstring keeps the blank line when two statements follow",
        "ZA3051",
        '"""Doc."""\n\nx = 1\n\ny = 2\n',
    ),
    case("pair: a shebang keeps the blank line", "ZA3051", "#!/usr/bin/env python\n\nx = 1\n"),
    case(
        "pair: a match case body",
        "ZA3051",
        "match v:\n    case 1:\n        a()\n\n        b()\n",
        "match v:\n    case 1:\n        a()\n        b()\n",
    ),
    case(
        "pair: in an else block",
        "ZA3051",
        "if a:\n    pass\nelse:\n    b = 1\n\n    c = 2\n",
        "if a:\n    pass\nelse:\n    b = 1\n    c = 2\n",
    ),
    case("pair: joined statements", "ZA3051", "a = 1; b = 2\n"),
    # ZA3061 keep-assertion-with-binding
    case(
        "binding assert: the assert checks the name just bound",
        "ZA3061",
        "def f():\n    a()\n    x = load()\n\n    assert x is not None\n    use(x)\n",
        "def f():\n    a()\n    x = load()\n    assert x is not None\n    use(x)\n",
    ),
    case(
        "binding assert: a multi-line binding",
        "ZA3061",
        "def f():\n    a()\n    x = load(\n        1,\n    )\n\n    assert x\n    use(x)\n",
        "def f():\n    a()\n    x = load(\n        1,\n    )\n    assert x\n    use(x)\n",
    ),
    case(
        "binding assert: an annotated binding and an isinstance check",
        "ZA3061",
        "def f():\n    a()\n    x: object = load()\n\n    assert isinstance(x, int)\n    use(x)\n",
        "def f():\n    a()\n    x: object = load()\n    assert isinstance(x, int)\n    use(x)\n",
    ),
    case(
        "binding assert: one name of a tuple target",
        "ZA3061",
        "def f():\n    a()\n    x, y = load()\n\n    assert y\n    use(x)\n",
        "def f():\n    a()\n    x, y = load()\n    assert y\n    use(x)\n",
    ),
    case(
        "binding assert: an assert of another name",
        "ZA3061",
        "def f():\n    a()\n    x = load()\n\n    assert y\n    use(x)\n",
    ),
    case(
        "binding assert: a none check joined by and",
        "ZA3061",
        "def f():\n    a()\n    x = load()\n\n    assert x is not None and y\n    use(x)\n",
        "def f():\n    a()\n    x = load()\n    assert x is not None and y\n    use(x)\n",
    ),
    case(
        "binding assert: an assert that leads a run of assertions stays with the run",
        "ZA3061",
        "def test_f():\n    a()\n    product = get()\n\n    assert product is not None\n    assert product.id == 1\n",
    ),
    case(
        "binding assert: an assertion helper below also makes a run",
        "ZA3061",
        "def test_f():\n    a()\n    node = get()\n\n    assert node is not None\n    assert_tls(node)\n",
    ),
    case(
        "binding assert: an assert about an attribute is the assert phase of a test",
        "ZA3061",
        "def test_f():\n    a()\n    response = get()\n\n    assert response.status == 200\n    stop()\n",
    ),
    case(
        "binding assert: a comparison with a value narrows nothing",
        "ZA3061",
        "def test_f():\n    a()\n    total = add(1, 2)\n\n    assert total == 3\n    stop()\n",
    ),
    case(
        "binding assert: an attribute target binds no name",
        "ZA3061",
        "def f():\n    a()\n    self.x = load()\n\n    assert x\n    use(x)\n",
    ),
    case(
        "binding assert: a comment keeps the blank line",
        "ZA3061",
        "def f():\n    a()\n    x = load()\n\n    # sanity\n    assert x\n    use(x)\n",
    ),
    case(
        "binding assert: a multi-line assert keeps the blank line",
        "ZA3061",
        "def f():\n    a()\n    x = load()\n\n    assert x, (\n        'msg'\n    )\n    use(x)\n",
    ),
    case(
        "binding assert: an annotation without a value binds nothing",
        "ZA3061",
        "def f():\n    a()\n    x: int\n\n    assert x\n    use(x)\n",
    ),
    case(
        "binding assert: an assert that does not follow the binding",
        "ZA3061",
        "def f():\n    x = load()\n    use(x)\n\n    assert x\n    stop()\n",
    ),
    # ZA3057 keep-guard-clauses-together
    case(
        "guard chain: two if guards",
        "ZA3057",
        "def f(a, b):\n    if a is None:\n        raise ValueError\n\n    if b is None:\n        return\n    use(a)\n",
        "def f(a, b):\n    if a is None:\n        raise ValueError\n    if b is None:\n        return\n    use(a)\n",
    ),
    case(
        "guard chain: a chain in the middle of a body",
        "ZA3057",
        "def f(a):\n    x = load(a)\n\n    if x is None:\n        return\n\n    if not x.items:\n        raise E\n\n    use(x)\n",
        "def f(a):\n    x = load(a)\n\n    if x is None:\n        return\n    if not x.items:\n        raise E\n\n    use(x)\n",
    ),
    case(
        "guard chain: an assert and an if guard",
        "ZA3057",
        "for x in y:\n    assert x\n\n    if x.skip:\n        continue\n    use(x)\n",
        "for x in y:\n    assert x\n    if x.skip:\n        continue\n    use(x)\n",
    ),
    case(
        "guard chain: helper guards",
        "ZA3057",
        "def f(a):\n    check(a)\n\n    check(a.b)\n    use(a)\n",
        "def f(a):\n    check(a)\n    check(a.b)\n    use(a)\n",
        Options(guard_roots=("check",)),
    ),
    case(
        "guard chain: an if with an else is no guard",
        "ZA3057",
        "def f(a):\n    if a:\n        return 1\n    else:\n        return 2\n\n    if a:\n        return\n    x()\n",
    ),
    case(
        "guard chain: an if that does more than jump is no guard",
        "ZA3057",
        "def f(a):\n    if a:\n        log(a)\n        return\n\n    if a.b:\n        return\n    x()\n",
    ),
    case(
        "guard chain: a comment keeps the blank line",
        "ZA3057",
        "def f(a):\n    if a:\n        return\n\n    # second\n    if a.b:\n        return\n    x()\n",
    ),
    case("guard chain: a module is no block", "ZA3057", "if a:\n    raise E\n\nif b:\n    raise E\nuse(a)\n"),
    # ZA3060 keep-jump-with-binding
    case(
        "binding jump: a lone binding and the return that uses it",
        "ZA3060",
        "def f():\n    a()\n\n    x = load()\n\n    return x.id\n",
        "def f():\n    a()\n\n    x = load()\n    return x.id\n",
    ),
    case(
        "binding jump: a raise of the bound error",
        "ZA3060",
        "def f():\n    a()\n\n    error = Error(1)\n\n    raise error\n",
        "def f():\n    a()\n\n    error = Error(1)\n    raise error\n",
    ),
    case(
        "binding jump: a raise from the bound cause",
        "ZA3060",
        "def f():\n    a()\n\n    cause = find()\n\n    raise Error from cause\n",
        "def f():\n    a()\n\n    cause = find()\n    raise Error from cause\n",
    ),
    case(
        "binding jump: a binding inside a stanza keeps the blank line",
        "ZA3060",
        "def f():\n    a()\n    x = load()\n\n    return x\n",
    ),
    case(
        "binding jump: a return of another name",
        "ZA3060",
        "def f():\n    a()\n\n    x = load()\n\n    return y\n",
    ),
    case(
        "binding jump: a bare return",
        "ZA3060",
        "def f():\n    a()\n\n    x = load()\n\n    return\n",
    ),
    case(
        "binding jump: a multi-line return keeps the blank line",
        "ZA3060",
        "def f():\n    a()\n\n    x = load()\n\n    return wrap(\n        x,\n    )\n",
    ),
    case(
        "binding jump: a multi-line binding keeps the blank line",
        "ZA3060",
        "def f():\n    a()\n\n    x = load(\n        1,\n    )\n\n    return x\n",
    ),
    case(
        "binding jump: a comment keeps the blank line",
        "ZA3060",
        "def f():\n    a()\n\n    x = load()\n\n    # done\n    return x\n",
    ),
    # ZA3058 keep-guard-with-binding
    case(
        "binding guard: the guard tests the name just bound",
        "ZA3058",
        "def f(i):\n    a()\n    customer = load(i)\n\n    if not customer:\n        raise E\n    use(customer)\n",
        "def f(i):\n    a()\n    customer = load(i)\n    if not customer:\n        raise E\n    use(customer)\n",
    ),
    case(
        "binding guard: a multi-line binding and a guard that reads one name of many",
        "ZA3058",
        "def f():\n    a()\n    x, y = load(\n        1,\n    )\n\n    if y > 3:\n        return\n    use(x)\n",
        "def f():\n    a()\n    x, y = load(\n        1,\n    )\n    if y > 3:\n        return\n    use(x)\n",
    ),
    case(
        "binding guard: a guard of another name",
        "ZA3058",
        "def f(i):\n    a()\n    customer = load(i)\n\n    if not i:\n        raise E\n    use(customer)\n",
    ),
    case(
        "binding guard: an if that does more than jump is no guard",
        "ZA3058",
        "def f(i):\n    a()\n    x = load(i)\n\n    if not x:\n        log(i)\n        raise E\n    use(x)\n",
    ),
    case(
        "binding guard: an assert belongs to ZA3061",
        "ZA3058",
        "def f(i):\n    a()\n    x = load(i)\n\n    assert x\n    use(x)\n",
    ),
    case(
        "binding guard: a comment keeps the blank line",
        "ZA3058",
        "def f(i):\n    a()\n    x = load(i)\n\n    # missing\n    if not x:\n        raise E\n    use(x)\n",
    ),
    case(
        "binding guard: a module is no block",
        "ZA3058",
        "a()\nx = load()\n\nif not x:\n    raise E\nuse(x)\n",
    ),
    # ZA3059 keep-accumulator-with-loop
    case(
        "accumulator: a total that the loop augments",
        "ZA3059",
        'def f(items):\n    a()\n    total = Decimal("0.00")\n\n    for item in items:\n        total += item.price\n    return total\n',
        'def f(items):\n    a()\n    total = Decimal("0.00")\n    for item in items:\n        total += item.price\n    return total\n',
    ),
    case(
        "accumulator: a list that the loop appends to",
        "ZA3059",
        "def f(xs):\n    a()\n    found = []\n\n    for x in xs:\n        if x:\n            found.append(x)\n    return found\n",
        "def f(xs):\n    a()\n    found = []\n    for x in xs:\n        if x:\n            found.append(x)\n    return found\n",
    ),
    case(
        "accumulator: a search result that the loop assigns",
        "ZA3059",
        "def f(xs):\n    a()\n    match = None\n\n    while xs:\n        match = xs.pop()\n    return match\n",
        "def f(xs):\n    a()\n    match = None\n    while xs:\n        match = xs.pop()\n    return match\n",
    ),
    case(
        "accumulator: a method that only uses the object builds nothing",
        "ZA3059",
        "def f(xs):\n    a()\n    client = Client()\n\n    for x in xs:\n        client.send(x)\n    return client\n",
    ),
    case(
        "accumulator: a seed that reads a variable is no seed",
        "ZA3059",
        "def f(xs, n):\n    a()\n    total = n\n\n    for x in xs:\n        total += x\n    return total\n",
    ),
    case(
        "accumulator: a loop that does not build the name",
        "ZA3059",
        "def f(xs):\n    a()\n    total = 0\n\n    for x in xs:\n        use(x)\n    return total\n",
    ),
    case(
        "accumulator: a comment keeps the blank line",
        "ZA3059",
        "def f(xs):\n    a()\n    total = 0\n\n    # sum\n    for x in xs:\n        total += x\n    return total\n",
    ),
    # ZA3018 separate-checked-stanzas
    case(
        "checked binding: a guarded binding below another statement",
        "ZA3018",
        "async def f(s, i):\n    service = Service(s)\n    cart = await service.get(i)\n    if cart is None:\n        return None\n    use(cart)\n",
        "async def f(s, i):\n    service = Service(s)\n\n    cart = await service.get(i)\n    if cart is None:\n        return None\n\n    use(cart)\n",
    ),
    case(
        "checked binding: a group of bindings the guard reads",
        "ZA3018",
        "def f():\n    log()\n    a = load()\n    b = load()\n    if a is None or b is None:\n        return\n    use(a, b)\n",
        "def f():\n    log()\n\n    a = load()\n    b = load()\n    if a is None or b is None:\n        return\n\n    use(a, b)\n",
    ),
    case(
        "checked binding: a binding the guard does not read stays above the group",
        "ZA3018",
        "def f():\n    a = load()\n    b = load()\n    cart = load()\n    if cart is None:\n        return\n    use(a, b)\n",
        "def f():\n    a = load()\n    b = load()\n\n    cart = load()\n    if cart is None:\n        return\n\n    use(a, b)\n",
    ),
    case(
        "checked binding: a narrowing assert",
        "ZA3018",
        "def f():\n    log()\n    event = find()\n    assert event is not None\n    use(event)\n",
        "def f():\n    log()\n\n    event = find()\n    assert event is not None\n    use(event)\n",
    ),
    case(
        "checked binding: an assert that leads a run of assertions checks nothing",
        "ZA3018",
        "def test_f():\n    log()\n    product = get()\n    assert product is not None\n    assert product.id == 1\n",
    ),
    case(
        "checked binding: a group that opens the body",
        "ZA3018",
        "def f(i):\n    cart = load(i)\n    if cart is None:\n        return\n    use(cart)\n",
        "def f(i):\n    cart = load(i)\n    if cart is None:\n        return\n\n    use(cart)\n",
    ),
    case(
        "checked binding: a guard that reads no binding above it",
        "ZA3018",
        "def f(i):\n    log()\n    cart = load(i)\n    if i is None:\n        return\n    use(cart)\n",
        "def f(i):\n    log()\n    cart = load(i)\n\n    if i is None:\n        return\n\n    use(cart)\n",
    ),
    case(
        "checked binding: a comment above the binding gets the blank line above it",
        "ZA3018",
        "def f(i):\n    log()\n    # the cart\n    cart = load(i)\n    if cart is None:\n        return\n    use(cart)\n",
        "def f(i):\n    log()\n\n    # the cart\n    cart = load(i)\n    if cart is None:\n        return\n\n    use(cart)\n",
    ),
]

# The relationship keepers win the gaps that the separating rules ask for.
CASES += [
    Case(
        "checked binding: with the keep rules and the run rules, the checked step stands on its own",
        ("ZA3018", "ZA3058", "ZA3057", "ZA3001", "ZA3002", "ZA3011", "ZA3054"),
        "async def f(s, i):\n    service = Service(s)\n    cart = await service.get(i)\n\n    if cart is None:\n        return None\n\n    if not cart.items:\n        raise E\n\n    use(cart)\n",
        "async def f(s, i):\n    service = Service(s)\n\n    cart = await service.get(i)\n    if cart is None:\n        return None\n    if not cart.items:\n        raise E\n\n    use(cart)\n",
    ),
    Case(
        "binding guard: wins over the multi-line rule and joins the guard chain below",
        ("ZA3058", "ZA3057", "ZA3001", "ZA3011"),
        "def f(i):\n    a()\n    cart = load(i)\n\n    if cart is None:\n        return\n\n    if not cart.items:\n        raise E\n\n    use(cart)\n",
        "def f(i):\n    a()\n\n    cart = load(i)\n    if cart is None:\n        return\n    if not cart.items:\n        raise E\n\n    use(cart)\n",
    ),
    Case(
        "binding jump: wins over the declaration run",
        ("ZA3060", "ZA3002"),
        "def f():\n    a = 1\n\n    b = load(a)\n\n    return b\n",
        "def f():\n    a = 1\n\n    b = load(a)\n    return b\n",
    ),
    Case(
        "binding jump: a binding that a kept run joins to the stanza above is not lone",
        ("ZA3060", "ZA3002", "ZA3009", "ZA3054"),
        "def f():\n    a = 1\n\n    b = load(a)\n\n    return b\n",
        "def f():\n    a = 1\n    b = load(a)\n\n    return b\n",
    ),
    Case(
        "binding assert: wins over the declaration run",
        ("ZA3061", "ZA3001", "ZA3002"),
        "def f():\n    a = publish(\n        1,\n    )\n\n    b = load(a)\n\n    assert b is not None\n\n    return b\n",
        "def f():\n    a = publish(\n        1,\n    )\n\n    b = load(a)\n    assert b is not None\n\n    return b\n",
    ),
    Case(
        "guard chain: wins over the multi-line rule and keeps the gap below the guards",
        ("ZA3057", "ZA3001", "ZA3011"),
        "def f(a, b):\n    if a is None:\n        raise ValueError\n\n    if b is None:\n        raise ValueError\n    use(a)\n    use(b)\n",
        "def f(a, b):\n    if a is None:\n        raise ValueError\n    if b is None:\n        raise ValueError\n\n    use(a)\n    use(b)\n",
    ),
]


@pytest.mark.parametrize("item", CASES, ids=lambda item: item.id)
def test_rule_case(item: Case) -> None:
    check_case(item)


@pytest.mark.parametrize(
    "directive", ["# type: ignore", "# noqa", "# pragma: no cover", "# fmt: skip", "# yapf: disable"]
)
def test_a_tool_directive_leads_no_group_for_za3012(directive: str) -> None:
    text = f"a = 1\n{directive}\nb = 2\n"
    assert fix_with(text, "ZA3012") == text
