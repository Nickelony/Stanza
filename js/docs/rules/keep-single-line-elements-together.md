# keep-single-line-elements-together

**Level: minimal.**
Removes the blank line between two elements of a list when each spans a single line.

```ts
// Before
const retry = {
	attempts: 3,

	delay: 100,
	factor: 2,

	jitter: true,
};

// After
const retry = {
	attempts: 3,
	delay: 100,
	factor: 2,
	jitter: true,
};
```

- A list is the same set as for [`separate-multi-line-elements`](separate-multi-line-elements.md):
  object members, array elements, enum members, arguments and parameters.
- An element that spans several lines keeps the blank line, and [`separate-multi-line-elements`](separate-multi-line-elements.md) asks for it where enabled.
- A comment in the gap, a trailing comment included, keeps the blank line.
- Prettier keeps a blank line the author wrote between these elements, so this rule removes what the formatter leaves.
  Prettier joins a list that fits on one line and holds no blank line, so run Prettier after the fix.
- An option turns the rule off for one kind of list, such as the members of an object that you group with blank lines.

## Options

The options have the names of those of [`separate-multi-line-elements`](separate-multi-line-elements.md#options), and each rule reads its own.

| Option | Default | Effect |
| --- | --- | --- |
| `objectMembers` | `true` | Judges the members of an object literal. |
| `arrayElements` | `true` | Judges the elements of an array literal. |
| `enumMembers` | `true` | Judges the members of an enum. |
| `callArguments` | `true` | Judges the arguments of a call and of `new`. |
| `parameters` | `true` | Judges the parameters of a function, a method, a constructor and a function type. |

## C# counterpart

[ZA0133 - Keep single-line initializer members together](https://github.com/Nickelony/Stanza/wiki/ZA0133-Keep-single-line-initializer-members-together),
[ZA0143 - Keep single-line collection elements together](https://github.com/Nickelony/Stanza/wiki/ZA0143-Keep-single-line-collection-elements-together),
[ZA0153 - Keep single-line arguments together](https://github.com/Nickelony/Stanza/wiki/ZA0153-Keep-single-line-arguments-together)
and [ZA0163 - Keep single-line parameters together](https://github.com/Nickelony/Stanza/wiki/ZA0163-Keep-single-line-parameters-together),
which this one rule covers, with one option for each of them.
