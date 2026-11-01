# keep-multi-line-jsx-children-together

**Level: opt-in.**
Removes the blank line between two children of a JSX element when either spans more than one line.

```ts
// Before
const view = (
	<section>
		<Header />

		<Main>
			<Item />
		</Main>

		<Footer />
	</section>
);

// After
const view = (
	<section>
		<Header />
		<Main>
			<Item />
		</Main>
		<Footer />
	</section>
);
```

- The rule is the tight half of the pair with [`separate-multi-line-jsx-children`](separate-multi-line-jsx-children.md).
  Enable it over a preset and it owns the gaps it judges, so the separating half writes none of them and the children end up tight.
- A `{/* note */}` comment directly above a child keeps the blank line above it.
- Two single-line children belong to [`keep-single-line-jsx-children-together`](keep-single-line-jsx-children-together.md).
- Prettier keeps a blank line the author wrote between the children of an element without text, so the rule removes what the formatter leaves.
- An element with text among its children is left as written, because Prettier removes every blank line of such an element.
- No preset enables the rule, because [`separate-multi-line-jsx-children`](separate-multi-line-jsx-children.md) separates the children at the `strong` level.

## C# counterpart

[ZA2002 - Keep multi-line markup elements together](https://github.com/Nickelony/Stanza/wiki/ZA2002-Keep-multi-line-markup-elements-together).
