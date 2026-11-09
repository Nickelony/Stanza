# ZA2001 - Separate multi-line markup elements

**Suggestion, off by default in the analyzer and on in `stanza-markup`.**
Adds the missing blank line between two neighboring elements of a [container](https://github.com/Nickelony/Stanza/wiki/Terms) when either of the two spans more than one line.

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

- The analyzer reads only the raw string literals that [`stanza_markup_literals`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_literals) picks, by default those marked as XML;
  a markup file goes to the [`stanza-markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md) tool instead.
- The [`stanza-markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md) tool applies this rule unless the file [disables](https://github.com/Nickelony/Stanza/wiki/Terms) it,
  and reports nothing when the file [enables](https://github.com/Nickelony/Stanza/wiki/Terms) its [tight rule](https://github.com/Nickelony/Stanza/wiki/Terms), [`ZA2002`](https://github.com/Nickelony/Stanza/wiki/ZA2002-Keep-multi-line-markup-elements-together);
  [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
- Set [`stanza_markup_scope`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_scope) to `containers` to count only elements with child elements as multi-line.
- List element names in [`stanza_markup_separated_elements`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_separated_elements) to give a single-line element,
  such as `<Separator />`, the blank lines of a multi-line one.
- Elements that share a line are left as written, because the rule cannot tell what indentation a split needs.
- A comment that trails the first element on its line stays with it,
  and a comment directly above the second element travels with that element, so the new blank line goes above the comment.
  Between two single-line elements, [`ZA2004`](https://github.com/Nickelony/Stanza/wiki/ZA2004-Separate-a-commented-markup-element) writes that blank line.
- No [level](https://github.com/Nickelony/Stanza/wiki/Presets) enables the rule, so in the analyzer only its own severity line does, the line `stanza-markup` reads too.
- In the tool, while a [reprint](https://github.com/Nickelony/Stanza/wiki/Terms) is on, this rule writes no blank line inside any `<member>`, as [`stanza_markup_doc_reprint`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_doc_reprint) says.
