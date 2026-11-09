# Conventions

The rules the code keeps, so a change and its review are judged against one list.
A guard test enforces each rule whose **Kept by** column names it; a reviewer checks each rule marked *review*.

- A rule changes only through an edit to this file.
- It governs the C# projects; `js/`, `py/` and `site/` are held by their own linters and tests.
- Documentation follows [STYLEGUIDE.md](STYLEGUIDE.md), and [Terms](docs/Terms.md) defines the words the pages use.

## Architecture

| Rule | Kept by |
| --- | --- |
| **A1** No rule family's directory references another family, and the analyzer root references none. | `ConventionGuardTests` |
| **A2** A type at the analyzer root is an analyzer base, or is used by two or more families or by another root type. A type one family uses lives in that family. | `ConventionGuardTests` |
| **A3** `Markup.Core` references neither the analyzers nor a tool, and a tool references no analyzer assembly; the analyzers may still run over their sources. | `ConventionGuardTests` |
| **A4** An analyzer derives from `StanzaNodeAnalyzer`, `StanzaTreeAnalyzer`, `StanzaCompilationTreeAnalyzer` or `StanzaRuleAnalyzer`. A node analyzer whose rule has a `StanzaRules` entry, and any analyzer whose entry names node kinds, derives from `StanzaRuleAnalyzer` and reads its entry through `Rule`. | `RuleRegistryTests` |
| **A5** An analyzer reaches its descriptor through `Descriptor`, and names a `DiagnosticDescriptors` field only in its base constructor call. | `ConventionGuardTests` |
| **A6** A rule reads another rule's state only through `GetDeferral` on its own `StanzaRules` entry, or through its `RulePair`. The one exception is `BlankLineOwnership.ReadAskedGroupingBorders`, which every grouping rule reads to order two fixes: a deferral there would hand the gap over, and no rule gives up a gap by that read. A read of a deferral its rule always records fails loudly when the entry omits it, through `GetDeferral` or `IsRequiredDeferralOnAt`; a rule that may not defer is read through `IsDeferralOn`, `IsDeferralOnAt` or `FindJudgingDeferral`. | review |
| **A7** One provider fixes each rule. | `RuleRegistryTests` |
| **A8** A diagnostic carries data to its fix only through `FixOptions`, which alone writes and reads its properties. | `ConventionGuardTests` |
| **A9** Code that builds the text changes a fix applies, such as `BraceEdits`, lives in its family directory of the analyzer project; the fix project holds only providers and what they share. | review |
| **A10** A path a tool passes to another program, where it could start with `-` or `@`, takes a `./` prefix or follows a `--`, so it is never read as an option. A git revision that starts with `-` is refused, and a `--` ends the revisions, so a file named like one is never read as a revision. Git output a tool parses is asked for with `-z` and with color off, which `-c color.ui=never` does except for a command with a color key of its own, such as `git grep`, that takes `--no-color`. | review |
| **A11** Each directory of the engine and the tools uses only the directories before it, in the order [Architecture](docs/Architecture.md) gives. | `ConventionGuardTests` |
| **A12** A type's namespace is its project's root namespace plus its directory path. The linked `src/Shared` and `tests/Shared` keep one namespace each, and `IsExternalInit` keeps the compiler's. | `ConventionGuardTests` |

## Names

A method's first word says what it does, and the verb promises a return shape:

| Verb | Means | Returns |
| --- | --- | --- |
| `Get` | a value that always exists, computed or cached | a value, never null |
| `Read` | derives a value from its input, such as text, a tree, a file or an option key, without a cache; a `Get` of the same name caches it | any shape |
| `Find` | searches, and comes back empty when nothing matches | null, `default`, an empty list or `-1` |
| `Resolve` | settles a value from several sources, such as a key chain and its default | any shape |
| `Collect` | gathers matches into the list it returns or is given | a list, or nothing |
| `Build` | assembles a value or text from parts | a value, null when the parts make none |
| `Create` | builds the text changes a fix applies | a change, or a list of changes |
| `Report` | reports diagnostics | nothing |
| `Try` | does work that can decline, and says whether it did | `bool` |
| `Is`, `Has`, `Can`, or a third-person verb such as `Fits` | answers a question and changes nothing | `bool` |
| `To`, `From`, `For`, `With` | converts or derives a value | a value |

