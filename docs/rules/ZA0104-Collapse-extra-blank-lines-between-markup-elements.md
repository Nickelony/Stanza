# ZA0104 - Collapse extra blank lines between markup elements

**Warning, off by default in the analyzer and on in `stanza-markup`.**
Keeps no more blank lines in a [gap](https://github.com/Nickelony/Stanza/wiki/Terms) between two neighboring elements of a [container](https://github.com/Nickelony/Stanza/wiki/Terms) than [`stanza_markup_max_blank_lines`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_max_blank_lines) allows.

```csharp
// Before
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

- The analyzer inspects only the literals that [`ZA0101`](https://github.com/Nickelony/Stanza/wiki/ZA0101-Separate-multi-line-markup-elements) covers.
- The cap applies to any gap, including one between two single-line elements.
- [`stanza_markup_max_blank_lines`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_max_blank_lines) says how the cap shares a gap with a [tight rule](https://github.com/Nickelony/Stanza/wiki/Terms).
- A comment is never removed.
- A comment that trails the earlier element on its line, or sits directly above the later one,
  goes with that element, so the cap counts the blank lines between the two elements.
- A gap that holds any other comment is left alone.
- At `0`, the rule reports nothing.
- The [`stanza-markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md) tool applies the cap unless the file [disables](https://github.com/Nickelony/Stanza/wiki/Terms) this rule.
