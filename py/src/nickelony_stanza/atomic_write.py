"""Writes a file through a flushed temporary file beside it, so a failure never leaves the file half written."""

from __future__ import annotations

import contextlib
import os
import shutil
import tempfile
from pathlib import Path

# The extension of the temporary file a killed write leaves, which no scan reads because it is not `.py`.
TEMPORARY_SUFFIX = ".stanza-tmp"

# The characters of the file name a temporary name keeps, so a name at the limit still fits.
MAX_NAME_START = 32


def write_atomically(path: Path, data: bytes) -> None:
    """
    Replaces a file with the bytes, keeping its mode, and writes through a link to the file it points at.

    A read-only file is refused, because that is the author's mark that it stays as it is.
    The file is untouched or replaced whole, and a failure removes the temporary file.
    """
    target = Path(os.path.realpath(path))

    if not os.access(target, os.W_OK):
        raise PermissionError("the file is read-only, so it was left alone.")

    descriptor, name = tempfile.mkstemp(
        dir=target.parent, prefix=f"{target.name[:MAX_NAME_START]}.", suffix=TEMPORARY_SUFFIX
    )

    temporary = Path(name)

    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())

        # The new file starts private, so a mode that cannot be copied does not fail the write.
        with contextlib.suppress(OSError):
            shutil.copymode(target, temporary)

        os.replace(temporary, target)
    except BaseException:
        with contextlib.suppress(OSError):
            temporary.unlink()

        raise
