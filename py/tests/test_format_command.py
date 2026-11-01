"""The `format` command runs the formatter, then the rules, and checks that the formatter keeps the result."""

from __future__ import annotations

import importlib.util
import io
import shutil
import sys
from pathlib import Path

import pytest

from nickelony_stanza import Config, ConfigError, fix
from nickelony_stanza.analysis.document import read_document
from nickelony_stanza.cli import run
from nickelony_stanza.formatter import DEFAULT_FORMATTER, Formatter, FormatterError, detect_formatter, find_formatter
from nickelony_stanza.pipeline import run_pipeline
from tests.helpers import run_formatter

TOOLS = ["ruff", "black"]

# The formatter joins the call onto one line, so a rule that read the multi-line call before it ran would leave padding.
JOINABLE = "a = 1\nx = call(\n    1,\n    2\n)\nb = 2\n"
JOINED = "a = 1\nx = call(1, 2)\nb = 2\n"

# A fake formatter that takes out the blank line above every `else`, as a formatter that fights ZA3201 would.
UNDO = "import sys; sys.stdout.buffer.write(sys.stdin.buffer.read().replace(b'\\n\\nelse:', b'\\nelse:'))"

# The text the rules write above an `else`, and the text without it.
SEPARATED = "if a:\n    x()\n\nelse:\n    y()\n"
TIGHT = "if a:\n    x()\nelse:\n    y()\n"

# A fake formatter that takes out that blank line on its second run only, as a formatter does when a name the fix wrote widens a line.
ONCE = (
    "import pathlib, sys\n"
    "counter = pathlib.Path(sys.argv[1])\n"
    "calls = int(counter.read_text()) + 1 if counter.exists() else 1\n"
    "counter.write_text(str(calls))\n"
    "text = sys.stdin.buffer.read()\n"
    "sys.stdout.buffer.write(text.replace(b'\\n\\nelse:', b'\\nelse:') if calls == 2 else text)\n"
)


def _require(tool: str) -> None:
    if shutil.which(tool) is None and importlib.util.find_spec(tool) is None:
        pytest.skip(f"{tool} is not installed")


def _write(directory: Path, name: str, text: str) -> Path:
    path = directory / name
    path.write_bytes(text.encode())

    return path


def _undo_formatter() -> Formatter:
    return Formatter("fake", (sys.executable, "-c", UNDO))


