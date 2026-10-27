# ZA0036 - Remove brace padding

**Suggestion, on by default.**
Removes blank padding beside the braces of blocks, accessor lists and type declarations,
including between a clause header and its opening brace.

```csharp
// Before
public void Process()
{

	DoWork();

}

// After
public void Process()
{
	DoWork();
}
```

- Only whitespace is removed, and a directive in the [gap](https://github.com/Nickelony/Stanza/wiki/Terms) leaves it alone.
- Whitespace between the braces of an empty block or type body belongs to [`ZA0037`](https://github.com/Nickelony/Stanza/wiki/ZA0037-Collapse-empty-blocks).
- With a comment in the gap, the blank lines before the comment go.
- A block statement that follows a sibling leaves the gap above its opening brace to the blank line rules,
  such as [`ZA0005`](https://github.com/Nickelony/Stanza/wiki/ZA0005-Separate-multi-line-constructs), even where they are off.
- The other gaps around braces have other owners;
  see [Which rule owns which gap](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap).
- A namespace body, a `switch` statement and an object initializer are left alone.
