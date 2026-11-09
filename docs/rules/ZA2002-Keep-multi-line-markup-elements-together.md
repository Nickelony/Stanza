# ZA2002 - Keep multi-line markup elements together

**Suggestion, off by default.**
Removes the blank line between two neighboring elements of a [container](https://github.com/Nickelony/Stanza/wiki/Terms) when either of them spans more than one line.

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

- The [separating rule](https://github.com/Nickelony/Stanza/wiki/Terms) is [`ZA2001`](https://github.com/Nickelony/Stanza/wiki/ZA2001-Separate-multi-line-markup-elements);
  [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
- Under [`containers`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_scope) scope, a wrapped [leaf](https://github.com/Nickelony/Stanza/wiki/Terms) is not multi-line,
  so this rule leaves its [gap](https://github.com/Nickelony/Stanza/wiki/Terms) alone unless the element on the other side is multi-line.
- The analyzer reads only the raw string literals that [`stanza_markup_literals`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_literals) picks, by default those marked as XML.
  No [level](https://github.com/Nickelony/Stanza/wiki/Presets) enables the rule, so there only its own severity line does, the line `stanza-markup` reads too.
- The [`stanza-markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md) tool applies this rule only where a file [enables](https://github.com/Nickelony/Stanza/wiki/Terms) it,
  and [`ZA2001`](https://github.com/Nickelony/Stanza/wiki/ZA2001-Separate-multi-line-markup-elements) reports nothing over the same gaps.
- [`stanza_markup_max_blank_lines`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_max_blank_lines) says how this rule and the blank-line cap share a gap.
