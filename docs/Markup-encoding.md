# Markup encoding

`stanza-markup` reads each file in the encoding its byte order mark or XML declaration names, and writes it back in that encoding.

In this page, the *mark* is the byte order mark at the start of a file,
and the *declaration* is the `encoding` attribute of its XML declaration.

- A mark or declaration naming a supported encoding (see [Declaration names](#declaration-names)) sets the encoding.
  When both are present and agree, the file is written back in that encoding with its mark as it was;
  a disagreeing declaration is refused, as the next bullet says.
- A mark whose declaration names a different encoding is refused in every mode, as a [failure](https://github.com/Nickelony/Stanza/wiki/Terms).
  A declaration naming an encoding outside the supported set also counts as different.
- With no mark, a declaration naming an encoding outside the supported set is read as `iso-8859-1` by `check`.
  `format` refuses to write such a file, as the same failure, but only when the file needs a change.
- A UTF-32 or UTF-16 file with no mark that opens with `<` is sniffed from its bytes,
  and a declaration naming another encoding in it is refused in every mode.
- A U+FEFF character after the mark is not a mark, so the file is reported as one that could not be laid out, with a reason that names it.
- A file with no mark whose bytes hold no such opening, but whose declaration names UTF-16 or UTF-32, is refused the same way.
- With neither a mark nor a declaration, the file is read as UTF-8, and refused rather than mangled when its bytes do not decode.

## Declaration names

The tool reads the name from the XML declaration, not from `.editorconfig`, and matches it without regard to case.

| Encoding | Accepted names |
| --- | --- |
| UTF-8 | `utf-8`, `utf8` |
| US-ASCII | `us-ascii`, `ascii` |
| ISO-8859-1 | `iso-8859-1`, `latin1`, `latin-1` |
| UTF-16, little endian | `utf-16`, `utf-16le`, `unicode` |
| UTF-16, big endian | `utf-16be`, `unicodefffe` |
| UTF-32, little endian | `utf-32`, `utf-32le` |
| UTF-32, big endian | `utf-32be` |

With a mark, `utf-16` and `utf-32` agree with either byte order the mark states.
