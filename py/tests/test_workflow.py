"""The workflow runs on the files the tests read and runs every check the development notes list."""

from __future__ import annotations

import re
from pathlib import Path

import pytest

ROOT = Path(__file__).parent.parent
WORKFLOW = (ROOT.parent / ".github" / "workflows" / "py.yml").read_text(encoding="utf-8").splitlines()
EXPECTED_PATHS = ["py/**", "docs/rules/**", "js/docs/rules/**", ".github/workflows/py.yml"]


def _path_filter(event: str) -> list[str]:
    block = WORKFLOW[WORKFLOW.index(f"  {event}:") + 1 :]
    end = next(index for index, line in enumerate(block) if re.match(r" {0,2}\S", line))

    return [match.group(1) for line in block[:end] if (match := re.fullmatch(r" {6}- '([^']+)'", line))]


@pytest.mark.parametrize("event", ["push", "pull_request"])
def test_the_workflow_filters_on_the_files_the_tests_read(event: str) -> None:
    assert _path_filter(event) == EXPECTED_PATHS


def test_the_workflow_runs_the_checks_of_the_readme() -> None:
    commands = {line.strip().removeprefix("run: ") for line in WORKFLOW if line.strip().startswith("run: ")}
    development = (ROOT / "README.md").read_text(encoding="utf-8").split("## Development")[1]

    for command in ("pytest", "ruff check .", "ruff format --check .", "mypy", "stanza-py check src tests"):
        assert command in development
        assert command in commands


def test_the_matrix_starts_at_the_oldest_supported_python() -> None:
    pyproject = (ROOT / "pyproject.toml").read_text(encoding="utf-8")
    oldest = re.search(r'requires-python = ">=([\d.]+)"', pyproject)
    versions = re.search(r"python-version: \[(.+)\]", "\n".join(WORKFLOW))

    assert oldest is not None
    assert versions is not None
    assert re.findall(r"'([\d.]+)'", versions.group(1))[0] == oldest.group(1)
