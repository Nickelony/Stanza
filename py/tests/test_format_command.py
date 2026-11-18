"""The `format` command runs the formatter, then the rules, and checks that the formatter keeps the result."""

from __future__ import annotations

import importlib.util
import io
import os
import shutil
import subprocess
import sys
from pathlib import Path
from typing import Literal

import pytest

from nickelony_stanza import Config, ConfigError, fix
from nickelony_stanza import formatter as formatter_module
from nickelony_stanza.analysis.document import read_document
from nickelony_stanza.cli import run
from nickelony_stanza.formatter import Formatter, FormatterError, find_formatter
from nickelony_stanza.pipeline import run_pipeline
from tests.helpers import run_formatter, write_file

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


def _undo_formatter() -> Formatter:
    return Formatter("fake", (sys.executable, "-c", UNDO))


def _hide_formatters(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(formatter_module, "_which", lambda name: None)
    monkeypatch.setattr(formatter_module, "_has_module", lambda name: False)


@pytest.mark.parametrize("tool", TOOLS)
def test_the_formatter_runs_before_the_rules(tool: str, tmp_path: Path) -> None:
    _require(tool)
    path = write_file(tmp_path, "a.py", JOINABLE)

    assert run(["format", "--formatter", tool, "--level", "moderate", str(path)]) == 0
    assert path.read_text() == JOINED

    # The rules first and the formatter second leave padding beside a call the formatter joins.
    padded = fix(JOINABLE, Config.create("moderate")).text
    assert run_formatter(tool, padded) != JOINED


@pytest.mark.parametrize("tool", TOOLS)
def test_a_second_run_changes_nothing_and_a_check_passes(tool: str, tmp_path: Path) -> None:
    _require(tool)

    path = write_file(
        tmp_path, "a.py", "import os\nx=1\ny = 2\ndef f(a,b):\n  if a: return b\n  return a\nprint(f(1,2))\n"
    )
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
    path = write_file(tmp_path, "a.py", JOINABLE)

    assert run(["format", "--check", "--formatter", tool, "--level", "moderate", str(path)]) == 1
    assert path.read_text() == JOINABLE


def test_a_check_names_the_files_it_would_change(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    path = write_file(tmp_path, "a.py", "a = 1\nb = 2\nrun()\n")

    assert run(["format", "--check", "--formatter", "none", str(path)]) == 1

    captured = capsys.readouterr()

    assert f"would format {path}" in captured.out
    assert "1 file would change, 0 unsettled." in captured.err


def test_the_none_formatter_runs_the_rules_alone(tmp_path: Path) -> None:
    path = write_file(tmp_path, "a.py", "a = 1\nb = 2\nrun()\n")

    assert run(["format", "--formatter", "none", str(path)]) == 0
    assert path.read_text() == "a = 1\nb = 2\n\nrun()\n"


def test_a_formatter_that_undoes_the_fix_is_a_settle_failure(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    path = write_file(tmp_path, "a.py", TIGHT)

    monkeypatch.setattr("nickelony_stanza.cli.find_formatter", lambda *args: _undo_formatter())

    assert run(["format", "--level", "none", "--enable", "ZA3201", str(path)]) == 1
    assert path.read_text() == SEPARATED

    captured = capsys.readouterr()

    assert f"{path}: unsettled: fake changes what the rules wrote" in captured.err
    assert "1 file formatted, 1 unsettled." in captured.err


def test_a_check_reports_a_settle_failure_without_writing(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    path = write_file(tmp_path, "a.py", TIGHT)

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
    path = write_file(tmp_path, "a.py", JOINABLE)

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
    bad = write_file(tmp_path, "bad.py", "def (:\n")

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
    write_file(tmp_path, "pyproject.toml", '[tool.stanza]\nformatter = "none"\n')
    path = write_file(tmp_path, "a.py", "a = 1\nb = 2\nrun()\n")

    monkeypatch.chdir(tmp_path)
    _hide_formatters(monkeypatch)

    assert run(["format", str(path)]) == 0
    assert run(["format", "--formatter", "ruff", str(path)]) == 2
    assert "ruff is not installed" in capsys.readouterr().err


def test_a_formatter_the_table_does_not_know_is_a_configuration_error(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    write_file(tmp_path, "pyproject.toml", '[tool.stanza]\nformatter = "prettier"\n')
    path = write_file(tmp_path, "a.py", JOINABLE)

    monkeypatch.chdir(tmp_path)

    assert run(["format", str(path)]) == 2

    with pytest.raises(ConfigError):
        Config.create(formatter="prettier")  # type: ignore[arg-type]


def _plant(directory: Path, name: str) -> Path:
    """Writes a file that the system would start as the program `name`, as a checkout could carry one."""
    planted = directory / (f"{name}.exe" if sys.platform == "win32" else name)
    planted.write_bytes(b"")
    planted.chmod(0o755)

    return planted


def test_a_formatter_in_the_current_directory_is_never_started(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    checkout, installed = tmp_path / "checkout", tmp_path / "installed"
    checkout.mkdir()
    installed.mkdir()

    _plant(checkout, "ruff")
    _plant(checkout, "black")

    # The path names the checkout in every way the current directory can be named, and then the real directory.
    entries = ["", ".", str(checkout), f'"{checkout}"', str(installed)]

    monkeypatch.chdir(checkout)
    monkeypatch.setenv("PATH", os.pathsep.join(entries))
    monkeypatch.setattr(formatter_module, "_has_module", lambda name: False)

    for name in ("ruff", "black"):
        with pytest.raises(FormatterError, match="not installed"):
            find_formatter(name)

    real = _plant(installed, "ruff")

    found = find_formatter("ruff")

    assert found is not None
    assert os.path.samefile(found.command[0], real)

    with pytest.raises(FormatterError, match="not installed"):
        find_formatter("black")


@pytest.mark.parametrize("tool", TOOLS)
def test_a_formatter_module_in_the_current_directory_is_never_run(
    tool: Literal["ruff", "black"], tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    if importlib.util.find_spec(tool) is None:
        pytest.skip(f"{tool} is not installed as a module")

    checkout, empty = tmp_path / "checkout", tmp_path / "empty"
    checkout.mkdir()
    empty.mkdir()

    marker = checkout / "ran.txt"

    for name in TOOLS:
        write_file(checkout, f"{name}.py", f"open({str(marker)!r}, 'w').write('planted')\n")

    # No program on the path, so the formatter is the module of this interpreter, which the planted file would shadow.
    monkeypatch.chdir(checkout)
    monkeypatch.setenv("PATH", str(empty))

    found = find_formatter(tool)

    assert found is not None
    assert found.command[0] == sys.executable
    assert found.format(JOINABLE.encode(), "a.py").decode().replace("\r\n", "\n") == JOINED
    assert not marker.exists()


def test_the_module_runner_takes_the_current_directory_off_the_path_before_it_imports_anything(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    if importlib.util.find_spec("ruff") is None:
        pytest.skip("ruff is not installed as a module")

    checkout = tmp_path / "checkout"
    checkout.mkdir()

    marker = checkout / "ran.txt"

    # Before Python 3.11 `runpy` is a source file on the path, and `-X frozen_modules=off` makes it one on every version.
    for name in ["runpy", "ruff"]:
        write_file(checkout, f"{name}.py", f"open({str(marker)!r}, 'a').write('{name}')\n")

    monkeypatch.chdir(checkout)

    command = formatter_module._module_command("ruff")

    result = subprocess.run(
        [command[0], "-X", "frozen_modules=off", *command[1:], "format", "--stdin-filename", "a.py", "-"],
        input=JOINABLE.encode(),
        capture_output=True,
        check=False,
    )

    assert result.returncode == 0, result.stderr.decode()
    assert result.stdout.decode().replace("\r\n", "\n") == JOINED
    assert not marker.exists()


def test_a_formatter_module_only_in_the_current_directory_is_not_installed(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    write_file(tmp_path, "nofmt_ruff.py", "")
    monkeypatch.chdir(tmp_path)
    monkeypatch.syspath_prepend(str(tmp_path))

    assert not formatter_module._has_module("nofmt_ruff")


def test_a_formatter_on_a_relative_path_entry_is_never_started(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    (tmp_path / "bin").mkdir()
    _plant(tmp_path / "bin", "ruff")

    monkeypatch.chdir(tmp_path)
    monkeypatch.setenv("PATH", "bin")
    monkeypatch.setattr(formatter_module, "_has_module", lambda name: False)

    with pytest.raises(FormatterError, match="not installed"):
        find_formatter("ruff")
