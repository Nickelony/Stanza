# ZA0039 - Use a block body for a multi-line member

**Suggestion, off by default.**
Gives a method, constructor, destructor, operator, conversion operator or local function a block body
when its signature or expression body spans more than one line.

```csharp
// Before
private static Kind Pick(bool first) =>
	first ? Kind.One
		: Kind.Two;

// After
private static Kind Pick(bool first)
{
	return first ? Kind.One
		: Kind.Two;
}
```

- [`stanza_multiline_body_trigger`](https://github.com/Nickelony/Stanza/wiki/stanza_multiline_body_trigger) decides whether the signature,
  the expression body, either of them, or both make the member multi-line; `ZA0041` reads the same key for a value member.
- The block gets a `return` when the member hands a value back.
- The expression text stays as written, so its wrapped lines keep their indentation.
- A wrapped arrow does not make a member multi-line, so a member with a one-line signature and a one-line expression keeps its arrow.
- A comment beside the arrow or a directive in the expression keeps the arrow,
  because the fix cannot move either one.
- `ZA0040` judges only block bodies, so both can be [enabled](https://github.com/Nickelony/Stanza/wiki/Terms).
