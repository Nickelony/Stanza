"""The `stanza-py` command: `check` reports what the rules ask for, `fix` writes it, `format` formats first."""

from __future__ import annotations

import argparse
import difflib
import sys
from collections.abc import Sequence
from dataclasses import dataclass, replace
from pathlib import Path

from nickelony_stanza import __version__
from nickelony_stanza.analysis.document import Document, read_document
from nickelony_stanza.analysis.module_index import MAX_PROJECT_BYTES, MAX_PROJECT_FILES
from nickelony_stanza.analysis.projects import Projects
from nickelony_stanza.atomic_write import write_atomically
from nickelony_stanza.config import Config, ConfigError, load_config
from nickelony_stanza.engine import FixError, fix, lint
from nickelony_stanza.formatter import FORMATTERS, Formatter, FormatterError, find_formatter
from nickelony_stanza.paths import collect_files
from nickelony_stanza.pipeline import run_pipeline
from nickelony_stanza.rules import ALL_RULES
from nickelony_stanza.rules.base import LEVELS

EXIT_CLEAN = 0
EXIT_FINDINGS = 1
EXIT_ERROR = 2


def _count(number: int, noun: str) -> str:
    return f"{number} {noun}" if number == 1 else f"{number} {noun}s"


def _split(values: Sequence[str]) -> list[str]:
    return [item.strip() for value in values for item in value.split(",") if item.strip()]


def _create_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="stanza-py", description="Lay out the blank lines of Python code and name the arguments of its calls."
    )

    parser.add_argument("--version", action="version", version=f"stanza-py {__version__}")
    commands = parser.add_subparsers(dest="command", required=True)

    for name, help_text in (
        ("check", "Report the blank lines and argument names the rules ask for. Exits with 1 when there are any."),
        ("fix", "Write the blank lines and argument names the rules ask for."),
        (
            "format",
            "Run the formatter, write what the rules ask for, and check that the formatter keeps it.",
        ),
    ):
        command = commands.add_parser(name, help=help_text, description=help_text)

        command.add_argument("paths", nargs="*", default=["."], help="Files, directories, or - for standard input.")
        command.add_argument("--config", type=Path, help="A pyproject.toml with a [tool.stanza] table.")
        command.add_argument("--level", choices=list(LEVELS), help="The preset to start from.")

        command.add_argument(
            "--enable", action="append", default=[], metavar="RULES", help="Rules to add, by code or name."
        )

        command.add_argument(
            "--disable",
            action="append",
            default=[],
            metavar="RULES",
            help="Rules to remove, by code or name.",
        )

        command.add_argument("--exclude", action="append", default=[], metavar="GLOB", help="Paths to skip.")

        command.add_argument(
            "--stdin-filename",
            metavar="PATH",
            help="The path of the file standard input holds, so the rules resolve its calls against its project.",
        )

        if name == "check":
            command.add_argument("--diff", action="store_true", help="Show the fix of each file as a diff.")

        if name == "format":
            command.add_argument(
                "--check", action="store_true", help="Write nothing. Exit with 1 when a file would change."
            )

            command.add_argument(
                "--formatter",
                choices=list(FORMATTERS),
                help="The formatter to run first. The default is the formatter key, else auto.",
            )

    commands.add_parser("rules", help="List the rules with their levels.")

    return parser


def _load(args: argparse.Namespace) -> tuple[Config, Path]:
    """Loads the configuration of a run, and names the directory the formatter detection starts from."""
    config, pyproject = load_config(args.config, Path.cwd())
    config = config.override(args.level, _split(args.enable), _split(args.disable))
    config = replace(config, exclude=(*config.exclude, *_split(args.exclude)))

    if getattr(args, "formatter", None):
        config = replace(config, formatter=args.formatter)

    return config, _project_directory(pyproject, args.paths)


def _project_directory(pyproject: Path | None, paths: Sequence[str]) -> Path:
    """The directory of the Stanza configuration file, else the one that holds the first path."""
    if pyproject is not None:
        return pyproject.resolve().parent

    first = next((Path(path).resolve() for path in paths if path != "-"), Path.cwd())

    return first if first.is_dir() else first.parent


# The note a run prints once when a project scan stopped at its caps, which leaves every call unnamed.
_SCAN_NOTE = (
    f"stanza-py: note: the project holds more than {MAX_PROJECT_FILES} files or {MAX_PROJECT_BYTES // 1_000_000} MB "
    "of source, so the call rules left every call alone; name the project directories with source-roots."
)


@dataclass(frozen=True)
class _Target:
    """A file a run reads, or standard input with no `path`, and the path its calls resolve against."""

    label: str
    path: Path | None
    source: Path | None


def _print_rules() -> int:
    for rule in ALL_RULES:
        print(f"{rule.code}  {rule.level:<9} {rule.name:<32} {rule.description}")

    return EXIT_CLEAN


