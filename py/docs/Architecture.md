# Architecture

Python Stanza reads a file once, assigns each gap between neighboring statements and each call to one rule,
and writes only blank lines and the parameter names of calls.
This page records why it is built that way.

## Engine

The tool uses the standard `ast` and `tokenize` modules and no third-party parser.

- A fix inserts or deletes whole lines, or inserts a name into a line,
  so a lossless concrete syntax tree such as the one of `libcst` would add a compiled dependency and nothing these edits need.
- `ast` gives the first and last line of every statement and `tokenize` gives every comment, which is all a gap needs.
  A name also needs the column of the token that starts an argument, which `tokenize` gives too.
- The tool runs under the interpreter of the project, so it parses the syntax that interpreter parses.
  A file that uses newer syntax than the interpreter is an error for that file.
- After every pass the fix parses the new text and compares it with the *expected tree*:
  the tree it read, with the positional tail of each named call moved into leading keywords.
- A blank line keeps the tree, so a pass whose tree differs in any other way is refused.

## Shared analysis

`nickelony_stanza.analysis` reads a file into containers, units and gaps once, whatever number of gap rules are enabled.
The call rules read the file through the [call walker](#call-rules) instead.

- A **container** is a list of sibling units: a module, a class body, a function body, the body of any compound statement, or the cases of a `match`.
  The last statement of a block and the keyword that continues its statement, such as `else`, form a container of two units.
- A **header container** holds the last line of a block header and the first unit of its body, so a rule can read the gap below the header.
- A **unit** is a statement, a `case`, the line of a clause keyword or of a block header, or the shebang and encoding lines that open a file.
  A decorated definition starts at its first decorator.
- A **gap** is the space between two units.
  It records the comments that lead the next unit, whether a blank line stands in it, whether it is joined, and why it is restricted.
- A **claim** is the question a rule asks of a gap. It reads the tree and the comments, so the answer survives any fix;
  only `ZA3009` and the rules that gather a loose run also read whether a blank line already stands in a gap.

The session in `analysis/session.py` asks every enabled gap rule about every gap,
and gives the gap to the first rule in the [owner order](Rule-map.md#one-owner-per-gap) that says yes.
A rule that is off asks nothing, so its gaps pass to the next rule that asks for them.

## Roles

A rule writes a blank line or removes one, and the role decides what it owns and who wins a gap two rules ask for:

| Role | Rules | Owns | Against a rule that writes |
| --- | --- | --- | --- |
| `separate` | The rules that add a blank line. | A gap with no blank line. | Reports the gap in the owner order. |
| `keep` | The rules that keep two units apart: `ZA3202`, `ZA3203`, `ZA3112` and `ZA3113`. | A gap that holds only blank lines. | Wins, so no `separate` rule sees the gap as asked for. |
| `keep-run` | The rules that keep or gather a run: `ZA3052` to `ZA3055`. | A gap that holds only blank lines. | Yields every gap that a `separate` rule asks for. |

- `ZA3202` and `ZA3112` are the tight halves of the [pairs](Rule-map.md#pairs-and-their-default-halves), and `ZA3113` and `ZA3203` are in no pair.
- Naming one half of a pair in `enable` replaces the other half at a level where `Rule.opposite` links them.
- A `keep-run` rule reads the claims of the `separate` rules, and a gap they ask for is a gap it leaves blank.
- The claims of a `separate` rule are shape-only except for `ZA3009`,
  so removing the blank lines inside a run changes no answer of the rules that write the lines at its ends.
- `ZA3051` is a `keep` rule that the session handles on its own, because it judges a whole container.

## Restricted gaps

A gap is restricted when a formatter or a convention owns it, and only the rule named for that restriction may ask for it:

| Restriction | Gap | May ask |
| --- | --- | --- |
| `definition` | Beside a function or class, below a block that ends in one, or below the header of a block that is no function or class and opens with one. | No rule. |
| `module-header` | Below the shebang, the encoding line or the module docstring. | `ZA3017` |
| `class-docstring` | Below a class docstring. | `ZA3013` |
| `function-docstring` | Below a function docstring. | No rule. |

The gap before a clause keyword is `definition` when the block above ends in a function or class, because the formatters write a blank line there.

A function docstring above exactly two statements does not count as a unit,
so `ZA3051` judges the two statements as a pair and the other rules ask for nothing in that container.

## Fixes

- A gap finding carries one line edit: insert a line ending above the first line of the next unit's leading comments,
  or delete the blank lines of a gap.
- A call finding carries one insertion for each argument it names, and an insertion writes `name=` at a column of a line.
- The edits of one pass never overlap, because each gap and each call has one owner, so the engine applies them together,
  last place first, so the lines and columns above stay true.
  An insertion on a line another fix deletes is an error.
- A fix to a file whose tree is too deeply nested to compare, such as an expression of 1,200 terms, is an error, and the file is left as written.
- The engine fixes in passes until no finding is left. Every file in the tests settles in one pass.
- A pass keeps the line endings and the encoding of the file.
- The tool writes a fixed file through a temporary file beside it, so a failed write leaves the original, and the file keeps its mode.

## Silencing

- A gap finding is dropped before it becomes an edit when a `# noqa` comment names its code
  on the first line of the unit below the gap or on the line the blank would go above.
- A call finding is dropped when the comment stands on the first line of the call's statement, on the line where the call starts
  or on the line of the `(` that opens its arguments.
- The gap or the call keeps its owner, so no other rule reports it.

## Call rules

A call rule writes the name of a parameter before an argument, and it claims no gap, so it has no role and sits outside the owner order.

### Edits and the walker

- A line edit deletes lines and then inserts text before the first of them, and an insertion writes text at a character column of a line.
  The call rules write insertions only, one for each argument they name.
- `analysis/calls.py` lists the calls of a file in source order, each with the first line of its statement.
- The walker skips a call inside a `# fmt: off` region or one that a trailing `# fmt: skip` comment covers,
  as the [comments table](Quirks.md#comments) says.
- The walker skips a call inside an f-string before Python 3.12, whose tokens it cannot read.
- The walker tokenizes the file to find the `(` of a call and the first token of each argument.
  `ast` counts columns in UTF-8 bytes, so each position is converted to a character column.
- A name goes after the `(` or `,` in front of its argument, so a parenthesized argument takes it outside the parentheses.
- Python allows no positional argument after a keyword, so a rule names every argument from the first one it names to the last.
  A call is left alone when one of them cannot take a name, such as a positional-only parameter or an unparenthesized generator.

### The owner of a call

The walker asks the call rules in `CALL_RULES` order, and the first rule that plans names for a call owns it.
A `# noqa` that silences the owner leaves the call as written, and the other rule does not take it over.
`named-argument-ignore` removes a call before any rule judges it.

### The resolver

The resolver reads which parameter each argument fills, from source alone and without importing or running any code.
`binding.py` returns the parameters of the callee or the reason it leaves the call alone, and `resolver.py` settles the callee.

A name resolves when exactly one statement binds it outside every `if`, `try`, `with`, loop and `match`,
and no `global` statement or later star import may rebind it.
`classes.py` reads the method resolution order and the constructor of a class, `scopes.py` the names each scope binds, and `shapes.py` the parameters.

### Sources and skipped calls

`module_index.py` follows an import or a re-export through the directories of the project roots, then those of `sys.path`, and never imports a module.
The project roots are the `project` source, the standard library is `stdlib`, and the other directories of `sys.path` are `installed`.
A top-level name that the roots and the standard library both define is unreadable, because the interpreter may have loaded the standard one first.

A gap in what the resolver sees leaves the call unresolved, so a skip reason names the gap:

- The callee is a value, a name that is rebound or bound conditionally, or a builtin or compiled definition.
- The module is missing, compiled, sourceless or shadowed, a `.pyi` stub stands beside it, or the options leave out its source.
- A decorator, a class shape or a call shape that the resolver does not model stands in the way.
- The callee is a form a type checker reads positionally, or a project module replaces it with a test double.
- The method is not allowed by `named-argument-methods`, or the dispatch condition below fails.
- The method is a dunder other than the few that construct or call an object, whose parameters are read as positional-only.
- The project passes the caps of the scan, so no check that reads every module can clear the call, as [the project index](#the-project-index) says.

The [rule page](rules/ZA3501-name-call-arguments.md#what-it-names) lists each skipped callee and the caps.

### The project index

`Projects` in `analysis/projects.py` hands each file the index of its roots: the `source-roots` key, else the roots detected from the file.
Detection takes the directory of the nearest `pyproject.toml`, `setup.py` or `setup.cfg`, its `src` directory,
and the directory above the outermost package that holds the file.
A directory without `__init__.py` inside a package belongs to that package, so it does not end the climb.

The checks for test doubles and for subclasses need every module of the project, so the index scans its roots once:

- A root that holds `pyproject.toml`, `setup.py` or `setup.cfg`, its `src` directory, and every root `source-roots` names are read as a whole tree.
- Any other root is read flat: its own `.py` files, plus the regular packages directly under it, each read whole.
- The home directory and a filesystem or drive root are read flat alone, because a tree that large is no project.
  The `source-roots` key refuses them, as the [configuration page](Configuration.md#keys) says.
- The scan never follows a symlink or junction.
  It skips the directories a [directory scan](Configuration.md#command-line) skips, every dot-directory, `site-packages`
  and every directory that holds `pyvenv.cfg`, so caches, builds and environments stay out.
- Past the file and size caps that the [rule page](rules/ZA3501-name-call-arguments.md#what-it-names) gives, the scan stops and every call is left alone,
  because a check that misses a module could clear a call wrongly.
- `check`, `fix` and `format` then print one note on stderr for the run, which changes neither the output nor the exit code.
  The flake8 plugin stays silent.

The index keeps its modules from file to file:

- Files of one set of roots share one index, so a run parses each module once:
  a file whose text equals the version the index holds is checked through that tree, and its scope table is built once.
- The first call a call rule resolves makes the index read every module of the project,
  so a strong run reads the whole project before it names a call.
- A text that differs from the disk replaces the disk version for the rest of the run, and the caches built from the old version go with it.
- A text that differs only by blank lines, or by arguments of calls inside function bodies that a fix passed by name,
  keeps the version the index holds and its caches, because the resolver reads neither.
- A name written into a call outside a function body replaces the version, because the call may feed a definition, a binding or a stored attribute.
- A text without a path has no roots, so it resolves against itself, the standard library and the installed packages.

### The dispatch condition

A call through `self`, `cls` or `super()` dispatches on the class of the caller, so the resolver names it only when the dispatch is safe:

- The method is found through the method resolution order of the class, and `super()` through the classes after it.
- Every project class that inherits the class must reach the same definition, or the call is left alone.
- The check reads project classes only, because it assumes no code outside the roots subclasses a project class,
  and it relies on the [scan](#the-project-index) having read every one.
- An attribute that any project module assigns under the method's name, such as `self.name = ...`, also refuses the call.
