# keep-single-line-switch-cases-together

**Level: minimal.**
Removes the blank line between two cases of a `switch` statement when each spans a single line.

```ts
// Before
switch (state) {
	case 'pending': return 'waiting';

	case 'shipped': return 'shipped';
}

// After
switch (state) {
	case 'pending': return 'waiting';
	case 'shipped': return 'shipped';
}
```

- Prettier writes the body of a case on its own line, so a case stays on one line only in a file the formatter has not laid out.
  The rule matters for a project that runs no formatter.
- The rule never shares a gap with the pair [`separate-multi-line-switch-cases`](separate-multi-line-switch-cases.md)
  and [`keep-multi-line-switch-cases-together`](keep-multi-line-switch-cases-together.md), which judge a gap beside a case of several lines.
- A comment in the gap, a trailing comment included, keeps the blank line.
- An empty `case` that falls through to the next one is joined already, and a blank line there is left as written.

## C# counterpart

[ZA0113 - Keep single-line switch sections together](https://github.com/Nickelony/Stanza/wiki/ZA0113-Keep-single-line-switch-sections-together).
