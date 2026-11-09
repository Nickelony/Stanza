"""The source indexes of a run, one for each set of project roots, so the files of one project share their parses."""

from __future__ import annotations

from collections.abc import Iterable
from pathlib import Path

from nickelony_stanza.analysis.binding import SourceIndex, detect_roots
from nickelony_stanza.analysis.module_index import normalize

# The path the resolver reads a text without a file under: no file or package can stand there,
# so the text resolves its own definitions, the standard library and the installed packages, and no project module.
STDIN_PATH = Path("<stdin>", "<stdin>.py")


class Projects:
    """Hands each file the index of its project roots: the configured ones, else the roots detected from the file."""

    def __init__(self, roots: Iterable[str | Path] = ()) -> None:
        self._roots = tuple(normalize(Path(root)) for root in roots)
        self._indexes: dict[tuple[Path, ...], SourceIndex] = {}
        self._detected: dict[Path, tuple[Path, ...]] = {}

    def _detect(self, path: Path) -> tuple[Path, ...]:
        directory = normalize(path).parent

        if directory not in self._detected:
            self._detected[directory] = detect_roots(path)

        return self._detected[directory]

    @property
    def passed_caps(self) -> bool:
        """Tells whether the project scan of any index stopped at its caps, so the call rules left every call alone."""
        return any(index.passed_caps for index in self._indexes.values())

    def index_for(self, path: Path | None) -> SourceIndex:
        """Gets the index of a file, shared with every file of the same roots; a text without a file has no roots."""
        roots = self._roots if self._roots or path is None else self._detect(path)
        index = self._indexes.get(roots)

        if index is None:
            index = self._indexes[roots] = SourceIndex(roots, configured=bool(self._roots))

        return index
