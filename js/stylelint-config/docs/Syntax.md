# Syntax

The syntax reads the CSS in the templates of a script, so the rules of the config see a `css`, `styled` or `keyframes` template as a stylesheet.
The config sets it as the `customSyntax` of the script files, and `stylelint-config-stanza/syntax` exports it for a config that sets its own.

```js
import syntax from 'stylelint-config-stanza/syntax';

export default { overrides: [{ files: ['**/*.ts'], customSyntax: syntax }] };
```

It wraps [`postcss-styled-syntax`](https://github.com/hudochenkov/postcss-styled-syntax) and keeps its output for every source it does not change.
A fix rewrites the template and nothing else of the script, and the text of each `${...}` comes back as it was written.

## What the upstream parser reads

- The upstream parser finds a template by the name of its tag, not by its import.
  It reads `css`, `createGlobalStyle`, `styled.div`, `styled(Component)`, their `.attrs(...)` forms and `styled.div(props => ...)`.
- A template of a renamed import, such as `import s from '@emotion/styled'`, is not found, so it is not linted.
- A script that TypeScript cannot parse is skipped whole.
  The fix leaves it as written and reports nothing.

## Standalone interpolations

A `${...}` that stands alone on the whitespace before a node, or at the end of a block, becomes a node of its own.
The upstream parser keeps it in the whitespace of the node below, where a rule reading the blank lines of a block would consume it.

```tsx
/* Before */
import { css } from '@emotion/react';

const error = css`
	&.error {
		${border}
		.label {
			color: red;
		}
		${tail}
	}
`;

/* After */
import { css } from '@emotion/react';

const error = css`
	&.error {
		${border}

		.label {
			color: red;
		}

		${tail}
	}
`;
```

- The node is a declaration with the property `stanza-interpolation`, so the declaration rules treat it as a sibling.
  A rule or an at-rule below it gets a blank line, and the first node of a block has none above it.
- A `${...}` at the end of a block is a declaration too, so a rule or an at-rule above it gets a blank line, and one alone in a block has none.
- The blank lines inside a multi-line `${...}` are JavaScript, so no rule counts them as CSS spacing.
- A `${...}` inside a selector, a property or a value stays part of it.
  Stylelint does not judge a rule whose selector holds one, so no blank line is added above `${Button}:hover & { ... }`.
- Each node keeps its place in the script, so a nested template inside a `${...}` is fixed at its own source range.

## Keyframes

The upstream parser skips a `keyframes` template, so the syntax reads it as a `css` template.
It does so for a tag named `keyframes` that the file imports from `@emotion/react` or `@emotion/css`.

```tsx
/* Before */
import { keyframes } from '@emotion/react';

const spin = keyframes`
	from {
		opacity: 0;
	}
	to {
		opacity: 1;
	}
`;

/* After */
import { keyframes } from '@emotion/react';

const spin = keyframes`
	from {
		opacity: 0;
	}

	to {
		opacity: 1;
	}
`;
```

- The tag is found by its import binding, so a `keyframes` that a function parameter or a local declaration shadows is not read.
  A `keyframes` of another library and a tag under another name, such as `import { keyframes as animate }`, are not read either.
- A `keyframes` inside a `${...}` of a `css` template is read, with its whitespace and comments between the tag and the backtick kept.
- The parser swaps each tag for a private-use marker of the same length and writes the tag back after the fix.
  It picks a marker character that the script does not hold.
- A script that holds the text `keyframes` is bound once more to find the tags, with the TypeScript compiler API and without reading any other file.

## Limits

- The syntax checks the blank lines of a template and never the JavaScript around it.
- The tag of a `css` template is read by its name, so a `css` tag of any library is read as CSS.
- Object styles, such as `css({ color: 'red' })`, are not CSS text and are not read.
- The `createGlobalStyle` tag and the `keyframes` of styled-components are untested.
- Vue, Svelte and HTML files are not read.
