# ZA0103 - Keep single-line markup elements together

**Warning, off by default.**
Removes the blank line between two neighboring elements of a [container](https://github.com/Nickelony/Stanza/wiki/Terms) when neither of them spans more than one line.

```csharp
// Before
const string Project = """
	<Project>
	  <PackageReference Include="a" />

	  <PackageReference Include="b" />
	</Project>
	""";

// After
const string Project = """
	<Project>
	  <PackageReference Include="a" />
	  <PackageReference Include="b" />
	</Project>
	""";
```

- This rule judges a [gap](https://github.com/Nickelony/Stanza/wiki/Terms) only when neither neighboring element spans more than one line,
  so it can be [enabled](https://github.com/Nickelony/Stanza/wiki/Terms) alongside [`ZA0101`](https://github.com/Nickelony/Stanza/wiki/ZA0101-Separate-multi-line-markup-elements) and [`ZA0102`](https://github.com/Nickelony/Stanza/wiki/ZA0102-Keep-multi-line-markup-elements-together).
- A wrapped [leaf](https://github.com/Nickelony/Stanza/wiki/Terms) spans more than one line,
  so this rule leaves its gap alone under either scope, and a blank line already there stays.
  See [`stanza_markup_scope`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_scope).
- [`stanza_markup_max_blank_lines`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_max_blank_lines) says how this rule and the blank-line cap share a gap.
- Only a file that enables this rule gets it from [`stanza-markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md).
