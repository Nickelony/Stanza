# separate-member-groups

**Level: moderate.**
Separates a run of two or more neighboring members of one kind from the members beside it.

```ts
// Before
class Widget {
	private count = 0;
	private name = '';
	constructor() {}
	reset() {}
	clear() {}
}

// After
class Widget {
	private count = 0;
	private name = '';

	constructor() {}

	reset() {}
	clear() {}
}
```

- The kinds of a class are fields, the constructor, accessors, methods and `static` blocks.
  An interface body and a type literal have properties, methods, call signatures, construct signatures and index signatures.
- A getter and a setter are accessors, so the pair is one run and stays together.
- Overload signatures and the implementation of a method stay together.
- A lone member stays tight.

## C# counterpart

[ZA0013 - Separate member groups](https://github.com/Nickelony/Stanza/wiki/ZA0013-Separate-member-groups).
