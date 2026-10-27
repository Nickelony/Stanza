# ZA0041 - Use an expression body for a single-line member

**Suggestion, off by default.**
Gives a method, constructor, destructor, operator, conversion operator or local function an expression body
when its signature and single statement fit within the line width.

```csharp
// Before
public int GetValue()
{
	return _value;
}

// After
public int GetValue() => _value;
```

- [`stanza_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_max_line_length) decides whether the complete member fits on one line.
- When the member fits only with the expression on its own line, the rule writes that shape.
  The arrow ends the signature line,
  or opens the next line where [`ZA0046`](https://github.com/Nickelony/Stanza/wiki/ZA0046-Put-the-arrow-on-the-line-after-the-signature) owns the arrow's gap at that member,
  and [`stanza_multiline_body_trigger`](https://github.com/Nickelony/Stanza/wiki/stanza_multiline_body_trigger) decides whether the arrow counts toward that line's width.
- A `with` expression or a query keeps its block where only that shape fits and the trigger counts the body,
  because CSharpier spreads either one over more lines after a broken arrow, and `ZA0040` would ask for the block back.
- `ZA0040` judges only expression bodies, so both can be enabled.
- Only a block whose one statement is a `return` with a value, a `throw` with an expression or an expression statement converts.
  An `if` or a `yield return` keeps its block.
- A block keeps its braces when it holds two statements, a comment inside the braces,
  or a statement that spans lines.
- A project below C# 6.0 gets no report. A constructor or destructor needs C# 7.0, and so does a body that throws.
