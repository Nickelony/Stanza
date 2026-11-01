"""Reads a Python file from its bytes: the text and the encoding to write it back with."""

from __future__ import annotations

import io
import tokenize
from dataclasses import dataclass


@dataclass(frozen=True)
class Document:
    """A file read as bytes: the text and the encoding to write it back with."""

    text: str
    encoding: str


def read_document(data: bytes) -> Document:
    """Decodes a Python file the way the interpreter does, keeping its line endings."""
    encoding, _ = tokenize.detect_encoding(io.BytesIO(data).readline)
    return Document(data.decode(encoding), encoding)