| Rule | Kept by |
| --- | --- |
| **N1** A method name starts with a verb, and a verb the table lacks, such as `Detect`, promises no shape. A search for an index is a `Find`, a `bool` question may start with its subject, as `ArrowPaysForItsLine` does, and a generated regex names what it matches. | `ConventionGuardTests` |
| **N2** A method returns the shape its verb promises, and only a `Try` method or a question returns `bool`. | `ConventionGuardTests` |
| **N3** A `Create` method names the change it builds, without a `Change` or `Changes` suffix. | `ConventionGuardTests` |
| **N4** The `Stanza` prefix is for a type at the root of the analyzer or fix project, and no type in a family directory takes it; such a type names its family's subject instead, such as `BodyArrows`. | `ConventionGuardTests` |
| **N5** An analyzer takes its descriptor's name, which is the words of its rule title without articles and `its`; a fix provider is `<Family>CodeFixProvider` for the directory of a rule it fixes; a base class carries no `Base` suffix. | `AnalyzerStructureGuardTests`, `ConventionGuardTests` |
| **N6** A parameter of a common Roslyn type takes its standard name: `sourceText`, `syntaxTree`, `semanticModel`, `cancellationToken` or `compilationOptions`. An override keeps the names of the member it overrides, and a record parameter the name of its property. | `ConventionGuardTests` |
| **N7** A `position` is a place in a `SourceText`, an `offset` is a character index into a string the code holds, a `column` counts display width, and an `index` counts items or lines. A report's column is the 1-based character column MSBuild names. | review |
| **N8** A rule that reports nothing at a place is `off` there; `disabled` means a setting turned it off, and `silenced` that a pragma did, as [Terms](docs/Terms.md) says. | review |
| **N9** A record of option values is named `<Area>Options`; a record of what a profile decides, such as `MarkupPolicy`, is named *policy* as [Terms](docs/Terms.md) says. A type that is not such a record takes neither "Options" nor "Policy" in its name, unless it derives from a Roslyn options type. | review |
| **N10** `LayOut` is the verb and `Layout` the noun. | review |
| **N11** An `Edit` is a change with the place it reports at, and a `Change` is the text change alone. A family's `<Family>Edits` helper keeps that name whichever of the two it returns. | review |
| **N12** A place that holds files is a *directory* in a name, a message and a doc; only a solution's `<Folder>` element and Microsoft's `dotnet_style_namespace_match_folder` key keep their own word. | `ConventionGuardTests` |
| **N13** An empty string in `src` is `string.Empty`, unless a constant is required, as by a pattern, a default value, an attribute argument or a `const`. | `ConventionGuardTests` |

## Tests

| Rule | Kept by |
| --- | --- |
| **T1** A test pins behavior a user or a caller sees, and fails when that behavior breaks. A new guard or contract test has been seen failing. | review |
| **T2** A test of one rule lives in `<Stem>Tests.cs`, the rule's analyzer name without `Analyzer`. A test marked `[CrossCutting]` drives a rule for another behavior and is exempt from T2 and from the fixed-text check of T4. | `RuleTestCoverageTests` |
| **T3** A test's name is a sentence that states the behavior. | review |
| **T4** A test that runs a fix pins the text the fix writes, in a constant named `fixedSource`, or in the template each row of a generated contract fills. | review; `RuleTestCoverageTests` checks that the text is read |
| **T5** A test asserts behavior, never a constant or a fact of the platform. | review |
| **T6** A test in `Analyzers.Tests` that cannot run ends `Inconclusive` from its body, never through a condition attribute such as `[OSCondition]`: `RuleTestCoverageTests` reads only a run in which every test started. | `ConventionGuardTests` |

## Complexity

| Rule | Kept by |
| --- | --- |
| **C1** An abstract class or an interface has at least two implementations. | `ConventionGuardTests` |
| **C2** Nothing exists that nothing sets or reads: no member, parameter or option, unless a comment names it as a test seam. | `ConventionGuardTests`, IDE0051, IDE0052, IDE0060 |
| **C3** A method that only forwards a call hides a cache, or names something the callee's name does not. | review |
| **C4** A cache states the work it saves, and a test or a measurement shows the saving. | review |
| **C5** A type, layer, option or parameter serves a need the code has today. | review |
| **C6** A method or constructor longer than 80 lines, counted from its signature to its closing brace, or with more than 6 parameters, does one job a split would not make clearer. A constructor over 6 parameters says in a single-line note on its type why the job needs them. | review |
| **C7** A type whose constructor only stores its parameters takes a primary constructor, unless a parameter's type is private to the type or a private constructor sends callers through the factory that checks what it stores. | review |
| **C8** Work a rule does for each node of a container stays linear in the container's size, so a lambda or a block of many calls cannot make a rule quadratic. | review |
