# separate-base-call

**Level: strong.**
Adds a blank line below a `super` call that opens a constructor or method body, and above one that closes it,
so the call to the parent stands apart from the member's own work.

```ts
// Before
class Panel extends Widget {
	constructor(options) {
		super(options);
		this.title = options.title;
		this.size = options.size;
	}

	dispose() {
		this.timer.dispose();
		this.stream.dispose();
		super.dispose();
	}
}

// After
class Panel extends Widget {
	constructor(options) {
		super(options);

		this.title = options.title;
		this.size = options.size;
	}

	dispose() {
		this.timer.dispose();
		this.stream.dispose();

		super.dispose();
	}
}
```

- A base call is a statement that calls through a chain that starts at `super`,
  such as `super(options);`, `super.render();`, `await super.load();` or `super.items.clear();`.
  An assignment such as `super.value = 1;` is no call, and neither is a call that passes `super.value` as an argument.
- The rule judges the block body of a constructor, a method, a getter, a setter and an object method.
  An arrow function and a nested block such as an `if` body are left alone, because a base call there is not the call the member extends.
- The opening call is the first statement below the guard clauses that open the body.
  [`separate-guard-clauses`](separate-guard-clauses.md) owns the blank line above it, and this rule the one below it.
- The closing call is the last statement of the body.
  A body that opens and closes with a base call gets both blank lines, and a base call alone in its body has no gap to set apart.
- `return super.load();` is a jump, so [`separate-jump-statements`](separate-jump-statements.md) judges the gap above it.
- A body of two single-line statements stays tight, because [`keep-two-units-together`](keep-two-units-together.md) owns it.
- A comment directly above the statement below the call moves with it, so the blank line goes above the comment.
- The guards are read as `separate-guard-clauses` reads them, by the [`guardRoots`](separate-guard-clauses.md#settings) setting, whether or not that rule is enabled.
- The rule has no tight counterpart; leave it disabled to keep base calls tight.

## C# counterpart

[ZA0010 - Separate the base call](https://github.com/Nickelony/Stanza/wiki/ZA0010-Separate-the-base-call).
