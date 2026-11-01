# separate-multi-line-elements

**Level: strong.**
Adds a blank line between two elements of a list when either spans more than one line,
so a multi-line object, callback or call stands apart from its neighbor.

```ts
// Before
const options = create(
	'Application',
	true,
	(key, value) => {
		warn(key, value);
	},
	retries,
);

// After
const options = create(
	'Application',
	true,

	(key, value) => {
		warn(key, value);
	},

	retries,
);
```

- A list is the members of an object literal, the elements of an array literal and the members of an enum.
  The arguments of a call or of `new` are a list, and so are the parameters of a function, a method, a constructor or a function type.
- The rule leaves two single-line elements as written, and elements that share a line, as in `run(a, () => {` and `}, retries);`.
- A comment directly above an element moves with it, so the blank line goes above the comment.
- A pattern, such as `const { a, b } = value;`, is not judged.
  [`separate-multi-line-constructs`](separate-multi-line-constructs.md) judges the members of an interface and a type literal.
- Prettier keeps a blank line the author wrote between these elements, so the rule and the formatter agree.
- [`keep-single-line-elements-together`](keep-single-line-elements-together.md) removes the blank line between two single-line elements.
  Disable this rule to keep multi-line elements tight.

## Options

| Option | Default | Effect |
| --- | --- | --- |
| `objectMembers` | `true` | Judges the members of an object literal. |
| `arrayElements` | `true` | Judges the elements of an array literal. |
| `enumMembers` | `true` | Judges the members of an enum. |
| `callArguments` | `true` | Judges the arguments of a call and of `new`. |
| `parameters` | `true` | Judges the parameters of a function, a method, a constructor and a function type. |

## C# counterpart

[ZA0131 - Separate multi-line initializer members](https://github.com/Nickelony/Stanza/wiki/ZA0131-Separate-multi-line-initializer-members),
[ZA0141 - Separate multi-line collection elements](https://github.com/Nickelony/Stanza/wiki/ZA0141-Separate-multi-line-collection-elements),
[ZA0151 - Separate multi-line arguments](https://github.com/Nickelony/Stanza/wiki/ZA0151-Separate-multi-line-arguments)
and [ZA0161 - Separate multi-line parameters](https://github.com/Nickelony/Stanza/wiki/ZA0161-Separate-multi-line-parameters),
with one option for each of them.
