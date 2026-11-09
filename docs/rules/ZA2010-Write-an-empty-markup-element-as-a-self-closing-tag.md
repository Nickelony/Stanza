# ZA2010 - Write an empty markup element as a self-closing tag

**Suggestion, off by default.**
Writes an element that holds nothing between its start tag and its end tag as one tag that opens and closes at once.

```csharp
// Before
// language=xml
const string Project = """
	<Project>
	  <ItemGroup>
	    <Compile Include="a.cs"></Compile>
	    <None></None>
	  </ItemGroup>
	</Project>
	""";

// After
// language=xml
const string Project = """
	<Project>
	  <ItemGroup>
	    <Compile Include="a.cs" />
	    <None />
	  </ItemGroup>
	</Project>
	""";
```

- An element is empty when no character stands between its two tags.
  An element that holds whitespace, a comment, a processing instruction, a CDATA section or a reference stays as written,
  because `xml:space` and some tools give whitespace its meaning.
- An HTML element that needs an end tag stays as written, such as `div`, `span`, `script`, `textarea`, `a` or `li`,
  because a browser reads `<div/>` as an opening tag.
- The name of an HTML element is compared without case, and a name with a namespace prefix is not one.
  A void element such as `br` or `img` takes the self-closing tag.
- Attributes, wrapped attributes and the namespace prefix are never touched, because only the whitespace before the bracket and the end tag change.
- A bracket on a line of its own, below the attributes, keeps that line, and the slash takes its place.
  An end tag that wraps over lines stays as written.
- The tag takes the [slash shape](https://github.com/Nickelony/Stanza/wiki/Terms) of the pair
  [`ZA2008`](https://github.com/Nickelony/Stanza/wiki/ZA2008-Separate-the-slash-of-a-self-closing-markup-element) and
  [`ZA2009`](https://github.com/Nickelony/Stanza/wiki/ZA2009-Keep-the-slash-of-a-self-closing-markup-element-tight):
  spaced unless only the tight half is [enabled](https://github.com/Nickelony/Stanza/wiki/Terms), so a file that enables neither gets the spaced shape.
  The pair finds nothing to change in what this rule wrote, whichever fix runs first.
- CSharpier keeps an empty element as written when it formats a project file, in either form, so the rule agrees with it.
- The analyzer reads only the raw string literals that [`stanza_markup_literals`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_literals) picks, by default those marked as XML.
  No [level](https://github.com/Nickelony/Stanza/wiki/Presets) enables the rule, so there only its own severity line does, the line `stanza-markup` reads too.
- The [`stanza-markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md) tool applies this rule only where a file enables it, in every [profile](https://github.com/Nickelony/Stanza/wiki/Markup-behavior#profiles).
- Under a profile that skips property elements, the tool leaves an element inside one alone;
  the analyzer never skips property elements, so it still reports there.
- In the tool, while a [reprint](https://github.com/Nickelony/Stanza/wiki/Terms) is on, this rule changes no element inside any `<member>`, as [`stanza_markup_doc_reprint`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_doc_reprint) says.
  [`ZA0909`](https://github.com/Nickelony/Stanza/wiki/ZA0909-Write-an-empty-element-as-a-self-closing-tag) writes the empty elements of a reprinted body.

```csharp
// Unchanged
// language=xml
const string Page = """
	<root>
	  <div></div>
	  <script src="a.js"></script>
	  <note> </note>
	  <note><!-- later --></note>
	</root>
	""";
```
