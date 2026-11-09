# Stylelint config Stanza

*Where code becomes poetry.*

A Stylelint config that lays out the blank lines of CSS, SCSS and the CSS-in-JS of Emotion,
the sibling of [JS Stanza](https://github.com/Nickelony/Stanza/blob/main/js/README.md).
It sets core rules of Stylelint and ships the syntax that lets them read an Emotion template, and it adds no rule of its own.
A fix inserts or removes whitespace between two neighboring nodes and never reorders code.

The package is not published yet.

## Install

```text
npm install --save-dev stylelint stylelint-config-stanza
```

The config needs Stylelint 17 or 18 and an ES module config file.
Extend it:

```js
// stylelint.config.mjs
export default { extends: ['stylelint-config-stanza'] };
```

Run `stylelint --fix "src/**/*.{css,scss,ts,tsx}"` to apply the fixes.
A fix writes the same text on every run, and a second run changes nothing.

The config reads a `.js`, `.jsx`, `.mjs`, `.cjs`, `.ts`, `.tsx`, `.mts` or `.cts` file as a script with CSS in its templates,
and a `.scss` file as SCSS through `postcss-scss`. It lints any other file as Stylelint reads it, so a `.css` file is CSS.

## What it does

The fixes separate a multi-line rule, an at-rule block and the declaration that follows a block from their neighbors,
and they remove the blank line that opens a block.
Here is a CSS-in-JS template as Prettier leaves it, and as the fix writes it:

```tsx
/* Before */
import styled from '@emotion/styled';

const Card = styled.div`
	color: red;
	&:hover {
		color: blue;
	}
	${truncate}
	@media (min-width: 600px) {
		padding: 8px;
	}
	margin: 0;
`;

/* After */
import styled from '@emotion/styled';

const Card = styled.div`
	color: red;

	&:hover {
		color: blue;
	}

	${truncate}

	@media (min-width: 600px) {
		padding: 8px;
	}

	margin: 0;
`;
```

A plain stylesheet gets the same fixes:

```css
/* Before */
.card {

	color: red;
	.title {
		font-weight: bold;
	}
}

/* After */
.card {
	color: red;

	.title {
		font-weight: bold;
	}
}
```

## Rules

The config sets these Stylelint rules, and each page owns its option set:

| Rule | What it does |
| --- | --- |
| [`rule-empty-line-before`](https://github.com/Nickelony/Stanza/blob/main/js/stylelint-config/docs/rules/rule-empty-line-before.md) | Separates a multi-line rule from the node above it. |
| [`at-rule-empty-line-before`](https://github.com/Nickelony/Stanza/blob/main/js/stylelint-config/docs/rules/at-rule-empty-line-before.md) | Separates an at-rule from the node above it, and keeps blockless at-rules compact. |
| [`declaration-empty-line-before`](https://github.com/Nickelony/Stanza/blob/main/js/stylelint-config/docs/rules/declaration-empty-line-before.md) | Separates a declaration from the block above it, and keeps the groups of declarations as written. |

A rule of your own config replaces the entry of the same name, and `null` turns a rule off.

## Emotion

The [syntax](https://github.com/Nickelony/Stanza/blob/main/js/stylelint-config/docs/Syntax.md) wraps [`postcss-styled-syntax`](https://github.com/hudochenkov/postcss-styled-syntax), which reads the CSS in a template.
It adds two things:

- A `${...}` that stands alone, such as `${truncate}` above, is a sibling of the rules around it, so the rules separate a selector from it.
  Its JavaScript is never reformatted, and the blank lines inside it are not CSS spacing.
- A `keyframes` template of `@emotion/react` or `@emotion/css` is read as CSS, which the upstream parser skips.

Tests cover Emotion's `css`, `styled.div`, `styled(Component)` and `keyframes`, and the `styled.div` of styled-components.
A template under a renamed import is not read, and the Vue and HTML files are not read either.
The `createGlobalStyle` tag and the `keyframes` of styled-components are untested.
The [syntax page](https://github.com/Nickelony/Stanza/blob/main/js/stylelint-config/docs/Syntax.md#limits) lists the limits.

Do not set another `customSyntax` for those files, because it replaces the syntax of this config and the fixes next to a `${...}` stop.

## Relation to other tools

**JS Stanza.**
[`eslint-plugin-stanza`](https://github.com/Nickelony/Stanza/blob/main/js/README.md) lays out the blank lines between the statements and members of the script.
This config lays out the blank lines inside its CSS templates and stylesheets.
Both follow the same principle, and neither touches the other's text.

**Prettier.**
Prettier formats the CSS in a `css` or `styled` template and in a stylesheet, keeps a blank line the author wrote, and adds none.
So the fixes of this config stand, and Prettier leaves the fixed file as it is.
Prettier does not format a `keyframes` template or a template under a renamed tag, which is why the indentation there is hand-kept.

**Stylelint.**
The rules are core rules of Stylelint, and no other formatting rule is needed beside them.
`stylelint-config-standard` sets the same rules with stricter options:
it removes the blank line between two declarations and between two blockless at-rules of one name.
Extend this config after it to keep the groups that you wrote.

## Dependencies

- `stylelint` is a peer dependency, because the config is a set of its rules and the project owns the version that runs them.
- `postcss` is a peer dependency, because the syntax builds nodes that Stylelint's own copy of PostCSS must accept.
- `postcss-styled-syntax` is the parser that finds the templates, and the syntax wraps it.
- `postcss-scss` is the parser of a `.scss` file, so a `//` comment and a `$variable` read as SCSS.
- `typescript` binds the names of a script, so a `keyframes` tag is traced to its import and a shadowed one is skipped.
  `postcss-styled-syntax` depends on it already, so it adds no second copy.

## Development

```text
npm ci
npm test
npm run format:check
npm run lint
```

`npm run lint` lints the package with the `strong` preset of [JS Stanza](https://github.com/Nickelony/Stanza/blob/main/js/README.md), so run `npm ci` and `npm run build` in `js/` first.
