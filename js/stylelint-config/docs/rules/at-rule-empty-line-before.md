# at-rule-empty-line-before

Asks for a blank line above an at-rule, and for none above the first at-rule of a block, while blockless at-rules stay as written.

```js
'at-rule-empty-line-before': ['always', { except: ['first-nested'], ignore: ['after-comment', 'blockless-after-blockless'], ignoreAtRules: ['else'] }]
```

```css
/* Before */
.card {
	color: red;
	@media (min-width: 600px) {
		color: blue;
	}
}

/* After */
.card {
	color: red;

	@media (min-width: 600px) {
		color: blue;
	}
}
```

| Option | Effect |
| --- | --- |
| `always` | An at-rule has a blank line above it. |
| `except: ['first-nested']` | The first at-rule of a block has no blank line above it, so the fix removes one. |
| `ignore: ['after-comment']` | An at-rule directly below a comment is not judged, so the comment stays attached. |
| `ignore: ['blockless-after-blockless']` | A blockless at-rule below another one is not judged, so a list of `@use` or `@import` rules keeps the groups it was written in. |
| `ignoreAtRules: ['else']` | An `@else` is not judged, so it stays beside the `}` of its `@if`. |

- A blockless at-rule below a rule or a declaration still gets the blank line, such as `@include focus;` below a declaration.
- The fix inserts or removes the blank line and never moves an at-rule.
- The [Stylelint page](https://stylelint.io/user-guide/rules/at-rule-empty-line-before) owns the meaning of each option.
