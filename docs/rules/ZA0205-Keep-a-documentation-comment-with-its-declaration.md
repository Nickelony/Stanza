# ZA0205 - Keep a documentation comment with its declaration

**Suggestion, off by default.**
Keeps a documentation comment directly above the declaration it documents by removing the blank lines between them.

```csharp
// Before
/// <summary>Binds the options from the configuration section.</summary>

internal int Bind()

// After
/// <summary>Binds the options from the configuration section.</summary>
internal int Bind()
```

- A blank line between the comment and an attribute list is removed too,
  because the attribute list belongs to the declaration.
- The rule judges a comment before a member or a local function.
  A comment that follows an attribute list, or that precedes a statement, is left alone.
- If another comment sits in the [gap](https://github.com/Nickelony/Stanza/wiki/Terms), the rule reports nothing.
