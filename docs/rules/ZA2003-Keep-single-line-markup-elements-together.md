# ZA2003 - Keep single-line markup elements together

**Suggestion, off by default.**
Removes the blank line between two neighboring elements of a [container](https://github.com/Nickelony/Stanza/wiki/Terms) when neither of them spans more than one line.

```csharp
// Before
// language=xml
const string Project = """
	<Project>
	  <PackageReference Include="a" />

	  <PackageReference Include="b" />
	</Project>
	""";

// After
// language=xml
const string Project = """
	<Project>
	  <PackageReference Include="a" />
	  <PackageReference Include="b" />
	</Project>
	""";
```

- This rule judges a [gap](https://github.com/Nickelony/Stanza/wiki/Terms) only when neither neighboring element spans more than one line,
  so it can be [enabled](https://github.com/Nickelony/Stanza/wiki/Terms) alongside [`ZA2001`](https://github.com/Nickelony/Stanza/wiki/ZA2001-Separate-multi-line-markup-elements) and [`ZA2002`](https://github.com/Nickelony/Stanza/wiki/ZA2002-Keep-multi-line-markup-elements-together).
- A wrapped [leaf](https://github.com/Nickelony/Stanza/wiki/Terms) spans more than one line,
  so this rule leaves its gap alone under either scope, and a blank line already there stays.
  See [`stanza_markup_scope`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_scope).
- This rule removes the blank line above a comment that stands directly above the later element.
  Where [`ZA2004`](https://github.com/Nickelony/Stanza/wiki/ZA2004-Separate-a-commented-markup-element) is enabled too, that rule writes nothing there.
- [`stanza_markup_max_blank_lines`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_max_blank_lines) says how this rule and the blank-line cap share a gap.
- The analyzer reads only the raw string literals that [`stanza_markup_literals`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_literals) picks, by default those marked as XML.
  No [level](https://github.com/Nickelony/Stanza/wiki/Presets) enables the rule, so there only its own severity line does, the line `stanza-markup` reads too.
- Only a file that enables this rule gets it from [`stanza-markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md).
