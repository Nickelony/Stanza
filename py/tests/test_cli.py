"""The command line, the configuration, the silencing comments and the flake8 plugin."""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

import pytest

from nickelony_stanza import Config, ConfigError, Options, fix, lint
from nickelony_stanza.analysis import module_index
from nickelony_stanza.cli import run
from nickelony_stanza.config import config_from_table, load_config
from tests.helpers import SCRIPTS

BEFORE = "a = 1\nb = 2\nrun()\n"
AFTER = "a = 1\nb = 2\n\nrun()\n"


def _write(directory: Path, name: str, text: str | bytes) -> Path:
    path = directory / name
    path.write_bytes(text if isinstance(text, bytes) else text.encode())

    return path


def test_check_exits_with_one_when_a_file_has_findings(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    path = _write(tmp_path, "a.py", BEFORE)

    assert run(["check", "--level", "moderate", str(path)]) == 1
    assert "ZA3002" in capsys.readouterr().out
    assert path.read_text() == BEFORE


def test_check_exits_with_zero_when_the_files_are_clean(tmp_path: Path) -> None:
    path = _write(tmp_path, "a.py", AFTER)
    assert run(["check", str(path)]) == 0


def test_check_shows_a_diff(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    path = _write(tmp_path, "a.py", BEFORE)

    run(["check", "--diff", str(path)])

    assert "+\n" in capsys.readouterr().out


def test_fix_writes_the_file_and_a_second_run_changes_nothing(tmp_path: Path) -> None:
    path = _write(tmp_path, "a.py", BEFORE)

    assert run(["fix", str(path)]) == 0
    assert path.read_text() == AFTER

    stamp = path.stat().st_mtime_ns

    assert run(["fix", str(path)]) == 0
    assert path.stat().st_mtime_ns == stamp


def test_fix_keeps_the_encoding_the_bom_and_the_line_endings(tmp_path: Path) -> None:
    latin = _write(
        tmp_path,
        "latin.py",
        "# -*- coding: latin-1 -*-\nname = 'caf\xe9'\nother = 2\nrun()\n".encode("latin-1"),
    )

    bom = _write(tmp_path, "bom.py", b"\xef\xbb\xbf" + BEFORE.encode())
    crlf = _write(tmp_path, "crlf.py", BEFORE.replace("\n", "\r\n"))

    assert run(["fix", str(tmp_path)]) == 0

    assert latin.read_bytes().decode("latin-1").endswith("other = 2\n\nrun()\n")
    assert bom.read_bytes() == b"\xef\xbb\xbf" + AFTER.encode()
    assert crlf.read_bytes() == AFTER.replace("\n", "\r\n").encode()


def test_a_syntax_error_exits_with_two_and_the_other_files_are_still_fixed(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    _write(tmp_path, "bad.py", "def (:\n")
    good = _write(tmp_path, "good.py", BEFORE)

    assert run(["fix", str(tmp_path)]) == 2
    assert "bad.py" in capsys.readouterr().err
    assert good.read_text() == AFTER


def test_a_missing_path_exits_with_two(tmp_path: Path) -> None:
    assert run(["check", str(tmp_path / "missing.py")]) == 2


def test_a_directory_scan_skips_stubs_and_the_usual_directories(tmp_path: Path) -> None:
    _write(tmp_path, "stub.pyi", BEFORE)
    (tmp_path / ".venv").mkdir()

    skipped = _write(tmp_path / ".venv", "lib.py", BEFORE)
    kept = _write(tmp_path, "kept.py", BEFORE)

    assert run(["fix", str(tmp_path)]) == 0
    assert (tmp_path / "stub.pyi").read_text() == BEFORE
    assert skipped.read_text() == BEFORE
    assert kept.read_text() == AFTER


def test_exclude_skips_the_paths_a_glob_names(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    (tmp_path / "gen").mkdir()

    generated = _write(tmp_path / "gen", "a.py", BEFORE)
    handwritten = _write(tmp_path, "b.py", BEFORE)

    monkeypatch.chdir(tmp_path)

    assert run(["fix", ".", "--exclude", "gen/**"]) == 0
    assert generated.read_text() == BEFORE
    assert handwritten.read_text() == AFTER


def test_standard_input_is_fixed_to_standard_output(monkeypatch: pytest.MonkeyPatch) -> None:
    result = subprocess.run(
        [str(SCRIPTS / "stanza-py"), "fix", "-"], input=BEFORE.encode(), capture_output=True, check=False
    )

    assert result.returncode == 0
    assert result.stdout.decode() == AFTER
    assert "1 file fixed." in result.stderr.decode()


def test_the_rules_command_lists_every_rule(capsys: pytest.CaptureFixture[str]) -> None:
    assert run(["rules"]) == 0
    assert "ZA3051" in capsys.readouterr().out


def test_the_rules_command_names_the_opt_in_level(capsys: pytest.CaptureFixture[str]) -> None:
    assert run(["rules"]) == 0
    assert "ZA3112  opt-in" in capsys.readouterr().out


def test_an_opt_in_rule_is_enabled_by_name_only(tmp_path: Path) -> None:
    path = _write(tmp_path, "a.py", "match v:\n    case 1:\n        a()\n\n    case 2:\n        b()\n")

    assert run(["check", "--level", "strong", str(path)]) == 0
    assert run(["check", "--level", "strong", "--enable", "keep-multi-line-cases-together", str(path)]) == 1
    assert run(["fix", "--enable", "ZA3112", str(path)]) == 0
    assert path.read_text(encoding="utf-8") == "match v:\n    case 1:\n        a()\n    case 2:\n        b()\n"


def test_the_module_runs_as_a_script(tmp_path: Path) -> None:
    path = _write(tmp_path, "a.py", AFTER)
    assert subprocess.run([sys.executable, "-m", "nickelony_stanza", "check", str(path)], check=False).returncode == 0


def test_the_level_enable_and_disable_choose_the_rules(tmp_path: Path) -> None:
    path = _write(tmp_path, "a.py", BEFORE)

    assert run(["check", "--level", "minimal", str(path)]) == 0
    assert run(["check", "--level", "minimal", "--enable", "separate-declaration-runs", str(path)]) == 1
    assert run(["check", "--level", "strong", "--disable", "ZA3002,ZA3003,ZA3004", str(path)]) == 0


def test_the_configuration_comes_from_pyproject(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    _write(tmp_path, "pyproject.toml", '[tool.stanza]\nlevel = "minimal"\nenable = ["ZA3002"]\n')
    path = _write(tmp_path, "a.py", BEFORE)
    monkeypatch.chdir(tmp_path)

    assert run(["check", str(path)]) == 1


def test_the_command_line_adds_to_the_rules_of_the_configuration(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    _write(tmp_path, "pyproject.toml", '[tool.stanza]\nlevel = "minimal"\n')
    path = _write(tmp_path, "a.py", "x = 1\nrun()\n# note\nlog(x)\n")
    monkeypatch.chdir(tmp_path)

    assert run(["check", str(path)]) == 0
    assert run(["check", str(path), "--enable", "ZA3012"]) == 1
    assert run(["check", str(path), "--enable", "ZA3012", "--disable", "ZA3012"]) == 0


def test_a_rule_the_configuration_disables_stays_off(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    _write(tmp_path, "pyproject.toml", '[tool.stanza]\nlevel = "strong"\ndisable = ["ZA3012"]\n')
    path = _write(tmp_path, "a.py", "x = 1\nrun()\n# note\nlog(x)\n")
    monkeypatch.chdir(tmp_path)

    assert run(["check", str(path)]) == 0
    assert run(["check", str(path), "--enable", "ZA3012"]) == 0


def test_a_bad_configuration_exits_with_two(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    _write(tmp_path, "pyproject.toml", '[tool.stanza]\nlevle = "strong"\n')
    path = _write(tmp_path, "a.py", BEFORE)
    monkeypatch.chdir(tmp_path)

    assert run(["check", str(path)]) == 2


def test_the_table_reads_every_key(tmp_path: Path) -> None:
    (tmp_path / "src").mkdir()

    config = config_from_table(
        {
            "level": "none",
            "enable": ["ZA3005"],
            "assertion-roots": ["verify"],
            "guard-roots": ["check"],
            "subscription-roots": ["listen"],
            "max-loose-run-size": 5,
            "exclude": ["gen/**"],
            "formatter": "black",
            "named-argument-min-arguments": 3,
            "named-argument-match": "require",
            "named-argument-literals": "bool",
            "named-argument-sources": ["project"],
            "named-argument-methods": "private",
            "named-argument-ignore": ["log.*"],
            "source-roots": ["src"],
        },
        tmp_path,
    )

    assert [rule.code for rule in config.rules] == ["ZA3005"]

    assert config.options == Options(
        ("verify",),
        ("check",),
        ("listen",),
        5,
        named_argument_min_arguments=3,
        named_argument_match="require",
        named_argument_literals="bool",
        named_argument_sources=("project",),
        named_argument_methods="private",
        named_argument_ignore=("log.*",),
        source_roots=(str((tmp_path / "src").resolve()),),
    )

    assert config.exclude == ("gen/**",)
    assert config.formatter == "black"


@pytest.mark.parametrize(
    "table",
    [
        {"level": "loud"},
        {"enable": ["ZA9999"]},
        {"enable": "ZA3001"},
        {"match-cases": True},
        {"split-declaration-kinds": True},
        {"subscription-roots": "connect"},
        {"max-loose-run-size": -1},
        {"max-loose-run-size": 1001},
        {"max-loose-run-size": "3"},
        {"max-loose-run-size": True},
        {"level": 1},
        {"formatter": "prettier"},
        {"named-argument-min-arguments": -1},
        {"named-argument-min-arguments": 1001},
        {"named-argument-min-arguments": "4"},
        {"named-argument-min-arguments": True},
        {"named-argument-match": "always"},
        {"named-argument-literals": "all"},
        {"named-argument-literals": True},
        {"named-argument-sources": ["web"]},
        {"named-argument-sources": "project"},
        {"named-argument-methods": "public"},
        {"named-argument-ignore": "log.*"},
        {"source-roots": "src"},
        {"source-roots": [Path.cwd().anchor]},
        {"source-roots": [str(Path.home())]},
        {"source-roots": ["no-such-directory"]},
        {"source-roots": ["~"]},
        {"source-roots": ["pyproject.toml"]},
        {"formatter": 1},
        {"unknown": 1},
    ],
)
def test_the_table_refuses_what_it_cannot_read(table: dict[str, object]) -> None:
    with pytest.raises(ConfigError):
        config_from_table(table)


@pytest.mark.parametrize(
    ("text", "silenced"),
    [
        ("a = 1\nb = 2\nrun()  # noqa\n", True),
        ("a = 1\nb = 2\nrun()  # noqa: ZA3002\n", True),
        ("a = 1\nb = 2\nrun()  # noqa: ZA3\n", True),
        ("a = 1\nb = 2\nrun()  # NOQA: E501, ZA3002\n", True),
        ("a = 1\nb = 2\nrun()  # noqa: E501\n", False),
        ("a = 1\nb = 2\nrun()  # noqa: ZA3005\n", False),
        ("# flake8: noqa\na = 1\nb = 2\nrun()\n", True),
        ("# ruff: noqa: ZA3002\na = 1\nb = 2\nrun()\n", True),
        ("# ruff: noqa: ZA3005\na = 1\nb = 2\nrun()\n", False),
        ('a = 1\nb = 2\nrun("# noqa")\n', False),
    ],
)
def test_noqa_comments_silence_a_finding(text: str, silenced: bool) -> None:
    config = Config.create("moderate")
    codes = [finding.code for finding in lint(text, config)]

    assert ("ZA3002" not in codes) is silenced
    assert (fix(text, config).text == text) is silenced


def test_a_silenced_gap_is_not_handed_to_another_rule() -> None:
    text = "x = 1\ny = 2\nif z:  # noqa: ZA3001\n    pass\n"
    config = Config.create("moderate")

    assert lint(text, config) == []
    assert fix(text, config).text == text


def test_the_flake8_plugin_reports_the_findings(tmp_path: Path) -> None:
    path = _write(tmp_path, "a.py", BEFORE)

    result = subprocess.run(
        [str(SCRIPTS / "flake8"), "--select", "ZA3", "--stanza-level", "moderate", "--isolated", str(path)],
        capture_output=True,
        check=False,
        cwd=tmp_path,
    )

    assert result.returncode == 1
    assert "ZA3002 Add a blank line" in result.stdout.decode()


def test_the_source_roots_are_read_from_the_directory_of_the_configuration_file(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    project, elsewhere = tmp_path / "project", tmp_path / "elsewhere"
    (project / "src").mkdir(parents=True)
    (elsewhere / "lib").mkdir(parents=True)
    monkeypatch.chdir(elsewhere)

    pyproject = _write(project, "pyproject.toml", '[tool.stanza]\nsource-roots = ["src", "."]\n')
    config, _ = load_config(pyproject, Path.cwd())

    assert config.options.source_roots == (str((project / "src").resolve()), str(project.resolve()))

    # A root is judged from the directory of the configuration file, even where the working directory holds one.
    _write(project, "pyproject.toml", '[tool.stanza]\nsource-roots = ["lib"]\n')

    with pytest.raises(ConfigError, match="which is not a directory"):
        load_config(pyproject, Path.cwd())


def test_a_run_whose_project_passes_the_scan_caps_notes_it_once(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    config = _write(
        tmp_path, "pyproject.toml", "[tool.stanza]\nlevel = 'none'\nenable = ['ZA3501']\nformatter = 'none'\n"
    )

    first = _write(tmp_path, "a.py", "def draw(a, b, c, d): ...\ndraw(1, 2, 3, 4)\n")
    second = _write(tmp_path, "b.py", "from a import draw\ndraw(1, 2, 3, 4)\n")

    _write(tmp_path, "c.py", "")

    arguments = ["check", "--config", str(config), str(first), str(second)]

    assert run(arguments) == 1
    assert "note" not in capsys.readouterr().err

    monkeypatch.setattr(module_index, "MAX_PROJECT_FILES", 2)

    assert run(arguments) == 0

    captured = capsys.readouterr()
    note = "stanza-py: note: the project holds more than 20000 files or 64 MB of source, so the call rules left every"

    assert captured.err.count(note) == 1
    assert captured.out == ""


def test_the_flake8_plugin_resolves_a_call_against_the_project_of_its_file(tmp_path: Path) -> None:
    _write(tmp_path, "pyproject.toml", "[project]\nname = 'demo'\n")
    _write(tmp_path, "shapes.py", "def draw(width, height, smooth, path): ...\n")

    path = _write(tmp_path, "main.py", "from shapes import draw\n\ndraw(1, 2, 3, 4)\n")
    command = [str(SCRIPTS / "flake8"), "--select", "ZA35", "--stanza-level", "strong", "--isolated"]

    from_file = subprocess.run([*command, str(path)], capture_output=True, check=False, cwd=tmp_path)

    from_stdin = subprocess.run(
        [*command, "-"], input=path.read_bytes(), capture_output=True, check=False, cwd=tmp_path
    )

    assert from_file.returncode == 1
    assert "main.py:3:5: ZA3501 Name the arguments of this call." in from_file.stdout.decode()
    assert from_stdin.returncode == 0
