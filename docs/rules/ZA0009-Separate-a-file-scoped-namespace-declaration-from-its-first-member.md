# ZA0009 - Separate a file-scoped namespace declaration from its first member

**Warning, on by default.**
Adds a blank line between a file-scoped namespace declaration and its first member,
because the declaration ends with a semicolon and no brace sets the member apart.

```csharp
// Before
namespace App;
class Widget
{
	internal int Count;
}

// After
namespace App;

class Widget
{
	internal int Count;
}
```

- A comment before the member belongs to the member, so the blank line lands above the comment.
- A comment after the semicolon belongs to the header, so the blank line lands below the last line of a multi-line one.
- The [gap](https://github.com/Nickelony/Stanza/wiki/Terms) *before* the header belongs to `ZA0007` when an import section sits above it.
- An import section after the header moves the gap below that section to `ZA0007`,
  and this rule reports nothing.
- The rule leaves an existing blank line before the first member alone.
- A directive at the boundary, such as `#region`, keeps the gap as written.
- The rule leaves a brace-bodied namespace alone.