def _hide_formatters(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(shutil, "which", lambda name: None)
    monkeypatch.setattr(importlib.util, "find_spec", lambda name: None)


@pytest.mark.parametrize("tool", TOOLS)
def test_the_formatter_runs_before_the_rules(tool: str, tmp_path: Path) -> None:
    _require(tool)
    path = _write(tmp_path, "a.py", JOINABLE)

    assert run(["format", "--formatter", tool, "--level", "moderate", str(path)]) == 0
    assert path.read_text() == JOINED

    # The rules first and the formatter second leave padding beside a call the formatter joins.
    padded = fix(JOINABLE, Config.create("moderate")).text
    assert run_formatter(tool, padded) != JOINED


@pytest.mark.parametrize("tool", TOOLS)
def test_a_second_run_changes_nothing_and_a_check_passes(tool: str, tmp_path: Path) -> None:
    _require(tool)

    path = _write(tmp_path, "a.py", "import os\nx=1\ny = 2\ndef f(a,b):\n  if a: return b\n  return a\nprint(f(1,2))\n")
    arguments = ["--formatter", tool, "--level", "strong"]

    assert run(["format", *arguments, str(path)]) == 0

    formatted = path.read_text()
    stamp = path.stat().st_mtime_ns

    assert run_formatter(tool, formatted) == formatted
    assert fix(formatted, Config.create("strong")).text == formatted
    assert run(["format", *arguments, str(path)]) == 0
    assert path.stat().st_mtime_ns == stamp
    assert run(["format", "--check", *arguments, str(path)]) == 0


@pytest.mark.parametrize("tool", TOOLS)
def test_a_check_changes_nothing_and_exits_with_one(tool: str, tmp_path: Path) -> None:
    _require(tool)
    path = _write(tmp_path, "a.py", JOINABLE)

    assert run(["format", "--check", "--formatter", tool, "--level", "moderate", str(path)]) == 1
    assert path.read_text() == JOINABLE


def test_a_check_names_the_files_it_would_change(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    path = _write(tmp_path, "a.py", "a = 1\nb = 2\nrun()\n")

    assert run(["format", "--check", "--formatter", "none", str(path)]) == 1

    captured = capsys.readouterr()

    assert f"would format {path}" in captured.out
    assert "1 file would change, 0 unsettled." in captured.err


def test_the_none_formatter_runs_the_rules_alone(tmp_path: Path) -> None:
    path = _write(tmp_path, "a.py", "a = 1\nb = 2\nrun()\n")

    assert run(["format", "--formatter", "none", str(path)]) == 0
    assert path.read_text() == "a = 1\nb = 2\n\nrun()\n"


def test_a_formatter_that_undoes_the_fix_is_a_settle_failure(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    path = _write(tmp_path, "a.py", TIGHT)

    monkeypatch.setattr("nickelony_stanza.cli.find_formatter", lambda *args: _undo_formatter())

    assert run(["format", "--level", "none", "--enable", "ZA3201", str(path)]) == 1
    assert path.read_text() == SEPARATED

    captured = capsys.readouterr()

    assert f"{path}: unsettled: fake changes what the rules wrote" in captured.err
    assert "1 file formatted, 1 unsettled." in captured.err


def test_a_check_reports_a_settle_failure_without_writing(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    path = _write(tmp_path, "a.py", TIGHT)

    monkeypatch.setattr("nickelony_stanza.cli.find_formatter", lambda *args: _undo_formatter())

    assert run(["format", "--check", "--level", "none", "--enable", "ZA3201", str(path)]) == 1
    assert path.read_text() == TIGHT
    assert "unsettled" in capsys.readouterr().err


def test_the_pipeline_reports_a_text_the_formatter_changes_as_unsettled() -> None:
    document = read_document(TIGHT.encode())
    result = run_pipeline(document, Config.create("none", enable=["ZA3201"]), _undo_formatter(), "a.py")

    assert result.text == SEPARATED
    assert not result.is_settled


def test_the_pipeline_fixes_again_when_the_formatter_changes_the_fixed_text_and_settles_on_the_next_run(
    tmp_path: Path,
) -> None:
    counter = tmp_path / "calls.txt"
    formatter = Formatter("fake", (sys.executable, "-c", ONCE, str(counter)))
    result = run_pipeline(read_document(TIGHT.encode()), Config.create("none", enable=["ZA3201"]), formatter, "a.py")

    assert result.text == SEPARATED
    assert result.is_settled
    assert counter.read_text() == "3"


def test_a_file_name_that_starts_with_a_hyphen_reaches_the_formatter_with_a_prefix() -> None:
    echo = Formatter("ruff", (sys.executable, "-c", "import sys; sys.stdout.write(sys.argv[3])"))

    assert echo.format(b"", "-x.py") == b"./-x.py"
    assert echo.format(b"", "x.py") == b"x.py"


def test_a_formatter_that_is_missing_exits_with_two(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    path = _write(tmp_path, "a.py", JOINABLE)

    _hide_formatters(monkeypatch)

    assert run(["format", "--formatter", "black", str(path)]) == 2
    assert "black is not installed" in capsys.readouterr().err
    assert path.read_text() == JOINABLE

    with pytest.raises(FormatterError):
        find_formatter("ruff")

    assert find_formatter("none") is None


def test_a_formatter_that_refuses_a_file_is_an_error_for_that_file(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    refusing = Formatter("fake", (sys.executable, "-c", "import sys; sys.exit('cannot parse')"))
    bad = _write(tmp_path, "bad.py", "def (:\n")

    monkeypatch.setattr("nickelony_stanza.cli.find_formatter", lambda *args: refusing)

    assert run(["format", str(bad)]) == 2
    assert "cannot parse" in capsys.readouterr().err


def test_standard_input_is_formatted_to_standard_output(
    monkeypatch: pytest.MonkeyPatch, capsysbinary: pytest.CaptureFixture[bytes]
) -> None:
    class Input:
        buffer = io.BytesIO(b"a = 1\nb = 2\nrun()\n")

    monkeypatch.setattr(sys, "stdin", Input)

    assert run(["format", "--formatter", "none", "-"]) == 0
    assert capsysbinary.readouterr().out == b"a = 1\nb = 2\n\nrun()\n"


def test_the_formatter_comes_from_pyproject_and_the_flag_replaces_it(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    _write(tmp_path, "pyproject.toml", '[tool.stanza]\nformatter = "none"\n')
    path = _write(tmp_path, "a.py", "a = 1\nb = 2\nrun()\n")

    monkeypatch.chdir(tmp_path)
    _hide_formatters(monkeypatch)

    assert run(["format", str(path)]) == 0
    assert run(["format", "--formatter", "ruff", str(path)]) == 2
    assert "ruff is not installed" in capsys.readouterr().err


def test_a_formatter_the_table_does_not_know_is_a_configuration_error(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    _write(tmp_path, "pyproject.toml", '[tool.stanza]\nformatter = "prettier"\n')
    path = _write(tmp_path, "a.py", JOINABLE)

    monkeypatch.chdir(tmp_path)

    assert run(["format", str(path)]) == 2

    with pytest.raises(ConfigError):
        Config.create(formatter="prettier")  # type: ignore[arg-type]


def _install(monkeypatch: pytest.MonkeyPatch, *names: str) -> None:
    """Makes exactly the named formatters findable, as a path that holds only them would."""
    monkeypatch.setattr(shutil, "which", lambda name: f"/fake/{name}" if name in names else None)
    monkeypatch.setattr(importlib.util, "find_spec", lambda name: None)


def _name_of(formatter: Formatter | None) -> str | None:
    return None if formatter is None else formatter.name


def test_the_formatter_defaults_to_auto() -> None:
    assert DEFAULT_FORMATTER == "auto"
    assert Config.create().formatter == "auto"


@pytest.mark.parametrize(
    ("files", "expected"),
    [
        ({"pyproject.toml": "[tool.ruff]\nline-length = 100\n"}, "ruff"),
        ({"pyproject.toml": "[tool.ruff.lint]\nselect = ['E']\n"}, "ruff"),
        ({"ruff.toml": ""}, "ruff"),
        ({".ruff.toml": ""}, "ruff"),
        ({"pyproject.toml": "[tool.black]\nline-length = 100\n"}, "black"),
        ({"pyproject.toml": "[tool.black]\n\n[tool.ruff]\n"}, "ruff"),
        ({"pyproject.toml": "[tool.black]\n", "ruff.toml": ""}, "ruff"),
        ({"pyproject.toml": "[tool.stanza]\nlevel = 'strong'\n"}, None),
        ({"pyproject.toml": "[tool.ruff\n"}, None),
        ({}, None),
    ],
)
def test_the_formatter_a_project_configures_is_detected(
    tmp_path: Path, files: dict[str, str], expected: str | None
) -> None:
    for name, text in files.items():
        _write(tmp_path, name, text)

    assert detect_formatter(tmp_path) == expected


def test_the_search_climbs_to_the_nearest_directory_that_configures_a_formatter(tmp_path: Path) -> None:
    nested = tmp_path / "packages" / "app" / "src"
    nested.mkdir(parents=True)

    _write(tmp_path, "ruff.toml", "")
    _write(tmp_path / "packages", "pyproject.toml", "[tool.stanza]\n")

    assert detect_formatter(nested) == "ruff"

    _write(tmp_path / "packages" / "app", "pyproject.toml", "[tool.black]\n")

    assert detect_formatter(nested) == "black"


def test_auto_takes_the_configured_formatter_over_the_installed_ones(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    _write(tmp_path, "pyproject.toml", "[tool.black]\n")
    _install(monkeypatch, "ruff", "black")

    assert _name_of(find_formatter("auto", tmp_path)) == "black"


def test_auto_does_not_fall_back_when_the_configured_formatter_is_missing(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    _write(tmp_path, "ruff.toml", "")
    _install(monkeypatch, "black")

    with pytest.raises(FormatterError, match="ruff is not installed"):
        find_formatter("auto", tmp_path)


@pytest.mark.parametrize(
    ("installed", "expected"),
    [(("ruff", "black"), "ruff"), (("ruff",), "ruff"), (("black",), "black")],
)
def test_auto_takes_the_first_installed_formatter_when_none_is_configured(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, installed: tuple[str, ...], expected: str
) -> None:
    _write(tmp_path, "pyproject.toml", "[tool.stanza]\n")
    _install(monkeypatch, *installed)

    assert _name_of(find_formatter("auto", tmp_path)) == expected


def test_auto_names_the_three_ways_out_when_it_finds_nothing(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    path = _write(tmp_path, "a.py", JOINABLE)

    _install(monkeypatch)

    with pytest.raises(FormatterError, match=r"Configure one .*install ruff or black.*--formatter none"):
        find_formatter("auto", tmp_path)

    assert run(["format", "--config", str(_write(tmp_path, "pyproject.toml", "[tool.stanza]\n")), str(path)]) == 2
    assert "no formatter found" in capsys.readouterr().err
    assert path.read_text() == JOINABLE


def test_an_explicit_formatter_skips_the_detection(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    _write(tmp_path, "ruff.toml", "")
    _install(monkeypatch, "ruff", "black")

    assert _name_of(find_formatter("black", tmp_path)) == "black"
    assert _name_of(find_formatter("ruff", tmp_path)) == "ruff"
    assert find_formatter("none", tmp_path) is None


def test_the_command_detects_from_the_directory_of_the_config_file(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    project = tmp_path / "project"
    project.mkdir()

    _write(project, "pyproject.toml", "[tool.stanza]\n\n[tool.black]\n")

    path = _write(tmp_path, "a.py", JOINABLE)
    found: list[Path] = []

    def record(name: str, start: Path | None = None) -> Formatter | None:
        assert start is not None

        found.append(start)

        return None

    monkeypatch.setattr("nickelony_stanza.cli.find_formatter", record)

    assert run(["format", "--config", str(project / "pyproject.toml"), str(path)]) == 0
    assert found == [project.resolve()]


def test_the_command_detects_from_the_directory_of_the_first_path_without_a_config_file(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    source = tmp_path / "src"
    source.mkdir()

    path = _write(source, "a.py", JOINABLE)
    found: list[Path] = []

    def record(name: str, start: Path | None = None) -> Formatter | None:
        assert start is not None

        found.append(start)

        return None

    monkeypatch.chdir(tmp_path / "src")

    monkeypatch.setattr("nickelony_stanza.cli.find_formatter", record)
    monkeypatch.setattr("nickelony_stanza.cli.load_config", lambda *args: (Config.create(), None))

    assert run(["format", str(path)]) == 0
    assert run(["format", str(source)]) == 0
    assert found == [source.resolve(), source.resolve()]


def test_the_formatter_key_and_the_flag_override_the_detection(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    _write(tmp_path, "ruff.toml", "")
    _write(tmp_path, "pyproject.toml", '[tool.stanza]\nformatter = "none"\n')

    path = _write(tmp_path, "a.py", "a = 1\nb = 2\nrun()\n")

    monkeypatch.chdir(tmp_path)
    _install(monkeypatch)

    assert run(["format", str(path)]) == 0
    assert run(["format", "--formatter", "auto", str(path)]) == 2
    assert "ruff is not installed" in capsys.readouterr().err
