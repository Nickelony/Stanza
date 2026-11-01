# ZA2008 - Separate the slash of a self-closing markup element

**Suggestion, off by default.**
Puts exactly one space before the slash of an element that closes itself.

```csharp
// Before
// language=xml
const string Project = """
	<Project>
	  <Compile Include="a.cs"/>
	</Project>
	""";

// After
// language=xml
const string Project = """
	<Project>
	  <Compile Include="a.cs" />
	</Project>
	""";
```

- [`ZA2009`](https://github.com/Nickelony/Stanza/wiki/ZA2009-Keep-the-slash-of-a-self-closing-markup-element-tight) writes the tight form instead;
  [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
- A project that runs both gets this rule's shape, because it is the default half.
- The [gap](https://github.com/Nickelony/Stanza/wiki/Terms) is judged only when it stays on one line.
  A slash on a line of its own is the wrapped layout of the tag and stays as written.
  A tag whose attributes wrap still takes the space when the slash follows the last attribute, so `d="2"/>` becomes `d="2" />`.
- A tag with no attribute takes the space too, so `<None/>` becomes `<None />`.
- A tag under `xml:space="preserve"` stays as written until `xml:space="default"` reopens it.
  Text, comments, attributes and entities are never touched, because only the whitespace before the slash changes.
- Under a [profile](https://github.com/Nickelony/Stanza/wiki/Markup-behavior#profiles) that skips property elements, the tool leaves a tag inside one alone;
  the analyzer never skips property elements, so it still reports there.
- The analyzer reads only the raw string literals that [`stanza_markup_literals`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_literals) picks, by default those marked as XML.
  No [level](https://github.com/Nickelony/Stanza/wiki/Presets) enables the rule, so there only its own severity line does, the line `stanza-markup` reads too.
- The [`stanza-markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md) tool applies this rule only where a file [enables](https://github.com/Nickelony/Stanza/wiki/Terms) it.
- CSharpier writes the spaced form when it formats a project file, so the rule agrees with it.
- In the tool, while a [reprint](https://github.com/Nickelony/Stanza/wiki/Terms) is on, this rule changes no tag inside any `<member>`, as [`stanza_markup_doc_reprint`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_doc_reprint) says.
  [`ZA0907`](https://github.com/Nickelony/Stanza/wiki/ZA0907-Separate-the-slash-of-a-self-closing-tag) writes the slash of a reprinted body.
