"""A fixed file is replaced whole or left as it was, keeps its mode and is written through a link."""

from __future__ import annotations

import os
import stat
import sys
from pathlib import Path

import pytest

from nickelony_stanza.atomic_write import TEMPORARY_SUFFIX, write_atomically
from nickelony_stanza.cli import run
from nickelony_stanza.paths import collect_files

BEFORE = "a = 1\nb = 2\nrun()\n"
AFTER = "a = 1\nb = 2\n\nrun()\n"

POSIX_ONLY = pytest.mark.skipif(sys.platform == "win32", reason="the mode bits of Windows are the read-only flag only")


def _names(directory: Path) -> list[str]:
    return sorted(path.name for path in directory.iterdir())


def test_a_write_replaces_the_file_and_leaves_no_temporary_file(tmp_path: Path) -> None:
    path = tmp_path / "a.py"
    path.write_text(BEFORE)

    write_atomically(path, AFTER.encode())

    assert path.read_text() == AFTER
    assert _names(tmp_path) == ["a.py"]


@pytest.mark.parametrize("step", ["replace", "fsync"])
def test_a_failure_while_writing_leaves_the_original_and_no_temporary_file(
    step: str, tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    path = tmp_path / "a.py"
    path.write_text(BEFORE)

    def fail(*arguments: object) -> None:
        raise OSError("No space left on device")

    monkeypatch.setattr(os, step, fail)

    assert run(["fix", str(path)]) == 2
    assert "No space left on device" in capsys.readouterr().err
    assert path.read_text() == BEFORE
    assert _names(tmp_path) == ["a.py"]


def test_an_interrupt_while_writing_removes_the_temporary_file(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    path = tmp_path / "a.py"
    path.write_text(BEFORE)

    def interrupt(*arguments: object) -> None:
        raise KeyboardInterrupt

    monkeypatch.setattr(os, "fsync", interrupt)

    with pytest.raises(KeyboardInterrupt):
        write_atomically(path, AFTER.encode())

    assert path.read_text() == BEFORE
    assert _names(tmp_path) == ["a.py"]


@POSIX_ONLY
def test_a_write_keeps_the_mode_of_the_file(tmp_path: Path) -> None:
    path = tmp_path / "a.py"
    path.write_text(BEFORE)
    path.chmod(0o750)

    assert run(["fix", str(path)]) == 0
    assert path.read_text() == AFTER
    assert stat.S_IMODE(path.stat().st_mode) == 0o750


def test_a_write_through_a_link_replaces_the_target_and_keeps_the_link(tmp_path: Path) -> None:
    target = tmp_path / "target.py"
    link = tmp_path / "link.py"

    target.write_text(BEFORE)

    try:
        link.symlink_to(target)
    except OSError:
        pytest.skip("this machine cannot make a symbolic link")

    write_atomically(link, AFTER.encode())

    assert link.is_symlink()
    assert target.read_text() == AFTER
    assert _names(tmp_path) == ["link.py", "target.py"]


def test_a_read_only_file_is_refused_and_left_alone(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    path = tmp_path / "a.py"
    path.write_text(BEFORE)
    path.chmod(stat.S_IREAD)

    try:
        if os.access(path, os.W_OK):
            pytest.skip("this account writes to a read-only file")

        assert run(["fix", str(path)]) == 2
        assert "read-only" in capsys.readouterr().err
        assert path.read_text() == BEFORE
        assert _names(tmp_path) == ["a.py"]
    finally:
        path.chmod(stat.S_IWRITE | stat.S_IREAD)


def test_a_directory_scan_skips_the_temporary_file_a_killed_write_leaves(tmp_path: Path) -> None:
    (tmp_path / "a.py").write_text(BEFORE)
    (tmp_path / f"a.py{TEMPORARY_SUFFIX}").write_text(BEFORE)

    assert [path.name for path in collect_files([str(tmp_path)], [])] == ["a.py"]
