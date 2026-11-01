# separate-commented-jsx-children

**Level: strong.**
Adds a blank line above a `{/* note */}` comment that leads a single-line child of a JSX element, so the comment introduces a group.

```ts
// Before
const view = (
	<section>
		<Header />
		{/* The body. */}
		<Main />
	</section>
);

// After
const view = (
	<section>
		<Header />

		{/* The body. */}
		<Main />
	</section>
);
```

- The rule asks for the gap only when both neighbors are single-line, because [`separate-multi-line-jsx-children`](separate-multi-line-jsx-children.md) owns the others.
- The comment has to stand on the line directly above the child. A comment separated from the child by a blank line is a child of its own.
- A tool directive, such as `{/* eslint-disable-next-line */}` or `{/* prettier-ignore */}`, annotates the child below it and does not lead a group.
- [`keep-single-line-jsx-children-together`](keep-single-line-jsx-children-together.md) keeps the blank line above such a comment, so they never undo each other.
- An element with text among its children is left as written, because Prettier removes every blank line of such an element.

## C# counterpart

[ZA2004 - Separate a commented markup element](https://github.com/Nickelony/Stanza/wiki/ZA2004-Separate-a-commented-markup-element).