def _check_document(target: _Target, document: Document, config: Config, projects: Projects, *, show_diff: bool) -> int:
    label, path = target.label, target.source
    findings = lint(document.text, config, path=path, projects=projects)

    for finding in findings:
        print(f"{label}:{finding.line}:{finding.column}: {finding.code} {finding.message} ({finding.rule.name})")

    if findings and show_diff:
        fixed = fix(document.text, config, path=path, projects=projects).text

        sys.stdout.writelines(
            difflib.unified_diff(document.text.splitlines(True), fixed.splitlines(True), f"a/{label}", f"b/{label}")
        )

    return len(findings)


def _format_document(
    target: _Target, document: Document, config: Config, formatter: Formatter | None, projects: Projects, *, check: bool
) -> tuple[bool, bool]:
    """Runs the formatter and the fix over a document, and tells whether it changed and whether it settled."""
    label, path = target.label, target.path
    filename = label if path else str(target.source or "stdin.py")
    result = run_pipeline(document, config, formatter, filename, path=target.source, projects=projects)
    changed = result.text != document.text

    if path is None:
        if not check:
            sys.stdout.buffer.write(result.text.encode(document.encoding))
            sys.stdout.buffer.flush()
    elif changed and check:
        print(f"would format {label}")
    elif changed:
        write_atomically(path, result.text.encode(document.encoding))
        print(f"formatted {label}")

    if not result.is_settled:
        name = formatter.name if formatter else "the formatter"
        print(f"{label}: unsettled: {name} changes what the rules wrote. Run it once more.", file=sys.stderr)

    return changed, not result.is_settled


def _fix_document(target: _Target, document: Document, config: Config, projects: Projects) -> bool:
    """Fixes a document, writes the result to its file or to standard output, and tells whether it changed."""
    label, path = target.label, target.path
    result = fix(document.text, config, path=target.source, projects=projects)
    changed = result.text != document.text

    if path is None:
        sys.stdout.buffer.write(result.text.encode(document.encoding))
        sys.stdout.buffer.flush()
    elif changed:
        write_atomically(path, result.text.encode(document.encoding))
        print(f"fixed {label}")

    return changed


def run(argv: Sequence[str] | None = None) -> int:
    """Runs the command line and returns its exit code."""
    args = _create_parser().parse_args(argv)

    if args.command == "rules":
        return _print_rules()

    try:
        config, project = _load(args)
        formatter = find_formatter(config.formatter, project) if args.command == "format" else None
        files = [Path(path) for path in collect_files([p for p in args.paths if p != "-"], config.exclude)]
    except (ConfigError, FormatterError, FileNotFoundError) as error:
        print(f"stanza-py: error: {error}", file=sys.stderr)
        return EXIT_ERROR

    targets = [_Target(str(path), path, path) for path in files]

    if "-" in args.paths:
        stdin_path = Path(args.stdin_filename) if args.stdin_filename else None
        targets.append(_Target(args.stdin_filename or "<stdin>", None, stdin_path))

    # One set of indexes serves every file of the run, so each module a file imports is parsed once.
    projects = Projects(config.options.source_roots)

    failed = False
    findings = 0
    changed = 0
    unsettled = 0

    for target in targets:
        label, path = target.label, target.path

        try:
            data = sys.stdin.buffer.read() if path is None else path.read_bytes()
            document = read_document(data)

            if args.command == "check":
                findings += _check_document(target, document, config, projects, show_diff=args.diff)
                continue

            if args.command == "format":
                is_changed, is_unsettled = _format_document(
                    target, document, config, formatter, projects, check=args.check
                )

                changed += is_changed
                unsettled += is_unsettled

                continue

            changed += _fix_document(target, document, config, projects)
        except (
            SyntaxError,
            UnicodeDecodeError,
            OSError,
            FixError,
            FormatterError,
            ValueError,
            RecursionError,
        ) as error:
            print(f"{label}: error: {error}", file=sys.stderr)
            failed = True

    if projects.passed_caps:
        print(_SCAN_NOTE, file=sys.stderr)

    if failed:
        return EXIT_ERROR

    if args.command == "check":
        print(f"{_count(findings, 'finding')} in {_count(len(targets), 'file')}.", file=sys.stderr)
        return EXIT_FINDINGS if findings else EXIT_CLEAN

    if args.command == "format":
        verb = "would change" if args.check else "formatted"
        print(f"{_count(changed, 'file')} {verb}, {unsettled} unsettled.", file=sys.stderr)

        return EXIT_FINDINGS if unsettled or (args.check and changed) else EXIT_CLEAN

    print(f"{_count(changed, 'file')} fixed.", file=sys.stderr)

    return EXIT_CLEAN


def main() -> None:
    """The entry point of the `stanza-py` script."""
    sys.exit(run())


__all__ = ["EXIT_CLEAN", "EXIT_ERROR", "EXIT_FINDINGS", "main", "run"]
