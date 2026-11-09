# ZA2005 - Remove markup element padding

**Suggestion, off by default.**
Removes the blank lines between the start tag of an element and its first item, and between its last item and its end tag.

```csharp
// Before
// language=xml
const string Project = """
	<Project>

	  <PropertyGroup>
	    <TargetFramework>net8.0</TargetFramework>

	  </PropertyGroup>
	</Project>
	""";

// After
// language=xml
const string Project = """
	<Project>
	  <PropertyGroup>
	    <TargetFramework>net8.0</TargetFramework>
	  </PropertyGroup>
	</Project>
	""";
```

- An item is an element, a comment or a processing instruction. A comment at the edge of an element keeps its text and its place.
- A comment that stands on the line of a tag belongs to that tag, so the padding is measured from the end of the comment.
- A blank line between two elements is not padding. [`ZA2001`](https://github.com/Nickelony/Stanza/wiki/ZA2001-Separate-multi-line-markup-elements) to [`ZA2004`](https://github.com/Nickelony/Stanza/wiki/ZA2004-Separate-a-commented-markup-element) and [`ZA2006`](https://github.com/Nickelony/Stanza/wiki/ZA2006-Collapse-extra-blank-lines-between-markup-elements) own it.
  The blank-line cap and the scope of those rules do not change this one: every blank line beside a tag goes.
- An element with text of its own stays as written, because the whitespace is part of its content.
  That covers text, a CDATA section and a reference such as `&#10;`, beside the child elements or instead of them.
  An element inside such text is judged by its own content.
- A subtree under `xml:space="preserve"` stays as written until `xml:space="default"` reopens it.
- Under a [profile](https://github.com/Nickelony/Stanza/wiki/Markup-behavior#profiles) that skips property elements, the tool leaves everything inside one alone;
  the analyzer never skips property elements, so it still reports there.
- The analyzer reads only the raw string literals that [`stanza_markup_literals`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_literals) picks, by default those marked as XML.
  No [level](https://github.com/Nickelony/Stanza/wiki/Presets) enables the rule, so there only its own severity line does, the line `stanza-markup` reads too.
- Unlike `ZA2001`, the [`stanza-markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md) tool applies this rule only where a file [enables](https://github.com/Nickelony/Stanza/wiki/Terms) it.
- CSharpier removes this padding when it formats a project file, so the rule agrees with it.
- In the tool, while a [reprint](https://github.com/Nickelony/Stanza/wiki/Terms) is on, this rule removes nothing inside any `<member>`, as [`stanza_markup_doc_reprint`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_doc_reprint) says.
  [`ZA0903`](https://github.com/Nickelony/Stanza/wiki/ZA0903-Remove-documentation-padding) removes the padding of a reprinted body.
