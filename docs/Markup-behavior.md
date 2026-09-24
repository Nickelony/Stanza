# Markup behavior

`stanza-markup` lays out each file under one profile, and the profile decides which elements the rules see.
Outside a [reprinted](https://github.com/Nickelony/Stanza/wiki/Terms) `<member>` body, the tool changes only whitespace:
the [gaps](https://github.com/Nickelony/Stanza/wiki/Terms) between elements, and the space between a comment marker and its text.
The [Markup encoding](https://github.com/Nickelony/Stanza/wiki/Markup-encoding) page covers how a file's encoding is chosen.

## Profiles

A profile is the whole per-file-type policy.

- `--profile` forces one for the whole run.
- Otherwise a `stanza_markup_profile` entry decides, and the extension mapping is the last resort.
- A profile lays out well formed XML only.
  A document with no root element is accepted only when it holds nothing but comments, processing instructions and whitespace,
  and its comment markers are still laid out.

| Profile | Extensions | What it lays out |
| --- | --- | --- |
| `xml` | `.xml`, `.config` | a gap between two neighboring element children whenever either of them is multi-line |
| `msbuild` | `.csproj`, `.vbproj`, `.fsproj`, `.vcxproj`, `.props`, `.targets`, `.proj`, `.projitems`, `.shproj` | container elements only |
| `slnx` | `.slnx` | container elements only |
| `xaml` | `.xaml`, `.axaml` | container elements only, and never inside a property element |

- *Container elements only* keeps a project file quiet:
  a `PackageReference` whose *attributes* merely wrapped over two lines is a [leaf](https://github.com/Nickelony/Stanza/wiki/Terms),
  so it can never trigger a blank line.
- A `.slnx` project entry is a leaf: `<Project Path="..." />` is self-closing,
  so it never triggers a blank line.
- A `<Folder>` that holds projects is a container, and gets blank lines like any other element.
- The `xml` profile counts a wrapped leaf as *multi-line*, so it separates that leaf from its neighbors.
- No profile claims `.resx`, so a directory scan leaves it out.

To count a wrapped leaf as multi-line under a profile that lays out container elements only,
see [Making the scope uniform](#making-the-scope-uniform).

### Making the scope uniform

The profiles differ in two policies: the scope, which decides which elements count as multi-line,
and whether property elements are skipped, which only `xaml` does. Override the scope per file type:

```ini
[*.{csproj,vbproj,fsproj,vcxproj,props,targets,proj,projitems,shproj,slnx,xaml,axaml}]
stanza_markup_scope = all
```

Overriding the scope changes only how a wrapped leaf counts; `xaml` still skips property elements.
`stanza_markup_profile` swaps the profile for a glob section,
but a `stanza_markup_scope` entry still overrides that profile's scope.
`--profile` swaps the profile for a whole run and keeps that profile's scope,
so no `stanza_markup_scope` entry applies to the run.

## What it changes

- The [separating rule](https://github.com/Nickelony/Stanza/wiki/Terms), [`ZA0101`](https://github.com/Nickelony/Stanza/wiki/ZA0101-Separate-multi-line-markup-elements),
  and its [tight rule](https://github.com/Nickelony/Stanza/wiki/Terms), [`ZA0102`](https://github.com/Nickelony/Stanza/wiki/ZA0102-Keep-multi-line-markup-elements-together), are a pair;
  [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) says which half a file gets.
- The single-line tight rule, [`ZA0103`](https://github.com/Nickelony/Stanza/wiki/ZA0103-Keep-single-line-markup-elements-together), applies only where a file enables it.
- The blank-line cap, [`ZA0104`](https://github.com/Nickelony/Stanza/wiki/ZA0104-Collapse-extra-blank-lines-between-markup-elements), applies unless a file disables it.
- The comment-marker space, [`ZA0105`](https://github.com/Nickelony/Stanza/wiki/ZA0105-Separate-a-markup-comment-marker-from-its-text), applies unless a file disables it.
- A reprint also applies the documentation clean-ups, each only where its rule is enabled.
  [`stanza_markup_doc_reprint`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_doc_reprint) decides whether the member bodies are rewritten.
- A reprint gives no ZA0101 separation inside a body it rewrites, so `format --reprint` followed by a plain `check` can report files.
  Setting `stanza_markup_doc_reprint` in `.editorconfig` keeps the two consistent.

## What it never touches

Outside a reprinted `<member>` body, attributes, quoting, entities, namespaces,
the text after a comment marker and the byte order mark survive byte for byte,
because the tool never re-serializes the document.
A new blank line takes the line ending of the line below it, so a file keeps its own convention.

A [reprint](https://github.com/Nickelony/Stanza/wiki/stanza_markup_doc_reprint) rewrites only what sits between the tags of a `<member>`,
the documentation clean-ups included.

## What it leaves alone

- **Mixed content and preserved space.** A container with text of its own is skipped;
  `xml:space="preserve"` disables the blank line rules for a subtree until `xml:space="default"`.
- **Links.** A file that is a symlink is skipped with the warning "is a link and was left alone",
  because a write would replace the link.
  Only a link to a file that a profile claims and `.editorconfig` enables is reported, named or found by a scan.
- **Cloud placeholders.** A file that a sync client such as OneDrive keeps online only is not a link, so it is read like any other file.
- **Skipped directories.** A scan does not enter a linked directory, and it does not report one.
  It also skips the [directories the command reference lists](https://github.com/Nickelony/Stanza/wiki/Markup-command-reference#paths-and-globs).
  A linked directory named on the command line is scanned, and a file that two arguments reach through it is inspected once.
- **Large documents.** A document over 64 Mi characters of decoded text, or with more than 1 Mi elements, is refused rather than read.
- **Files without a length.** A file that states a length of 0, as an empty file, a pipe or a device does, is read as empty without being opened,
  because opening a pipe blocks until a writer does. An `.editorconfig` that states no length is skipped the same way.
- **Layout.** Outside a [reprinted](https://github.com/Nickelony/Stanza/wiki/stanza_markup_doc_reprint) `<member>` body,
  the tool never splits a line; two elements that share a line are left to a formatter.
- **Anything that is not whitespace.** A gap is collapsed only when it holds nothing else:
  a comment inside it, a non-breaking space or a Unicode separator leaves it as it is.
  A form feed is not legal XML, so the file is refused.
- **The first child.** Nothing is ever inserted before the first element of a container.
- **Comments and PIs.**
  A comment or processing instruction directly above an element belongs to it,
  so the blank line goes above that comment; one already separated by a blank line stays as a section marker.
- **A document it cannot read.** A file that is not well formed is reported, not rewritten;
  an external entity is never resolved, and an internal-DTD entity is expanded only up to 1 Mi characters.

## Bodies a reprint leaves as written

A [reprint](https://github.com/Nickelony/Stanza/wiki/stanza_markup_doc_reprint) leaves a member body as written when one of these holds:

- The body holds `&`, a CDATA section, a comment or a processing instruction.
- The member, or an element inside it, is under `xml:space="preserve"`.
- The body contains a `<code>`, `<include>` or `<inheritdoc>` element.
- The start tag does not end its line, or the end tag does not stand on a line of its own.
- An element sits 32 levels or more below the member.
- A blank line sits inside a nested element other than a `<para>`.
- An inline element that is a direct child of the member holds a list or more than one paragraph,
  or a `<para>` holds a list.
- A `<list>` holds text outside its items, an empty item, a `<para>` inside an item, or a blank line inside an item.
- An element has no columns left for its text.
- The body is empty.
- The file breaks its lines with a bare carriage return and no line feed.

## Writing back

A write goes through a sibling temp file that replaces the original,
so an interrupted run cannot leave half a document behind.
That needs write access to the *directory*, not only to the file.
On Windows the replace keeps the original's access list and alternate streams.
A read-only file is never replaced, and neither is a file that changed after the run read it, on any system.
The run reports either as not written, as [Failures and warnings](#failures-and-warnings) describes.

A hard kill between the write and the replace leaves the temp file, `<start of name>.<random>.stanza-tmp`, beside the original.
The start is the first 32 characters of the original's name, so a name of any length the file system accepts can be written.
No profile claims that extension, so a scan passes it by unless a profile key, or a glob given with `--profile`, claims it.

A replace that fails after the original is gone moves the temp file into its place, so the new text is never lost.
If that move fails too, the error names the temp file, which keeps the new text.

The replace creates a new file, so another hard link to the original keeps the old text.
On Unix the new file keeps the original's mode but not its owner: it belongs to the user who ran the tool.

## Failures and warnings

The [command reference](https://github.com/Nickelony/Stanza/wiki/Markup-command-reference#exit-codes) lists the exit codes; this section lists the problems that fail a run.
A warning leaves the run passing, and `--strict` makes each warning below an error, which fails it.

- A file the run cannot read or parse is a warning.
  Under `--format msbuild` it is reported as [`ZA0100`](https://github.com/Nickelony/Stanza/wiki/ZA0100-Report-a-file-that-could-not-be-read-or-parsed).
- A file that `format` has changes for but will not write back in the encoding it declares is a warning, and is left as it was;
  [Markup encoding](https://github.com/Nickelony/Stanza/wiki/Markup-encoding) says which declarations those are.
- A directory scan that collects no file, or a glob whose matches are all excluded, disabled or claimed by no profile, is a warning.
- A file that is a symlink is skipped with a warning; a directory that is a symlink or a junction is skipped without one.
- A directory the scan cannot list is a warning, and one scan reports all of them as a single line:
  it names the first and counts the rest, so a glob rooted at a drive stays readable.
- An `.editorconfig` that cannot be read, or that sets a `stanza_markup_*` or documentation key to a value the tool cannot read, is a warning.
- A glob or a directory scan skips a file that `.editorconfig` disables silently, unless nothing is left to inspect,
  and then it is a warning.

A file given by name that `--exclude` covers, that `.editorconfig` disables, or that no profile claims, is skipped with a `note:` line,
even when it is a link. `--strict` never makes a note an error, so a hook that passes the list of changed files keeps passing.

These problems fail the run in every mode:

- A path that does not exist, and a command-line glob that matches no file or that the dialect refuses.
  [Markup globs](https://github.com/Nickelony/Stanza/wiki/Markup-globs) lists what the dialect refuses.
- A defect of the tool, a layout it would write broken, is an error in every mode, and the file is left as it was.
- A file whose write fails with an I/O error fails the run in every mode.
  The error is a plain `error: <path> could not be written: <reason>` line that carries no code,
  because `--format msbuild` applies only to `check`, which never writes.

