# ZA2006 - Collapse extra blank lines between markup elements

**Suggestion, off by default in the analyzer and on in `stanza-markup`.**
Keeps no more blank lines in a [gap](https://github.com/Nickelony/Stanza/wiki/Terms) between two neighboring elements of a [container](https://github.com/Nickelony/Stanza/wiki/Terms) than [`stanza_markup_max_blank_lines`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_max_blank_lines) allows.

```csharp
// Before
// language=xml
const string Project = """
	<Project>
	  <ItemGroup>
	    <PackageReference Include="a" />
	  </ItemGroup>



	  <ItemGroup>
	    <PackageReference Include="b" />
	  </ItemGroup>
	</Project>
	""";

// After
// language=xml
const string Project = """
	<Project>
	  <ItemGroup>
	    <PackageReference Include="a" />
	  </ItemGroup>

	  <ItemGroup>
	    <PackageReference Include="b" />
	  </ItemGroup>
	</Project>
	""";
```

- The analyzer reads only the raw string literals that [`stanza_markup_literals`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_literals) picks, by default those marked as XML.
  No [level](https://github.com/Nickelony/Stanza/wiki/Presets) enables the rule, so there only its own severity line does, the line `stanza-markup` reads too.
- The cap applies to any gap, including one between two single-line elements.
- [`stanza_markup_max_blank_lines`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_max_blank_lines) says how the cap shares a gap with a [tight rule](https://github.com/Nickelony/Stanza/wiki/Terms).
- A comment is never removed.
- A comment that trails the earlier element on its line, or sits directly above the later one,
  goes with that element, so the cap counts the blank lines between the two elements.
- A gap that holds any other comment is left alone.
- At `0`, the rule reports nothing.
- The [`stanza-markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md) tool applies the cap unless the file [disables](https://github.com/Nickelony/Stanza/wiki/Terms) this rule.
