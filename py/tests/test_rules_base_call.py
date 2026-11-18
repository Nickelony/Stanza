"""One case for each behavior of the rule that separates the call to the base class from its neighbors."""

from __future__ import annotations

import pytest

from tests.helpers import Case, case, check_case

CASES = [
    # ZA3014 separate-base-call
    case(
        "base call: the call that opens a body",
        "ZA3014",
        "class A(B):\n    def f(self):\n        super().f()\n        x = 1\n        y = 2\n",
        "class A(B):\n    def f(self):\n        super().f()\n\n        x = 1\n        y = 2\n",
    ),
    case(
        "base call: the call that closes a body",
        "ZA3014",
        "class A(B):\n    def f(self):\n        x = 1\n        y = 2\n        super().f()\n",
        "class A(B):\n    def f(self):\n        x = 1\n        y = 2\n\n        super().f()\n",
    ),
    case(
        "base call: an opening and a closing call",
        "ZA3014",
        "class A(B):\n    def f(self):\n        super().a()\n        x = 1\n        y = 2\n        super().b()\n",
        "class A(B):\n    def f(self):\n        super().a()\n\n        x = 1\n        y = 2\n\n        super().b()\n",
    ),
    case(
        "base call: two base calls in a row",
        "ZA3014",
        "class A(B):\n    def f(self):\n        super().a()\n        super().b()\n        x = 1\n",
        "class A(B):\n    def f(self):\n        super().a()\n\n        super().b()\n        x = 1\n",
    ),
    case(
        "base call: the opening call below the docstring and the guards",
        "ZA3014",
        'class A(B):\n    def f(self, a):\n        """Doc."""\n        if a is None:\n            return\n        super().f(a)\n        x = 1\n        y = 2\n',
        'class A(B):\n    def f(self, a):\n        """Doc."""\n        if a is None:\n            return\n        super().f(a)\n\n        x = 1\n        y = 2\n',
    ),
    case(
        "base call: an awaited base call",
        "ZA3014",
        "class A(B):\n    async def f(self):\n        await super().f()\n        x = 1\n        y = 2\n",
        "class A(B):\n    async def f(self):\n        await super().f()\n\n        x = 1\n        y = 2\n",
    ),
    case(
        "base call: a chain that starts at super",
        "ZA3014",
        "class A(B):\n    def f(self):\n        super().items.clear()\n        x = 1\n        y = 2\n",
        "class A(B):\n    def f(self):\n        super().items.clear()\n\n        x = 1\n        y = 2\n",
    ),
    case(
        "base call: a function docstring keeps the line below it",
        "ZA3014",
        'class A(B):\n    def f(self):\n        """Doc."""\n        super().f()\n        x = 1\n        y = 2\n',
        'class A(B):\n    def f(self):\n        """Doc."""\n        super().f()\n\n        x = 1\n        y = 2\n',
    ),
    case(
        "base call: a base call in the middle",
        "ZA3014",
        "class A(B):\n    def f(self):\n        x = 1\n        super().f()\n        y = 2\n        z = 3\n",
    ),
    case(
        "base call: a returned base call is a jump",
        "ZA3014",
        "class A(B):\n    def f(self):\n        x = 1\n        y = 2\n        return super().f()\n",
    ),
    case(
        "base call: a call that passes super as an argument",
        "ZA3014",
        "class A(B):\n    def f(self):\n        use(super())\n        x = 1\n        y = 2\n",
    ),
    case(
        "base call: an explicit parent call is no base call",
        "ZA3014",
        "class A(B):\n    def f(self):\n        B.f(self)\n        x = 1\n        y = 2\n",
    ),
    case(
        "base call: the body of a block is left alone",
        "ZA3014",
        "class A(B):\n    def f(self, a):\n        if a:\n            super().f()\n            x = 1\n            y = 2\n",
    ),
    case(
        "base call: a body of two statements belongs to the pair rule",
        "ZA3014",
        "class A(B):\n    def f(self):\n        super().f()\n        x = 1\n",
    ),
]


@pytest.mark.parametrize("item", CASES, ids=lambda item: item.id)
def test_rule_case(item: Case) -> None:
    check_case(item)
