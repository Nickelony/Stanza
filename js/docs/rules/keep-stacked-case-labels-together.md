# keep-stacked-case-labels-together

**Level: minimal.**
Removes the blank line between two stacked labels of a `switch` statement, where an empty `case` falls through to the next label.

```ts
// Before
switch (state) {
	case 'pending':

	case 'queued':
		return 'waiting';
	default:
		return 'done';
}

// After
switch (state) {
	case 'pending':
	case 'queued':
		return 'waiting';
	default:
		return 'done';
}

// Unchanged - Prettier removes a blank line below the last label
switch (state) {
	case 'pending':
	case 'queued':

		return 'waiting';
}

// Unchanged - a case with a body is no stacked label
switch (state) {
	case 'pending':
		return 'waiting';

	case 'queued':
		return 'waiting';
}
```

- A stacked label is an empty `case` directly above another `case` or the `default` label, so the labels share one body.
- A comment in the gap, a trailing comment included, keeps the blank line.
- Prettier removes the blank line between the last label and the first statement of a body, so this rule does not judge that gap.
  Prettier keeps the blank line between two stacked labels, so this rule removes what the formatter leaves.
- The other switch case rules leave the gap below an empty `case` as written, so none of them shares a gap with this rule.

## C# counterpart

[ZA0204 - Remove switch label padding](https://github.com/Nickelony/Stanza/wiki/ZA0204-Remove-switch-label-padding).
