# ZA0203 - Remove brace padding

**Suggestion, on by default.**
Removes blank padding beside the braces of blocks, accessor lists, type declarations, namespaces,
`switch` statements and expressions, initializers and anonymous objects,
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
- Whitespace between the braces of an empty block or type body belongs to [`ZA0204`](https://github.com/Nickelony/Stanza/wiki/ZA0204-Collapse-empty-blocks).
- With a comment in the gap, the blank lines before the first comment go.
  Before a closing brace the blank lines below the last comment go too, and the blank lines between comments stay.
- A block statement that follows a sibling leaves the gap above its opening brace to the blank line rules,
  such as [`ZA0001`](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-multi-line-constructs), even where they are off.
- The other gaps around braces have other owners;
  see [Which rule owns which gap](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap).
- An initializer that follows a comma is an element of its list, so the gap above it belongs to the rules of that list.
- A blank line between two closing braces, such as the end of a namespace after the type it holds, stays.
