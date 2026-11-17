"""Binding consumers distinguish outer value reads from expression-local names and writes."""

from __future__ import annotations

import pytest

from nickelony_stanza import Config, Options, fix, lint

READS = [
    ("x", True),
    ("lambda x: x", False),
    ("lambda y: x", True),
    ("lambda x=x: x", True),
    ("lambda *, x=x: x", True),
    ("lambda x=0: x", False),
    ("lambda *x: x", False),
    ("lambda **x: x", False),
    ("lambda x, /: x", False),
    ("lambda: (x := 1)", False),
    ("lambda: ((x := 1), x)", False),
    ("lambda: ((x := x + 1), x)", False),
    ("(x := 1)", False),
    ("(x := x + 1)", True),
    ("[x for x in values]", False),
    ("[value for value in x]", True),
    ("[x for value in values]", True),
    ("{x for x in values}", False),
    ("{x: x for x in values}", False),
    ("(x for x in values)", False),
    ("sum(x for x in values)", False),
    ("sum(x + value for value in values)", True),
    ("[x for x in x]", True),
    ("[x for y in values for x in y]", False),
    ("[x for y in values for x in x]", False),
    ("[x for x in values if predicate(x)]", False),
    ("lambda: [x for x in values]", False),
    ("lambda: [y for y in x]", True),
    ("(lambda x: (lambda: x))(0)", False),
    ("(lambda y: (lambda: x))(0)", True),
    ("[(lambda: x) for x in values]", False),
    ("[(lambda x=x: x) for x in values]", False),
    ("[(lambda x=x: x) for y in values]", True),
    ("[(x := value) for value in values]", False),
    ("lambda: (lambda y=(x := 1): x)", False),
    ("lambda: (lambda y=x: y)", True),
    ("lambda x: (lambda y=x: y)", False),
    ("lambda: ([x for x in values], x)", True),
    ("lambda: ([(x := value) for value in values], x)", False),
    ("[(x := x + value) for value in values]", True),
    ("{value: x for value in values}", True),
    ("[value for value in values if x]", True),
    ("[(x, value) for x, *value in values]", False),
    ("[value for value in values for other in x]", True),
    ("[value for value in values for x in x]", False),
    ("[1 for x.value in values]", True),
    ("[1 for slots[x] in values]", True),
    ("[1 for slots[lookup(x)] in values]", True),
    ("[1 for (x.value, value) in values]", True),
    ("[1 for (value, slots[x]) in values]", True),
    ("[1 for (x, slots[x]) in values]", False),
    ("[1 for (x, x.value) in values]", False),
    ("[1 for x in values for x.value in values]", False),
    ("[1 for item in values for slots[x] in item]", True),
    ("[1 for slots[(lambda x: x)(0)] in values]", False),
    ("[1 for slots[(lambda: x)()] in values]", True),
    ("[1 for slots[(lambda x=x: x)()] in values]", True),
    ("{1 for x.value in values}", True),
    ("{1: 2 for slots[x] in values}", True),
    ("(1 for x.value in values)", True),
]


@pytest.mark.parametrize(("expression", "reads"), READS)
@pytest.mark.parametrize("consumer", ["guard", "jump", "raise", "cause", "checked-if", "checked-helper"])
def test_consumers_use_the_preceding_binding_only_for_an_outer_value_read(
    expression: str, reads: bool, consumer: str
) -> None:
    if consumer in {"jump", "raise", "cause"}:
        jump = {
            "jump": f"return {expression}",
            "raise": f"raise Error({expression})",
            "cause": f"raise Error() from cause({expression})",
        }

        source = f"def f(values):\n    x = load()\n\n    {jump[consumer]}\n"
        expected = source.replace("load()\n\n", "load()\n") if reads else source
        codes = ["ZA3059"]
    elif consumer == "guard":
        source = f"def f(values):\n    x = load()\n\n    if predicate({expression}):\n        return\n    use(x)\n"
        expected = source.replace("load()\n\n", "load()\n") if reads else source
        codes = ["ZA3058"]
    else:
        check = f"if predicate({expression}):\n        return" if consumer == "checked-if" else f"check({expression})"
        source = f"def f(values):\n    audit()\n    x = load()\n    {check}\n    use(x)\n"
        expected = source.replace("audit()\n", "audit()\n\n") if reads else source.replace("load()\n", "load()\n\n")
        expected = expected.replace("    use(x)", "\n    use(x)")
        codes = ["ZA3012"]

    config = Config.create("none", enable=codes, options=Options(guard_roots=("check",)))

    compile(source, "consumer.py", "exec")
    result = fix(source, config)

    assert result.text == expected
    assert result.passes <= 1
    assert lint(expected, config) == []


@pytest.mark.parametrize("consumer", ["guard", "jump"])
@pytest.mark.parametrize("barrier", ["# comment\n", "# noqa: E501\n", "# fmt: off\n# fmt: on\n"])
def test_outer_captures_do_not_cross_comment_or_directive_barriers(consumer: str, barrier: str) -> None:
    tail = "return lambda: x" if consumer == "jump" else "if predicate(lambda: x):\n    return\nuse(x)"
    body = "x = load()\n\n" + barrier + tail
    source = "def f():\n" + "".join("    " + line + "\n" if line else "\n" for line in body.splitlines())
    config = Config.create("none", enable=["ZA3059" if consumer == "jump" else "ZA3058"])

    assert fix(source, config).text == source
    assert lint(source, config) == []
