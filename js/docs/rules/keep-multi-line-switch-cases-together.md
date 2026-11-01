# keep-multi-line-switch-cases-together

**Level: opt-in.**
Removes the blank line between two cases of a `switch` statement when either spans more than one line.

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

- The rule is the tight half of the pair with [`separate-multi-line-switch-cases`](separate-multi-line-switch-cases.md).
  Enable it over a preset and it owns the gaps it judges, so the separating half writes none of them and the cases end up tight.
- A comment in the gap, a trailing comment included, keeps the blank line.
- Two single-line cases belong to [`keep-single-line-switch-cases-together`](keep-single-line-switch-cases-together.md).
- Prettier keeps a blank line the author wrote between cases, so the rule removes what the formatter leaves.
- No preset enables the rule, because [`separate-multi-line-switch-cases`](separate-multi-line-switch-cases.md) separates the cases at the `moderate` level.

## C# counterpart

[ZA0112 - Keep multi-line switch sections together](https://github.com/Nickelony/Stanza/wiki/ZA0112-Keep-multi-line-switch-sections-together).
