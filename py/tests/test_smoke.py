"""Runs the strong preset, and the strong preset with every opt-in rule, over real files: no crash, nothing left to fix, and the syntax tree stays.

`STANZA_SMOKE_LIMIT` sets how many files to read, and 0 reads every file of the standard library and the environment.
"""

from __future__ import annotations

import os
import sysconfig
from pathlib import Path

import pytest

from nickelony_stanza import Config, fix
from nickelony_stanza.analysis.document import read_document
from tests.helpers import CONFIGS, same_tree

DEFAULT_LIMIT = 150


def _sample() -> list[Path]:
    roots = [Path(sysconfig.get_paths()["stdlib"]), Path(sysconfig.get_paths()["purelib"])]
    files = sorted(path for root in roots for path in root.rglob("*.py") if "lib2to3" not in path.parts)
    limit = int(os.environ.get("STANZA_SMOKE_LIMIT", DEFAULT_LIMIT))

    return files if limit == 0 else files[:: max(1, len(files) // limit)][:limit]


@pytest.mark.parametrize("name", ["strong", "everything", "everything separating", "everything both halves"])
def test_real_files_settle_and_keep_their_syntax_tree(name: str) -> None:
    config = CONFIGS[name]
    parsed = 0

    for path in _sample():
        try:
            text = read_document(path.read_bytes()).text
            compile(text, str(path), "exec", dont_inherit=True)
        except (SyntaxError, ValueError, UnicodeDecodeError):
            continue

        parsed += 1
        result = fix(text, config)

        assert fix(result.text, config).passes == 0, path
        assert same_tree(text, result.text, config), path

    assert parsed > 0


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ("a = 1\rb = 2\rrun()\r", "a = 1\rb = 2\r\rrun()\r"),
        ("a = 1\r\nb = 2\r\nrun()\r\n", "a = 1\r\nb = 2\r\n\r\nrun()\r\n"),
        ("a = 1\nb = 2\r\nrun()\n", "a = 1\nb = 2\r\n\r\nrun()\n"),
    ],
)
def test_every_kind_of_line_ending_is_kept(text: str, expected: str) -> None:
    assert fix(text, Config.create("strong")).text == expected
