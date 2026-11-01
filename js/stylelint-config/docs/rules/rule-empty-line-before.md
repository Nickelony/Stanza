# rule-empty-line-before

Asks for a blank line above a rule that spans several lines, and for none above the first rule of a block.

```js
'rule-empty-line-before': ['always-multi-line', { except: ['first-nested'], ignore: ['after-comment'] }]
```

```css
/* Before */
.card {

	.title {
		font-weight: bold;
	}
	.badge {
		color: blue;
	}
}

/* After */
.card {
	.title {
		font-weight: bold;
	}

	.badge {
		color: blue;
	}
}
```

| Option | Effect |
| --- | --- |
| `always-multi-line` | A rule whose block spans several lines has a blank line above it. A single-line rule is not judged. |
| `except: ['first-nested']` | The first rule of a block has no blank line above it, so the fix removes one. |
| `ignore: ['after-comment']` | A rule directly below a comment is not judged, so the comment stays attached. |

- A run of single-line rules stays compact, such as `.a { color: red; }` above `.b { color: blue; }`.
- In a template, a standalone `${...}` is a sibling, so the rule below one gets a blank line, and the first rule after the opening backtick gets none.
- A rule whose selector holds an interpolation, such as `${Button}:hover &` or `.${name}`, is not judged, because Stylelint skips it.
- The fix inserts or removes the blank line and never moves a rule.
- The [Stylelint page](https://stylelint.io/user-guide/rules/rule-empty-line-before) owns the meaning of each option.
- [`separate-multi-line-constructs`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-multi-line-constructs.md) lays out the same gap between the statements of the script.
