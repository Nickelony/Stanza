# ZA0004 - Separate multi-line constructs

**Warning, on by default.**
Adds a blank line at a [gap](https://github.com/Nickelony/Stanza/wiki/Terms) when either neighbor spans more than one line.

```csharp
// Before
var options = BuildOptions();
foreach (SectionOptions section in options.Sections)
{
	Bind(section);
}
logger.LogInformation("Starting the host.");

// After
var options = BuildOptions();

foreach (SectionOptions section in options.Sections)
{
	Bind(section);
}

logger.LogInformation("Starting the host.");
```

- The rule judges the statements of a block or switch section and the members of a type, enum, namespace or file.
  The elements of a delimited list have their own pair;
  see [Which rule owns which gap](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap).
- The rule leaves a gap alone when a blank line already separates the [units](https://github.com/Nickelony/Stanza/wiki/Terms) or a directive sits on it.
- A unit is multi-line when it spans lines from its first token to its last,
  so an attribute list on its own line counts, and a documentation comment above the unit does not.
- The rule inserts a blank line only when the second unit starts on the line the first one ends on or on the next line.
  The comments that travel with the second unit count as part of it.
- A multi-line comment that trails the first unit counts as part of it,
  so the blank line goes after the comment's last line, never inside the comment.
