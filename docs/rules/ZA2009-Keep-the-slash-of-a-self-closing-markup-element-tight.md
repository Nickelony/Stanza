# ZA2009 - Keep the slash of a self-closing markup element tight

**Suggestion, off by default.**
Removes the whitespace before the slash of an element that closes itself.

```csharp
// Before
// language=xml
const string Project = """
	<Project>
	  <Compile Include="a.cs" />
	</Project>
	""";

// After
// language=xml
const string Project = """
	<Project>
	  <Compile Include="a.cs"/>
	</Project>
	""";
```

- [`ZA2008`](https://github.com/Nickelony/Stanza/wiki/ZA2008-Separate-the-slash-of-a-self-closing-markup-element) writes the spaced form instead;
  [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
- A slash [gap](https://github.com/Nickelony/Stanza/wiki/Terms) that spans a line stays as written.
- Markup that [`ZA2008`](https://github.com/Nickelony/Stanza/wiki/ZA2008-Separate-the-slash-of-a-self-closing-markup-element) leaves as written stays as written here.
- CSharpier writes the spaced form when it formats a project file, so this rule undoes the formatter.
  Leave it off in a project that runs CSharpier on markup.
- The analyzer reads only the raw string literals that [`stanza_markup_literals`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_literals) picks, by default those marked as XML.
  No [level](https://github.com/Nickelony/Stanza/wiki/Presets) enables the rule, so there only its own severity line does, the line `stanza-markup` reads too.
- The [`stanza-markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md) tool applies this rule only where a file [enables](https://github.com/Nickelony/Stanza/wiki/Terms) it.
