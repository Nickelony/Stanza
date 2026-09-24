# Markup globs

`stanza-markup` reads a glob on the command line, in `--exclude` and in an `.editorconfig` section header with one dialect,
and this page lists what that dialect refuses and how it reads a path.
The [command reference](https://github.com/Nickelony/Stanza/wiki/Markup-command-reference#paths-and-globs) says what each place does with a glob.

## Refused globs

- The refused forms are numeric ranges such as `{1..3}` or `{-1..4}`, unclosed or empty classes, a trailing backslash,
  a class that holds `[`, `]`, `^`, `\` or the separator, and alternations nested more than 16 levels.
- A pattern of about 200 literal characters or wildcards is refused too, and a class such as `[a-c]` counts as one wildcard.
- A pattern of more than 1,024 characters is refused before it is translated, which bounds the time the translation takes;
  a rooted pattern is measured without its leading `/`.
- An `--exclude` or a section glob in `.editorconfig` that the dialect refuses matches nothing and is a warning, which `--strict` makes an error.

## Escaping

- A command-line glob or `--exclude` turns each backslash into a slash,
  so a backslash escapes the character after it only in `.editorconfig` sections.
- A command-line glob therefore cannot escape a bracket: write `?` for each bracket of a directory name,
  so `a?1?/*.xml` reaches the files under `a[1]`. A path without a wildcard is read as written.

## Roots and the working directory

- A leading `/` or a drive names the root of the file system in a path, a glob and an `--exclude` alike,
  so `/**/*.xml` and `C:\*.xml` search from the root, and `--exclude /sub` skips `sub` at the root.
- Write `./` for the working directory: `--exclude ./sub` skips `sub` there, `--exclude ./` skips every file,
  and `--exclude .` skips none, because no file is named `.`.

## Names to exclude

- A plain name such as `src` skips each file of that name, and each directory of that name below the working directory.
- An `--exclude` with a wildcard in its last segment, such as `gen*`, skips files only, never a directory.
