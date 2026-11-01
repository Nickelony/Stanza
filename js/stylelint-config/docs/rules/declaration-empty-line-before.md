# declaration-empty-line-before

Asks for a blank line above a declaration that follows a block, and for none above the first declaration of a block.

```js
'declaration-empty-line-before': ['always', { except: ['first-nested'], ignore: ['after-comment', 'after-declaration', 'inside-single-line-block'] }]
```

```css
/* Before */
.card {
	color: red;
	.title {
		font-weight: bold;
	}
	margin: 0;
}

/* After */
.card {
	color: red;

	.title {
		font-weight: bold;
	}

	margin: 0;
}
```

| Option | Effect |
| --- | --- |
| `always` | A declaration has a blank line above it. |
| `except: ['first-nested']` | The first declaration of a block has no blank line above it, so the fix removes one. |
| `ignore: ['after-comment']` | A declaration directly below a comment is not judged, so the comment stays attached. |
| `ignore: ['after-declaration']` | A declaration below another declaration is not judged, so the author decides where the groups of declarations end. |
| `ignore: ['inside-single-line-block']` | A declaration of a block written on one line is not judged. |

- A declaration that follows a block, an at-rule block or a rule is the only one the rule adds a blank line to.
- In a template, a standalone `${...}` is a declaration of its own, in the middle of a block or at its end,
  so it follows a block with a blank line, and the declaration below it stays as written.
- `stylelint-config-standard` removes the blank line between two declarations, and this config leaves it, so the groups of declarations stay.
- The fix inserts or removes the blank line and never moves a declaration.
- The [Stylelint page](https://stylelint.io/user-guide/rules/declaration-empty-line-before) owns the meaning of each option.
