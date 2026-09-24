# ZA0101 - Separate multi-line markup elements

**Warning, off by default in the analyzer and on in `stanza-markup`.**
Adds the missing blank line between two neighboring elements of a [container](https://github.com/Nickelony/Stanza/wiki/Terms) when either of the two spans more than one line.

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

- The analyzer reads non-interpolated multi-line raw string literals that parse as a whole document;
  a markup file goes to the [`stanza-markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md) tool instead.
- The [`stanza-markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md) tool applies this rule unless the file [disables](https://github.com/Nickelony/Stanza/wiki/Terms) it,
  and reports nothing when the file [enables](https://github.com/Nickelony/Stanza/wiki/Terms) its [tight rule](https://github.com/Nickelony/Stanza/wiki/Terms), [`ZA0102`](https://github.com/Nickelony/Stanza/wiki/ZA0102-Keep-multi-line-markup-elements-together);
  [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
- Set [`stanza_markup_scope`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_scope) to `containers` to count only elements with child elements as multi-line.
- Elements that share a line are left as written, because the rule cannot tell what indentation a split needs.
- A comment that trails the first element on its line stays with it,
  and a comment directly above the second element travels with that element, so the new blank line goes above the comment.
- The analyzer ships the rule off because a blank line added inside a raw string changes the string's value.
- In the tool, while a [reprint](https://github.com/Nickelony/Stanza/wiki/Terms) is on, this rule writes no blank line inside any `<member>`,
  reprinted or left as written. The analyzer does not reprint, so it still reports inside a member.
