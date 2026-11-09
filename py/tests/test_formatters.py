"""
Black and Ruff leave a fixed file as it is, and Stanza leaves the output of either as it is.

A name a call rule writes can push a line past the formatter's width, so those rules are checked through the two rounds
of `format` instead.
"""

from __future__ import annotations

from pathlib import Path

import pytest

from nickelony_stanza import fix
from nickelony_stanza.analysis.document import read_document
from nickelony_stanza.formatter import Formatter
from nickelony_stanza.pipeline import run_pipeline
from tests.helpers import (
    CONFIGS,
    add_blank_lines,
    corpus_files,
    drop_blank_lines,
    run_formatter,
    without_call_rules,
)

TOOLS = ["black", "ruff"]


def _mutants(text: str) -> list[str]:
    return [text, drop_blank_lines(text, 7), drop_blank_lines(text, 11), add_blank_lines(text, 3)]


@pytest.mark.parametrize("name", CONFIGS)
@pytest.mark.parametrize("tool", TOOLS)
@pytest.mark.parametrize("path", corpus_files(), ids=lambda path: path.name)
def test_a_formatted_file_stays_formatted_after_the_fix(tool: str, name: str, path: Path) -> None:
    config = without_call_rules(CONFIGS[name])
    text = path.read_text(encoding="utf-8")

    for mutant in _mutants(text):
        formatted = run_formatter(tool, mutant)
        assert formatted is not None

        fixed = fix(formatted, config).text

        assert run_formatter(tool, fixed) == fixed, f"{tool} rewrites what Stanza wrote"


@pytest.mark.parametrize("tool", TOOLS)
@pytest.mark.parametrize("path", corpus_files(), ids=lambda path: path.name)
def test_stanza_leaves_the_output_of_the_formatter_alone_after_a_fix(tool: str, path: Path) -> None:
    config = without_call_rules(CONFIGS["strong"])
    text = path.read_text(encoding="utf-8")

    for mutant in _mutants(text):
        fixed = fix(mutant, config).text
        formatted = run_formatter(tool, fixed)

        assert formatted is not None
        assert fix(formatted, config).text == formatted, f"Stanza rewrites the output of {tool}"


class _Tool(Formatter):
    """The formatter of the tests, with the settings `run_formatter` gives it."""

    def format(self, text: bytes, filename: str) -> bytes:
        formatted = run_formatter(self.name, text.decode())
        assert formatted is not None

        return formatted.encode()


@pytest.mark.parametrize("name", ["strong", "everything"])
@pytest.mark.parametrize("tool", TOOLS)
@pytest.mark.parametrize("path", corpus_files(), ids=lambda path: path.name)
def test_the_format_command_settles_a_file_whose_calls_the_rules_name(tool: str, name: str, path: Path) -> None:
    config = CONFIGS[name]

    for mutant in _mutants(path.read_text(encoding="utf-8")):
        result = run_pipeline(read_document(mutant.encode()), config, _Tool(tool, ()), path.name)

        assert result.is_settled, f"{tool} rewrites what Stanza wrote in the second round"
        assert fix(result.text, config).text == result.text


