# keep-single-line-jsx-children-together

**Level: strong.**
Removes the blank line between two children of a JSX element when each spans a single line.

```ts
// Before
const list = (
	<ul>
		<Item id={1} />

		<Item id={2} />
		<Item id={3} />
	</ul>
);

// After
const list = (
	<ul>
		<Item id={1} />
		<Item id={2} />
		<Item id={3} />
	</ul>
);
```

- A child is an element, a fragment or an expression such as `{items.map(...)}`, as for [`separate-multi-line-jsx-children`](separate-multi-line-jsx-children.md).
- A child that spans several lines keeps the blank line, and [`separate-multi-line-jsx-children`](separate-multi-line-jsx-children.md) asks for it where enabled.
- A `{/* note */}` comment directly above a child keeps the blank line above it, like a comment in the gap of a list.
  [`separate-commented-jsx-children`](separate-commented-jsx-children.md) asks for that blank line where enabled.
- Prettier keeps a blank line the author wrote between the children of an element without text, so this rule removes what the formatter leaves.
- An element with text among its children is left as written, because Prettier removes every blank line of such an element.

## C# counterpart

[ZA2003 - Keep single-line markup elements together](https://github.com/Nickelony/Stanza/wiki/ZA2003-Keep-single-line-markup-elements-together).
