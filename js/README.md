# JS Stanza

*Where code becomes poetry.*

ESLint rules that lay out the blank lines of JavaScript and TypeScript code,
the sibling of the [Stanza C# analyzers](https://github.com/Nickelony/Stanza#readme).
The [terms page](https://github.com/Nickelony/Stanza/wiki/Terms) defines a stanza, a gap and a unit, the words the rules use.

Prettier keeps at most one blank line between statements and never adds one.
JS Stanza adds the blank lines Prettier leaves out, and a few rules remove the blank lines that spread a run or a list thin:
a fix inserts or removes whitespace between two neighboring units and never reorders code.

The package is not published yet.

## Install

```text
npm install --save-dev eslint-plugin-stanza
```

The plugin needs ESLint 9 or 10 and a flat config.
Load a preset after `eslint-config-prettier`, so the plugin and Prettier agree on whitespace:

```js
import prettier from 'eslint-config-prettier';
import stanza from 'eslint-plugin-stanza';
import tseslint from 'typescript-eslint';

export default [
	{ files: ['**/*.{ts,tsx}'], languageOptions: { parser: tseslint.parser } },
	prettier,
	stanza.configs.moderate,
];
```

Run `eslint --fix` to apply the fixes. A fix writes the same text in every order and a second run changes nothing.

The fixes add or remove blank lines between neighboring units, as [`separate-jump-statements`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-jump-statements.md) does here:

```ts
// Before
const order = load(id);
validate(order);
order.close();
return order;

// After
const order = load(id);
validate(order);
order.close();

return order;
```

## Presets

| Preset | Rules |
| --- | --- |
| `none` | Registers the plugin and enables no rule. |
| `minimal` | The rules of the file header: the shebang, the directives and the imports, and the removal of blank lines between single-line list elements and single-line switch cases. |
| `moderate` | `minimal`, plus the multi-line, switch case, run, member and test rules, and `keep-two-units-together`. This is the preset to start from. |
| `strong` | `moderate`, plus the import group, await, assignment, guard, base call, jump, comment, modifier, list and JSX child rules, and the rules that gather loose runs. |

An opt-in rule belongs to no preset, so only an entry of your own enables it:

```js
export default [prettier, stanza.configs.strong, { rules: { 'stanza/keep-statement-runs-together': 'error' } }];
```

## Rules

| Rule | Level | What it does | C# rule |
| --- | --- | --- | --- |
| [`separate-directives`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-directives.md) | minimal | Separates the shebang and the directive prologue from the code. | `ZA0017` |
| [`separate-import-section`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-import-section.md) | minimal | Separates the leading imports from the first statement. | `ZA0015` |
| [`keep-single-line-elements-together`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/keep-single-line-elements-together.md) | minimal | Removes the blank line between two single-line elements of a list. | `ZA0133`, `ZA0143`, `ZA0153`, `ZA0163` |
| [`keep-single-line-switch-cases-together`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/keep-single-line-switch-cases-together.md) | minimal | Removes the blank line between two single-line cases of a `switch` statement. | `ZA0113` |
| [`separate-multi-line-constructs`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-multi-line-constructs.md) | moderate | Separates a unit that spans several lines from its neighbors. | `ZA0001` |
| [`separate-multi-line-switch-cases`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-multi-line-switch-cases.md) | moderate | Separates a case of a `switch` statement that spans several lines. | `ZA0111` |
| [`separate-declaration-runs`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-declaration-runs.md) | moderate | Separates a run of declarations of one kind. | `ZA0002`, `ZA0003` |
| [`separate-invocation-runs`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-invocation-runs.md) | moderate | Separates a run of calls of one helper. | `ZA0004`, `ZA0007` |
| [`separate-assertion-runs`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-assertion-runs.md) | moderate | Separates a run of assertions. | `ZA0005` |
| [`separate-member-groups`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-member-groups.md) | moderate | Separates a run of members of one kind. | `ZA0013` |
| [`separate-test-blocks`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-test-blocks.md) | moderate | Separates the `describe`, `it` and hook calls. | none |
| [`keep-two-units-together`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/keep-two-units-together.md) | moderate | Removes the blank line between the only two single-line units of a container. | `ZA0051` |
| [`separate-import-groups`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-import-groups.md) | strong | Separates adjacent imports of different kinds. | `ZA0016` |
| [`separate-await-runs`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-await-runs.md) | strong | Separates a run of await statements. | `ZA0006` |
| [`separate-assignment-runs`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-assignment-runs.md) | strong | Separates a run of assignments. | `ZA0008` |
| [`separate-guard-clauses`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-guard-clauses.md) | strong | Separates the guard clauses that open a block. | `ZA0011` |
| [`separate-base-call`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-base-call.md) | strong | Separates a `super` call that opens or closes a constructor or method body. | `ZA0010` |
| [`separate-jump-statements`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-jump-statements.md) | strong | Separates a `return`, `throw`, `break` or `continue` from a stanza above it. | `ZA0009` |
| [`separate-commented-siblings`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-commented-siblings.md) | strong | Separates a comment that leads a single-line unit. | `ZA0012` |
| [`separate-commented-jsx-children`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-commented-jsx-children.md) | strong | Separates a comment that leads a single-line child of a JSX element. | `ZA2004` |
| [`separate-modifier-groups`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-modifier-groups.md) | strong | Separates a run of members that share their access, `static` and `readonly` modifiers. | `ZA0014` |
| [`separate-multi-line-elements`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-multi-line-elements.md) | strong | Separates a multi-line element of an object, an array, an enum, an argument list or a parameter list. | `ZA0131`, `ZA0141`, `ZA0151`, `ZA0161` |
| [`separate-multi-line-jsx-children`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-multi-line-jsx-children.md) | strong | Separates a multi-line child of a JSX element from its siblings. | `ZA2001` |
| [`keep-single-line-jsx-children-together`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/keep-single-line-jsx-children-together.md) | strong | Removes the blank line between two single-line children of a JSX element. | `ZA2003` |
| [`gather-loose-statements`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/gather-loose-statements.md) | strong | Removes the blank lines of a run of single-line statements that is longer than a limit. | `ZA0052` |
| [`gather-loose-members`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/gather-loose-members.md) | strong | Removes the blank lines of a run of single-line members that is longer than a limit. | `ZA0053` |
| [`keep-statement-runs-together`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/keep-statement-runs-together.md) | opt-in | Removes the blank lines inside a run of statements that the run rules read as one. | `ZA0054` |
| [`keep-member-runs-together`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/keep-member-runs-together.md) | opt-in | Removes the blank lines inside a run of members that the member rules read as one. | `ZA0055` |
| [`keep-multi-line-switch-cases-together`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/keep-multi-line-switch-cases-together.md) | opt-in | Removes the blank line between two cases of a `switch` statement when either spans several lines. | `ZA0112` |
| [`keep-multi-line-jsx-children-together`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/keep-multi-line-jsx-children-together.md) | opt-in | Removes the blank line between two children of a JSX element when either spans several lines. | `ZA2002` |

Each rule owns its gaps: when several rules ask for one blank line, the first enabled rule in the [owner order](https://github.com/Nickelony/Stanza/blob/main/js/docs/Rule-map.md#one-owner-per-gap) reports it.
The [rule map](https://github.com/Nickelony/Stanza/blob/main/js/docs/Rule-map.md) says which C# rules JS Stanza leaves to Prettier and ESLint, and the [quirks](https://github.com/Nickelony/Stanza/blob/main/js/docs/Quirks.md) list the syntax the rules take care with.
The [settings](https://github.com/Nickelony/Stanza/blob/main/js/docs/Settings.md) page lists the `settings.stanza` keys that several rules read,
and [Relation to Prettier](https://github.com/Nickelony/Stanza/blob/main/js/docs/Relation-to-Prettier.md) says how the rules agree with it.

## CSS in templates

The rules read the CSS in a template literal as text and never change it.
[`stylelint-config-stanza`](https://github.com/Nickelony/Stanza/blob/main/js/stylelint-config/README.md) is a sibling package in `js/stylelint-config/`
that lays out the blank lines of the CSS in an Emotion template and in a stylesheet, with Stylelint.
It installs, tests and lints on its own, and it is not published yet.

## Development

```text
npm ci
npm run build
npm run typecheck
npm test
npm run format:check
npm run lint
```

`npm run lint` lints the plugin's own source with the `strong` preset and the opt-in rules that keep a run together, so it needs the build first.
It leaves the statement rule off for the tests, which set their arrange, act and assert steps apart with blank lines.
