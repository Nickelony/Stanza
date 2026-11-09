# Rule map

Each C# rule of Stanza is mirrored, adapted or left out of JS Stanza, and this page says which and why.

The JS rules lay out blank lines, because Prettier keeps at most one blank line between statements and adds none,
except the import groups that `@ianvs/prettier-plugin-sort-imports` adds.
It also keeps a blank line the author wrote between statements, members and the elements of a list, and removes the blank lines between declarators.

Everything else in the C# families is layout, a size limit or a code rewrite, and Prettier or ESLint core owns most of it;
the rows below name the gaps.

## Naming

A rule is named after the intent of the C# title, in the imperative: `separate-declaration-runs` mirrors `ZA0002 - Separate declaration runs`.
A rule has no number, and its page names its C# counterpart.
The plugin registers the rules as `stanza/<rule-name>`.

## Mirrored and adapted

| C# rule | JS rule | Level | Difference |
| --- | --- | --- | --- |
| `ZA0001` Separate multi-line constructs | [`separate-multi-line-constructs`](rules/separate-multi-line-constructs.md) | moderate | Judges the statements of a block and the members of a type, and leaves the cases of a `switch` statement to the switch case rules. |
| `ZA0111` Separate multi-line switch sections | [`separate-multi-line-switch-cases`](rules/separate-multi-line-switch-cases.md) | moderate | The separating half of the [pair](#pairs-and-their-tight-halves) with `ZA0112`. An empty `case` that falls through stays with the next one. |
| `ZA0112` Keep multi-line switch sections together | [`keep-multi-line-switch-cases-together`](rules/keep-multi-line-switch-cases-together.md) | opt-in | The tight half of the pair with `ZA0111`. Like the C# rule, it belongs to no preset. |
| `ZA0113` Keep single-line switch sections together | [`keep-single-line-switch-cases-together`](rules/keep-single-line-switch-cases-together.md) | minimal | Prettier writes the body of a case on its own line, so the rule matters for a project that runs no formatter. |
| `ZA2001` Separate multi-line markup elements | [`separate-multi-line-jsx-children`](rules/separate-multi-line-jsx-children.md) | strong | Judges the children of a JSX element, and leaves an element with text or a `{' '}` among its children as written, because Prettier removes its blank lines. The separating half of the [pair](#pairs-and-their-tight-halves) with `ZA2002`. |
| `ZA2002` Keep multi-line markup elements together | [`keep-multi-line-jsx-children-together`](rules/keep-multi-line-jsx-children-together.md) | opt-in | The tight half of the pair with `ZA2001`. Like the C# rule, it belongs to no preset. |
| `ZA2003` Keep single-line markup elements together | [`keep-single-line-jsx-children-together`](rules/keep-single-line-jsx-children-together.md) | strong | Prettier keeps a blank line the author wrote between the children of an element without text. A comment above a child keeps the blank line above it, where the C# rule removes it. |
| `ZA2004` Separate a commented markup element | [`separate-commented-jsx-children`](rules/separate-commented-jsx-children.md) | strong | A `{/* note */}` comment leads the child below it. It never meets `ZA2003`, which leaves the blank line above a comment alone. |
| `ZA0002`, `ZA0003` Separate declaration runs and same-type declaration runs | [`separate-declaration-runs`](rules/separate-declaration-runs.md) | moderate | The kinds are `const`, `let`, `var`, `using`, `await using` and `type`, and always split a run. `ZA0003` is folded in, because the rule reads the keyword and no declared type. |
| `ZA0004`, `ZA0007` Separate invocation runs and event subscription runs | [`separate-invocation-runs`](rules/separate-invocation-runs.md) | moderate | A call is one helper when its callee path is the same, such as `console.log`. `ZA0007` is folded in, because `addEventListener(...)` and `emitter.on(...)` runs are runs of one callee path. |
| `ZA0005` Separate assertion runs | [`separate-assertion-runs`](rules/separate-assertion-runs.md) | moderate | `settings.stanza.assertionRoots` lists the roots, such as `expect`, `assert` and `t`. |
| `ZA0006` Separate await runs | [`separate-await-runs`](rules/separate-await-runs.md) | strong | An await statement is `await call();`; a declaration or assignment that awaits belongs to its own rule. |
| `ZA0008` Separate assignment runs | [`separate-assignment-runs`](rules/separate-assignment-runs.md) | strong | An assignment is `=`, a compound operator, an increment or a decrement. It has no option to split runs by target, because the C# default does not split and the split reads a symbol. |
| `ZA0009` Separate a jump statement | [`separate-jump-statements`](rules/separate-jump-statements.md) | strong | Judges `return`, `throw`, `break` and `continue`; the `break` that closes a case is left alone. |
| `ZA0010` Separate the base call | [`separate-base-call`](rules/separate-base-call.md) | strong | The base call is a `super(...)` or `super.method(...)` statement that opens or closes the body of a constructor, a method, an accessor or an object method. |
| `ZA0011` Separate the guard clauses | [`separate-guard-clauses`](rules/separate-guard-clauses.md) | strong | A guard also includes a call of a helper that `settings.stanza.guardRoots` lists, such as `invariant(...)`, which `separate-base-call` reads too. |
| `ZA0012` Separate a commented sibling | [`separate-commented-siblings`](rules/separate-commented-siblings.md) | strong | A tool directive such as `// @ts-expect-error` is not a heading and does not count. |
| `ZA0051` Keep the two units of a container together | [`keep-two-units-together`](rules/keep-two-units-together.md) | moderate | Removes a blank line from a container of two units. It never meets the rules that add one, because they ask for nothing in its containers. |
| `ZA0013` Separate member groups | [`separate-member-groups`](rules/separate-member-groups.md) | moderate | Also judges interface bodies and type literals. |
| `ZA0014` Separate modifier groups | [`separate-modifier-groups`](rules/separate-modifier-groups.md) | strong | Groups by access, `static` and `readonly`; a `#name` member is private. It needs no option for the order, because a fix never moves a member. |
| `ZA0052` Gather a run of loose statements | [`gather-loose-statements`](rules/gather-loose-statements.md) | strong | Judges the statements of a block, a switch case, a static block, a module block and a file. The limit is `settings.stanza.maxLooseRunSize`, which `gather-loose-members` reads too. |
| `ZA0053` Gather a run of loose members | [`gather-loose-members`](rules/gather-loose-members.md) | strong | Judges the members of a class, an interface and a type literal. |
| `ZA0054` Keep a run of statements together | [`keep-statement-runs-together`](rules/keep-statement-runs-together.md) | opt-in | Judges the runs of the statement rules that are enabled. Like the C# rule, it belongs to no preset. |
| `ZA0055` Keep a run of members together | [`keep-member-runs-together`](rules/keep-member-runs-together.md) | opt-in | Judges the runs of the member rules that are enabled. Like the C# rule, it belongs to no preset. |
| `ZA0131`, `ZA0141`, `ZA0151`, `ZA0161` Separate multi-line initializer members, collection elements, arguments and parameters | [`separate-multi-line-elements`](rules/separate-multi-line-elements.md) | strong | Judges object members, array elements, enum members, arguments and parameters, with one option for each. |
| `ZA0133`, `ZA0143`, `ZA0153`, `ZA0163` Keep single-line initializer members, collection elements, arguments and parameters together | [`keep-single-line-elements-together`](rules/keep-single-line-elements-together.md) | minimal | Prettier keeps a blank line the author wrote between these elements, so this rule removes what the formatter leaves. It has one option for each kind of list. |
| `ZA0015` Separate the import section | [`separate-import-section`](rules/separate-import-section.md) | minimal | The section is the leading `import` statements. |
| `ZA0016` Separate the import directive groups | [`separate-import-groups`](rules/separate-import-groups.md) | strong | The kinds are the value import, the side-effect import and, with the `typeImports` option, the type import. The option is off, because `@ianvs/prettier-plugin-sort-imports` removes the blank line unless `importOrder` holds a `<TYPES>` group. |
| `ZA0017` Separate a file-scoped namespace declaration from its first member | [`separate-directives`](rules/separate-directives.md) | minimal | The header of a JS file is its shebang and its directive prologue. |
| none | [`separate-test-blocks`](rules/separate-test-blocks.md) | moderate | Separates the `describe`, `it`, `test` and hook calls of a test file. |

## Left to ESLint core

Each row names the rule that does the work and what it leaves uncovered.
Stanza rewrites whitespace only, so a rule whose fix changes tokens, or reports without a fix, stays with its owner.

| C# rule | Owner and what stays uncovered |
| --- | --- |
| `ZA0132`, `ZA0142`, `ZA0152`, `ZA0162` Keep multi-line elements together | Declined. A list rule has one option for each kind of list, and the separating rule leaves a kind alone when its option is off. A project that wants the multi-line elements of a kind tight turns that option off, and `keep-single-line-elements-together` removes the blank lines between single-line elements. Four rules for the tight halves would only repeat the options. |
| `ZA0204` Collapse empty blocks | Declined. Prettier prints an empty `if`, `else`, `try`, `finally`, labeled, bare and `case` block on two lines, and joins the braces of an empty function, class, loop, object and `catch` body. A fix that joined the first kind would be expanded again by every format and never settle, and Prettier already joins the second. `no-empty` reports an empty block and writes no fix. |
| `ZA0205`, `ZA0206` Braces | ESLint `curly`, whose option picks the direction. `['error', 'multi-line', 'consistent']` braces every branch of a chain when one branch is braced or spans several lines, and a loop body that spans several lines, as `ZA0205` does. `['error', 'multi-or-nest', 'consistent']` removes the braces of a body that holds one single-line statement, as `ZA0206` does, and keeps those around a declaration and around an `if` that an `else` could join. `curly` reads one direction for a whole project, so it cannot do both rules at once, and its fix writes `{b();}` and leaves the layout to Prettier. |
| `ZA0301`, `ZA0302` Member bodies | Declined. `arrow-body-style` covers arrow functions only: `always` braces every arrow, and `as-needed` turns a one-return block into an expression body even when the expression wraps. A method, a function and a constructor have no expression body in JS, so there is nothing to convert, and both rewrites change tokens. |
| `ZA0601` Remove the else after a jump | `no-else-return` removes the `else` after a `return`, and reports nothing after a `throw`, `break` or `continue`. Those three stay uncovered, because moving the statements of an `else` out of it changes tokens, not whitespace. |
| `ZA0602` Merge nested if statements | `no-lonely-if` turns an `if` that is the only statement of an `else` block into `else if`. The merge of `if (a) { if (b) {} }` into one condition is uncovered, because no core rule reports it and the merge changes tokens. `sonarjs/no-collapsible-if` reports it, and it is not installed in `js/node_modules`. |
| `ZA1005` to `ZA1007` Size and depth | Report-only counts, with no edit to write. `complexity` counts every branch of a function, so it sets no limit per condition, per `else if` chain or per ternary chain, and `no-nested-ternary` reports any nested ternary where `ZA1007` counts the `?` operators. |
| `ZA2007` Separate a markup comment marker from its text | JSX has no `<!-- -->`. `spaced-comment` with `block: { balanced: true }` writes the space after the opening `/*` and before the closing `*/` of `{/* note */}`, and without `balanced` it writes only the first. Comment text is not whitespace between units, so Stanza leaves it. |
| `ZA2010` Write an empty markup element as a self-closing tag | `react/self-closing-comp` of `eslint-plugin-react`, which is not installed in `js/node_modules`. Prettier keeps `<div></div>` as written, and the rewrite changes tokens. |

## Not needed

| C# rule | Owner in the JS toolchain |
| --- | --- |
| `ZA0056` Collapse extra blank lines | Prettier, which keeps at most one blank line. |
| `ZA0101` to `ZA0103` Accessors | No JS counterpart. A JS getter and its setter are two members of a class or an object, not an accessor list, so the member rules judge them like any member, and a getter stays with its setter, as the [quirks](Quirks.md#gaps-no-rule-owns) say. |
| `ZA0121` to `ZA0123` Switch expression arms | No JS counterpart. JS has no switch expression, and the cases of a `switch` statement have the rules of `ZA0111` to `ZA0113`. |
| `ZA0171` to `ZA0173` Declarators | Prettier, which removes every blank line between the declarators of one declaration. `ZA0171` would write a blank line that the next format removes, so it could never settle, and `ZA0172` and `ZA0173` have nothing left to remove. |
| `ZA0198`, `ZA0199` Trailing comma | Prettier `trailingComma`. |
| `ZA0201`, `ZA0202` Clause keywords, `ZA0203` Brace padding | Prettier. |
| `ZA0303`, `ZA0304` Getter bodies, `ZA0307` Arrow placement | No JS counterpart. Getters always have a block body, and espree rejects a line break before `=>`. |
| `ZA0305`, `ZA0306` Break and join an arrow body | Prettier, which breaks after a long `=>` and joins a line break before `=>`. |
| `ZA0401` to `ZA0405` Regions and conditional blocks | No JS counterpart. |
| `ZA0501` to `ZA0503` Named arguments | No JS counterpart. |
| `ZA0651`, `ZA0652` Null checks | `eqeqeq` with `{ "null": "ignore" }` keeps `== null`, which also matches `undefined`. Without `ignore`, it reports a null check and writes no fix. |
| `ZA0901` to `ZA0914` Documentation comments | `eslint-plugin-jsdoc` and `eslint-plugin-tsdoc`. |
| `ZA0951`, `ZA0952` Comment markers | ESLint `spaced-comment` or `@stylistic/spaced-comment`. By default it rewrites `///Text` as `// /Text`, changes banners such as `//-----` and writes no space before `*/`; the `markers`, `exceptions` and `balanced` options fix those. |
| `ZA0953` Trailing comment | Prettier. |
| `ZA1001` to `ZA1004` Size and depth | ESLint `max-len` (ZA1001), `max-statements` (ZA1002), `max-params` (ZA1003) and `max-depth` (ZA1004). `max-len` also reports a long string unless `ignoreStrings` is set, and `max-statements` counts statements as ZA1002 does, where `max-lines-per-function` counts lines. |
| `ZA2005`, `ZA2006`, `ZA2008`, `ZA2009` Markup | Prettier, which removes the padding and the extra blank lines of JSX and writes the spaced slash, so the tight form is not kept. |

## Pairs and their tight halves

A pair is a rule that adds a blank line beside a multi-line unit and its tight rule, which removes it.
A preset enables the separating half, and the tight half belongs to no preset.

| Gap | Separating half | Tight half |
| --- | --- | --- |
| Two cases of a `switch` statement, when either spans several lines | `separate-multi-line-switch-cases` | `keep-multi-line-switch-cases-together` |
| Two children of a JSX element, when either spans several lines | `separate-multi-line-jsx-children` | `keep-multi-line-jsx-children-together` |

- An entry that enables the tight half over a preset makes it the owner of the gaps it judges, and the separating half asks for none of them.
  Setting the separating half to `'off'` gives the same text.
- ESLint cannot tell an entry of a preset from an entry of the project, so enabling both halves gives the tight half.
  The C# analyzers give the default half when a file names both.
- The list rules have no pair, as the `ZA0132` row says.

## One owner per gap

A gap is the space between two neighboring units of a container, such as two statements of a block.
Several rules can ask for a blank line at one gap, so a [shared analysis](Quirks.md#one-owner-per-gap) assigns the gap to the first enabled rule in this order:

1. `separate-directives`
2. `separate-import-section`
3. `separate-import-groups`
4. `separate-multi-line-constructs`
5. `separate-multi-line-switch-cases`
6. `separate-multi-line-jsx-children`
7. `separate-multi-line-elements`
8. `separate-commented-siblings`
9. `separate-commented-jsx-children`
10. `separate-test-blocks`
11. `separate-base-call`
12. `separate-guard-clauses`
13. `separate-jump-statements`
14. `separate-member-groups`
15. `separate-modifier-groups`
16. `separate-declaration-runs`
17. `separate-invocation-runs`
18. `separate-assertion-runs`
19. `separate-await-runs`
20. `separate-assignment-runs`

A rule that is off hands its gaps to the next rule that asks for them, as in the C# analyzers, and the run rules follow the order of their C# IDs.
Only the report moves, because the rules of this list write the same blank line, so the order never changes the fixed text.
`keep-two-units-together` is outside the list: the rules above ask for nothing in a container it judges.

## Rules that remove a gap

The rules that remove a blank line are outside the list, because they never ask for a gap.
They remove a blank line only where no enabled rule of the list asks for one,
so the two kinds of rule never write and remove the same gap.

The first of these rules reports a blank line that several of them remove, in the order
`keep-single-line-elements-together`, `keep-single-line-jsx-children-together`, `keep-multi-line-jsx-children-together`,
`keep-single-line-switch-cases-together`, `keep-multi-line-switch-cases-together`, `keep-statement-runs-together`,
`keep-member-runs-together`, `gather-loose-statements`, `gather-loose-members`.
The statement rule and the member rule of a pair judge different containers, so a gap meets at most one rule of each pair.
The gather and keep rules of a run remove the blank lines of the whole run in one fix.
