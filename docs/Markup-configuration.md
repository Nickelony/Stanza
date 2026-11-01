# Markup configuration

`stanza-markup` reads its keys from `.editorconfig` for each file. The closest file and the last matching section win.
Each key has its own page, such as [`stanza_markup_scope`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_scope),
which lists its values and its default.
The [Markup](https://github.com/Nickelony/Stanza/wiki/Markup) page opens the rest of the tool's pages.

## Keys

| Key | Meaning |
| --- | --- |
| [`stanza_markup_enabled`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_enabled) | `false` takes the file out of the run. |
| [`stanza_markup_profile`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_profile) | A profile name instead of the extension. |
| [`stanza_markup_scope`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_scope) | `containers` or `all`: whether a wrapped leaf counts as multi-line. |
| [`stanza_markup_max_blank_lines`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_max_blank_lines) | The most blank lines a gap may keep; the option page says what `0` does. |
| [`stanza_markup_separated_elements`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_separated_elements) | Element names that take the blank lines of a multi-line element, such as `Separator`. |
| [`stanza_markup_doc_reprint`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_doc_reprint) | `members` writes the readable `<member>` bodies with the documentation layout; `off` does not. |

```ini
[*.{xml,config}]
stanza_markup_enabled = true
stanza_markup_profile = xml
stanza_markup_scope = all
stanza_markup_max_blank_lines = 2

[3rdParty/**]
stanza_markup_enabled = false
```

## How a file resolves its keys

- The search stops at the first file that sets `root = true`.
- A key written before the first `[section]` applies to every file below that directory.
- `=` and `:` both separate a key from its value, and `unset` removes a value an outer file set.
- A `#` or `;` ends a value or a section header, as it does for Roslyn, so a trailing comment is not part of either.
- A `stanza_` or `csharp_` value drops a `:severity` suffix such as `:suggestion`; a `dotnet_diagnostic` severity takes none.
- A section, key or file the tool cannot read is reported as a warning and leaves the other settings in force.
- A value the tool cannot use is reported as a warning, and the key takes its default.
  The analyzer takes the default without a warning.
- A `stanza_` key that no option page names, such as the misspelled `stanza_max_line_lenght`, is reported as a warning and ignored.
  A key only the analyzers read, such as `stanza_max_parameters` or [`stanza_markup_literals`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_literals),
  is never reported, because the tool does not check its value.
- `stanza_max_line_length`, `tab_width` and `indent_size`, which the [reprint](https://github.com/Nickelony/Stanza/wiki/Terms) also reads,
  have their values checked like a count, and `indent_size` also accepts `tab`.
  A misspelling of `tab_width` or `indent_size` is not reported, because the standard key has no `stanza_` prefix.
- The standard `max_line_length` is never reported, because `off` is a valid value of it that the chain of [`stanza_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_max_line_length) skips.
- `stanza_doc_weak_line_end_words` takes any word list, so no value of it is reported.
- `stanza_markup_separated_elements` reports an entry that is not a local element name, such as one with a space in it,
  and a value with a colon, such as `ui:Separator`, because the colon starts a severity suffix that cuts the value short.
- `stanza_doc_langwords` takes `null`, `true`, `false` and `default`, and a word it cannot name is reported and skipped.
- The tool reads no `.globalconfig`, so a setting made there reaches the analyzers alone;
  an `.editorconfig` that sets `is_global` is read like any other.

## Limits of one file

- The tool reads at most 1 MiB and 500 sections of one `.editorconfig`, far more than a real file holds.
- A file past the byte limit is read up to its last whole line within it, and a file past the section limit up to the 501st header.
- A file cut this way gets one warning that says what was ignored.
- A file whose first line is longer than the byte limit is ignored whole, and the warning says so.
- One `.editorconfig` reports at most 100 problems, the first in line order, and one more warning counts the rest.

## Section globs

A section header takes the glob dialect of the command line, and [Markup globs](https://github.com/Nickelony/Stanza/wiki/Markup-globs)
lists the forms the tool refuses.
A leading `/` in a header names the directory that holds the `.editorconfig`, not the root of the file system.

A backslash in a header escapes the character after it, so `[src\*.xml]` names a file whose name holds a star, as Roslyn reads it.
The tool warns when a backslash escapes a wildcard, because it is usually meant as a separator.

Roslyn reads some forms another way, so a section can reach different files in a build than in the tool:

| Form                                                        | stanza-markup                                 | Roslyn                             |
| ----------------------------------------------------------- | --------------------------------------------- | ---------------------------------- |
| Letter case, as in `[*.XML]`                                | ignored                                       | matched exactly                    |
| `?`                                                         | any character but `/`                         | any character, `/` included        |
| `**/`, as in `[src/**/x.xml]`                               | no directory or any number                    | at least one directory             |
| `{a}` and `{}`                                              | the literal text                              | an alternation of one, and of none |
| `[^a]`                                                      | refused with a warning, so it matches nothing | a class that holds `^` and `a`     |
| `[!a]`                                                      | any character but `a` and `/`                 | any character but `a`, even `/`    |
| A key before the first section                              | applies to every file below the directory     | ignored                            |

## Rules

The tool reads only the `dotnet_diagnostic.<id>.severity` lines of a file's chain,
and [Terms](https://github.com/Nickelony/Stanza/wiki/Terms) says which severity enables or disables a rule.
A value that names no severity is ignored.
A severity does not change how loudly the tool reports a finding.

[Markup behavior](https://github.com/Nickelony/Stanza/wiki/Markup-behavior#what-it-changes) says when each rule applies and how the analyzer differs.
