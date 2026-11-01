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
