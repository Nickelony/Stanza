# ZA2004 - Separate a commented markup element

**Suggestion, off by default.**
Adds a blank line above the comment that stands directly above a single-line element of a [container](https://github.com/Nickelony/Stanza/wiki/Terms),
so the comment stays with the element it describes.

```csharp
// Before
// language=xml
const string Project = """
	<Project>
	  <PackageReference Include="a" />
	  <!-- The second package. -->
	  <PackageReference Include="b" />
	</Project>
	""";

// After
// language=xml
const string Project = """
	<Project>
	  <PackageReference Include="a" />

	  <!-- The second package. -->
	  <PackageReference Include="b" />
	</Project>
	""";
```

- The rule judges a [gap](https://github.com/Nickelony/Stanza/wiki/Terms) only when neither neighboring element spans more than one line.
  A gap beside a wrapped or multi-line element stays with [`ZA2001`](https://github.com/Nickelony/Stanza/wiki/ZA2001-Separate-multi-line-markup-elements).
- A comment or processing instruction counts, and it has to stand on the line directly above the element, with no blank line between them.
- The blank line goes above the first line of a run of comment lines, never between the comment and the element.
- The rule stays quiet for the first child of a container, for a gap that already holds a blank line,
  and for a comment that trails the earlier element on its line.
  A comment on its own line below that trailing comment still fires.
- Where [`ZA2003`](https://github.com/Nickelony/Stanza/wiki/ZA2003-Keep-single-line-markup-elements-together) is [enabled](https://github.com/Nickelony/Stanza/wiki/Terms),
  it removes the blank line above such a comment and this rule writes nothing, so the two never undo each other.
  A `#pragma` that silences `ZA2003` over a literal hands its gaps back to this rule.
- The analyzer reads only the raw string literals that [`stanza_markup_literals`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_literals) picks, by default those marked as XML.
  No [level](https://github.com/Nickelony/Stanza/wiki/Presets) enables the rule, so there only its own severity line does, the line `stanza-markup` reads too.
- Unlike `ZA2001`, the [`stanza-markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md) tool applies this rule only where a file enables it.
- In the tool, while a [reprint](https://github.com/Nickelony/Stanza/wiki/Terms) is on, this rule writes no blank line inside any `<member>`, as [`stanza_markup_doc_reprint`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_doc_reprint) says.
