# ZA0105 - Separate a markup comment marker from its text

**Warning, off by default in the analyzer and on in `stanza-markup`.**
Puts a space between a markup comment marker, `<!--` or `-->`, and the text it touches.

```csharp
// Before
const string Project = """
	<Project>
	  <!--Groups every property of the build.-->
	</Project>
	""";

// After
const string Project = """
	<Project>
	  <!-- Groups every property of the build. -->
	</Project>
	""";
```

- A comment inside `xml:space="preserve"` text is left alone,
  because its markers are content rather than layout.
- Under a [profile](https://github.com/Nickelony/Stanza/wiki/Markup-behavior#profiles) that skips property elements,
  the tool leaves a comment inside one alone; the analyzer never skips property elements, so it still reports there.
- An empty comment is left alone, because it has no text to separate from its markers.
- The [`stanza-markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md) tool applies this rule unless the file [disables](https://github.com/Nickelony/Stanza/wiki/Terms) it.
