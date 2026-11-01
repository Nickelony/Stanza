# separate-multi-line-jsx-children

**Level: strong.**
Adds a blank line between two children of a JSX element when either spans more than one line,
so a multi-line element stands apart from its siblings.

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

- A child is an element, a fragment or an expression such as `{items.map(...)}`, and the rule judges the children of every element and fragment.
- A `{/* note */}` comment directly above a child moves with it, so the blank line goes above the comment.
- Children that share a line stay joined.
- An element with text among its children is left as written, because Prettier removes every blank line of such an element.
  A `{' '}` between two inline elements counts as text.
- A statement and a member belong to [`separate-multi-line-constructs`](separate-multi-line-constructs.md).
- The rule is the separating half of the pair with [`keep-multi-line-jsx-children-together`](keep-multi-line-jsx-children-together.md).
  While the tight half is enabled it owns the gaps it judges, and this rule asks for none of them.
- Two single-line children belong to [`keep-single-line-jsx-children-together`](keep-single-line-jsx-children-together.md),
  and a comment above a single-line child to [`separate-commented-jsx-children`](separate-commented-jsx-children.md).

## C# counterpart

[ZA2001 - Separate multi-line markup elements](https://github.com/Nickelony/Stanza/wiki/ZA2001-Separate-multi-line-markup-elements).