@pytest.mark.parametrize("tool", TOOLS)
@pytest.mark.parametrize(
    "snippet",
    [
        "x = 1\n{g}def f():\n    pass\n",
        "def f():\n    pass\n{g}x = 1\n",
        "import os\n{g}x = 1\n",
        '"""Doc."""\n{g}x = 1\ny = 2\n',
        'class A:\n    """Doc."""\n{g}    x = 1\n    y = 2\n',
        "class A:\n    x = 1\n{g}    def f(self):\n        pass\n",
        "def g():\n    def f():\n        pass\n{g}    y = 2\n    z = 3\n",
        "if a:\n    def f():\n        pass\ny = 2\nz = 3\n",
        "def f():\n    import os\n{g}    return os\n",
        'def f():\n    """Doc."""\n{g}    x = 1\n{g}    return x\n',
        'class A:\n    def f(self):\n        """Doc."""\n{g}        x = 1\n{g}        return x\n',
        "if a:\n    def f():\n        pass\n{g}else:\n    y = 1\n    z = 2\n",
        "try:\n    class A:\n        pass\n{g}except E:\n    y = 1\n    z = 2\n",
        "match v:\n    case 1:\n        def f():\n            pass\n{g}    case 2:\n        y = 1\n        z = 2\n",
        "match v:\n    case 1:\n        y = 1\n        z = 2\n{g}    case 2:\n        y = 1\n        z = 2\n",
        "if a:\n    y = 1\n    z = 2\n{g}else:\n    y = 1\n    z = 2\n",
        "if a:\n    y = 1\n    z = 2\n{g}# otherwise\nelse:\n    y = 1\n    z = 2\n",
        "try:\n    y = 1\n    z = 2\n{g}# fall back\n{g}except E:\n    y = 1\n    z = 2\n",
        "if a:\n    y = 1\n    z = 2\n    # trails\n{g}else:\n    y = 1\n    z = 2\n",
        "def f():\n    if a:\n        y = 1\n        z = 2\n{g}    # otherwise\n    else:\n        y = 1\n        z = 2\n",
        "class A(B):\n    def f(self):\n        super().f()\n{g}        y = 1\n        z = 2\n",
        "class A(B):\n    x: int\n{g}    y: int\n{g}    z: int\n{g}    w: int\n",
        "def f():\n{g}    x = 1\n    y = 2\n",
        "async def f():\n{g}    x = 1\n    y = 2\n",
        "class A:\n{g}    x = 1\n    y = 2\n",
        'def f():\n{g}    """Doc."""\n    x = 1\n',
        'class A:\n{g}    """Doc."""\n\n    x = 1\n',
        "def f():\n{g}    # note\n    x = 1\n    y = 2\n",
        "def f():\n    # note\n{g}    x = 1\n    y = 2\n",
        "def f():\n{g}    def g():\n        pass\n",
        "class A:\n{g}    def g(self):\n        pass\n",
        "if a:\n{g}    def g():\n        pass\n",
        "if a:\n    x = 1\nelse:\n{g}    class B:\n        pass\n",
        "with a:\n{g}    def g():\n        pass\n",
        "try:\n{g}    def g():\n        pass\nexcept E:\n{g}    y = 1\n    z = 2\n",
        "match v:\n    case 1:\n{g}        def f():\n            pass\n",
        "match v:\n{g}    case 1:\n{g}        y = 1\n        z = 2\n",
        "if a:\n{g}    # about g\n    def g():\n        pass\n",
        "for i in y:\n{g}    x = 1\nelse:\n{g}    z = 2\n",
    ],
)
@pytest.mark.parametrize("name", ["strong", "everything", "everything separating", "everything both halves"])
def test_the_gaps_a_formatter_owns_are_left_to_it(tool: str, snippet: str, name: str) -> None:
    config = CONFIGS[name]

    for gap in ("", "\n", "\n\n"):
        text = snippet.replace("{g}", gap)
        formatted = run_formatter(tool, text)

        assert formatted is not None
        assert fix(formatted, config).text == run_formatter(tool, fix(formatted, config).text)


@pytest.mark.parametrize("tool", TOOLS)
def test_a_comment_tight_under_the_block_is_the_layout_the_formatters_keep(tool: str) -> None:
    padded = "try:\n    load()\n\n# fall back to the cache\nexcept OSError:\n    use_cache()\n"
    tight = "try:\n    load()\n# fall back to the cache\nexcept OSError:\n    use_cache()\n"

    assert run_formatter(tool, padded) == padded
    assert run_formatter(tool, tight) == tight
    assert fix(padded, CONFIGS["minimal"]).text == tight


@pytest.mark.parametrize("tool", TOOLS)
@pytest.mark.parametrize(
    "header", ["def f():", "async def f():", "class A:", "if a:", "for i in y:", "while a:", "with a:"]
)
def test_the_blank_line_below_a_header_is_one_the_formatters_never_write(tool: str, header: str) -> None:
    tight = f"{header}\n    x = 1\n"
    padded = f"{header}\n\n    x = 1\n"

    assert fix(padded, CONFIGS["minimal"]).text == tight
    assert run_formatter(tool, tight) == tight
