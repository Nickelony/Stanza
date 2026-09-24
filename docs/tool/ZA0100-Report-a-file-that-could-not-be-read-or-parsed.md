# ZA0100 - Report a file that could not be read or parsed

[`stanza-markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md) reports a requested file it cannot process.
Causes include undecodable input, a document above the [size limit](https://github.com/Nickelony/Stanza/wiki/Markup-behavior#what-it-leaves-alone),
invalid markup, or a declared encoding that contradicts the bytes the file starts with.

`check --format msbuild` prints one line per failed file:

```text
parts/broken.xml(5,1): warning ZA0100: could not be laid out: Unexpected end of file has occurred. The following elements are not closed: member, members, doc.
parts/undecodable.xml(1,1): warning ZA0100: could not be read: the bytes could not be decoded as utf-8, so it was left alone.
```

Without `--format msbuild`, each line goes to standard error without the code.

- A failure message that spans several lines is joined onto one line, after the position.
- `check --format msbuild` reports the file at the failure position, or at line `1`, column `1` when the failure has no position.
- A file that cannot be read or parsed is a warning, so it fails a run only under `--strict`.
  A defect of the tool and a failed write use this code too, and are errors in every mode;
  [Failures and warnings](https://github.com/Nickelony/Stanza/wiki/Markup-behavior#failures-and-warnings) lists every case.
- Analyzer rules do not report this code.

The [command reference](https://github.com/Nickelony/Stanza/wiki/Markup-command-reference#exit-codes) lists the exit codes.
