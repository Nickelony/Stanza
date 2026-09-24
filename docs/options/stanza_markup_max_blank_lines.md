# stanza_markup_max_blank_lines

Controls the most blank lines a [gap](https://github.com/Nickelony/Stanza/wiki/Terms) between two markup elements may keep.
Default: `1`.
Read by [`ZA0104`](https://github.com/Nickelony/Stanza/wiki/ZA0104-Collapse-extra-blank-lines-between-markup-elements) and the markup [tight rules](https://github.com/Nickelony/Stanza/wiki/Terms),
and by stanza-markup.

| Value                                           | Effect                                                                                    |
| ----------------------------------------------- | ----------------------------------------------------------------------------------------- |
| `0`                                             | no cap: a gap is never cut back, and an enabled tight rule removes every blank line in it |
| a number from 1 to 1000                         | the gap keeps at most that many                                                           |
| below zero, above 1000, absent, or not a number | the default; the tool also reports a warning for a value it cannot read                   |

```csharp
// Before - three blank lines between the two elements
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

// After - the cap of one
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

A tight rule that a file [enables](https://github.com/Nickelony/Stanza/wiki/Terms) owns its whole gap,
whatever the cap says, and removes every blank line in it.
[ZA0102](https://github.com/Nickelony/Stanza/wiki/ZA0102-Keep-multi-line-markup-elements-together) or [ZA0103](https://github.com/Nickelony/Stanza/wiki/ZA0103-Keep-single-line-markup-elements-together) owns a gap,
as [`stanza_markup_scope`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_scope) decides.
Where a `#pragma` silences a tight rule over a literal, the cap takes the gaps that rule would have owned there.

[ZA0104](https://github.com/Nickelony/Stanza/wiki/ZA0104-Collapse-extra-blank-lines-between-markup-elements) cuts every other gap above the cap back to the cap,
and its page states which gaps the cap reaches and when the tool applies it.
