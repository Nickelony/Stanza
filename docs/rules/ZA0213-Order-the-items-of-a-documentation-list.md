# ZA0213 - Order the items of a documentation list

**Suggestion, off by default.**
Writes the header of a `<list>` or `<table>` element before the items it describes,
and the term of a row before the description that explains it.

```csharp
// Before
/// <list type="table">
/// <item>
/// <term>One</term>
/// <description>The first.</description>
/// </item>
/// <listheader>
/// <description>Value</description>
/// <term>Name</term>
/// </listheader>
/// </list>

// After
/// <list type="table">
/// <listheader>
/// <term>Name</term>
/// <description>Value</description>
/// </listheader>
/// <item>
/// <term>One</term>
/// <description>The first.</description>
/// </item>
/// </list>
```

- Two items of the same kind keep the order they stand in.
- A comment with several runs out of order gets one report for each run, at its first item out of place,
  and each fix orders that run alone.
- A list whose items a blank line or a line of prose separates is left unchanged,
  because a blank line is the author's own break, and a line of prose between items is comment text.
- An item must sit on lines of its own, and so must the term and the description of a row.
  A line that holds two of these is left unchanged, because moving one would also move the other.
- A list that holds a `<code>` block or a `<para>` element is left unchanged.
- It is the one clean-up that reads inside a list, as [Limitations](https://github.com/Nickelony/Stanza/wiki/Limitations) says.
- The [`stanza-markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md) tool also orders the items of a list when a [reprint](https://github.com/Nickelony/Stanza/wiki/Terms) rewrites a `<member>` body in a file that [enables](https://github.com/Nickelony/Stanza/wiki/Terms) this rule;
  see [`stanza_markup_doc_reprint`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_doc_reprint).
