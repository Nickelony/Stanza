# separate-multi-line-switch-cases

**Level: moderate.**
Adds a blank line between two cases of a `switch` statement when either spans more than one line.

```ts
// Before
switch (state) {
	case 'pending':
		logger.info('Waiting for the payment.');
		break;
	case 'shipped':
		logger.info('On its way.');
		break;
}

// After
switch (state) {
	case 'pending':
		logger.info('Waiting for the payment.');
		break;

	case 'shipped':
		logger.info('On its way.');
		break;
}
```

- Two single-line cases stay as written, and [`keep-single-line-switch-cases-together`](keep-single-line-switch-cases-together.md) judges them.
- A comment directly above a case moves with it, so the blank line goes above the comment.
- An empty `case` that falls through to the next one stays with it, so a list of labels stays one stanza.
- The rule is the separating half of the pair with [`keep-multi-line-switch-cases-together`](keep-multi-line-switch-cases-together.md).
  While the tight half is enabled it owns the gaps it judges, and this rule asks for none of them.
- Leave the rule disabled to keep the cases tight, and enable the tight half to remove the blank lines that stand.
- [`separate-multi-line-constructs`](separate-multi-line-constructs.md) judges the statements around the `switch` and inside a case, not the cases themselves.

## C# counterpart

[ZA0111 - Separate multi-line switch sections](https://github.com/Nickelony/Stanza/wiki/ZA0111-Separate-multi-line-switch-sections).
