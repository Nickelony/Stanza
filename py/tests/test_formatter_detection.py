"""The `format` command and `find_formatter` pick the formatter a project configures, or the first one installed."""

from __future__ import annotations

from pathlib import Path

import pytest

from nickelony_stanza import Config
from nickelony_stanza import formatter as formatter_module
from nickelony_stanza.cli import run
from nickelony_stanza.formatter import DEFAULT_FORMATTER, Formatter, FormatterError, detect_formatter, find_formatter
from tests.helpers import write_file

SOURCE = "a = 1\nb = 2\nrun()\n"


def _install(monkeypatch: pytest.MonkeyPatch, *names: str) -> None:
    """Makes exactly the named formatters findable, as a path that holds only them would."""
    monkeypatch.setattr(formatter_module, "_which", lambda name: f"/fake/{name}" if name in names else None)
    monkeypatch.setattr(formatter_module, "_has_module", lambda name: False)


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
        write_file(tmp_path, name, text)

    assert detect_formatter(tmp_path) == expected


def test_the_search_climbs_to_the_nearest_directory_that_configures_a_formatter(tmp_path: Path) -> None:
    nested = tmp_path / "packages" / "app" / "src"
    nested.mkdir(parents=True)

    write_file(tmp_path, "ruff.toml", "")
    write_file(tmp_path / "packages", "pyproject.toml", "[tool.stanza]\n")

    assert detect_formatter(nested) == "ruff"

    write_file(tmp_path / "packages" / "app", "pyproject.toml", "[tool.black]\n")

    assert detect_formatter(nested) == "black"


def test_auto_takes_the_configured_formatter_over_the_installed_ones(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    write_file(tmp_path, "pyproject.toml", "[tool.black]\n")
    _install(monkeypatch, "ruff", "black")

    assert _name_of(find_formatter("auto", tmp_path)) == "black"


def test_auto_does_not_fall_back_when_the_configured_formatter_is_missing(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    write_file(tmp_path, "ruff.toml", "")
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
    write_file(tmp_path, "pyproject.toml", "[tool.stanza]\n")
    _install(monkeypatch, *installed)

    assert _name_of(find_formatter("auto", tmp_path)) == expected


def test_auto_names_the_three_ways_out_when_it_finds_nothing(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    path = write_file(tmp_path, "a.py", SOURCE)

    _install(monkeypatch)

    with pytest.raises(FormatterError, match=r"Configure one .*install ruff or black.*--formatter none"):
        find_formatter("auto", tmp_path)

    assert run(["format", "--config", str(write_file(tmp_path, "pyproject.toml", "[tool.stanza]\n")), str(path)]) == 2
    assert "no formatter found" in capsys.readouterr().err
    assert path.read_text() == SOURCE


def test_an_explicit_formatter_skips_the_detection(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    write_file(tmp_path, "ruff.toml", "")
    _install(monkeypatch, "ruff", "black")

    assert _name_of(find_formatter("black", tmp_path)) == "black"
    assert _name_of(find_formatter("ruff", tmp_path)) == "ruff"
    assert find_formatter("none", tmp_path) is None


def test_the_command_detects_from_the_directory_of_the_config_file(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    project = tmp_path / "project"
    project.mkdir()

    write_file(project, "pyproject.toml", "[tool.stanza]\n\n[tool.black]\n")

    path = write_file(tmp_path, "a.py", SOURCE)
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

    path = write_file(source, "a.py", SOURCE)
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
    write_file(tmp_path, "ruff.toml", "")
    write_file(tmp_path, "pyproject.toml", '[tool.stanza]\nformatter = "none"\n')

    path = write_file(tmp_path, "a.py", "a = 1\nb = 2\nrun()\n")

    monkeypatch.chdir(tmp_path)
    _install(monkeypatch)

    assert run(["format", str(path)]) == 0
    assert run(["format", "--formatter", "auto", str(path)]) == 2
    assert "ruff is not installed" in capsys.readouterr().err
