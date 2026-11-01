# stanza_markup_literals

Controls which C# raw string literals the analyzer reads as markup for the markup rules.
Default: `marked`.
Read by the markup rules of the analyzer, such as [`ZA2001`](https://github.com/Nickelony/Stanza/wiki/ZA2001-Separate-multi-line-markup-elements);
stanza-markup ignores it.

| Value         | Effect                                                                                |
| ------------- | ------------------------------------------------------------------------------------- |
| `marked`      | a literal is read when a `language=xml` comment or a `[StringSyntax("Xml")]` marks it |
| `all`         | every literal is read, test fixtures and expected output included                     |
| `off`         | no literal is read, so the markup rules report nothing in C#                          |
| anything else | `marked`                                                                              |

```csharp
// marked - each of these literals is marked, so the markup rules read it
class Templates
{
	// language=xml
	const string Project = """
		<Project>
		  <ItemGroup />
		</Project>
		""";

	[StringSyntax(StringSyntaxAttribute.Xml)]
	public string Body { get; set; } = """
		<Body>
		  <Item />
		</Body>
		""";

	void Load()
	{
		Render(/* lang=xml */ """
			<Page>
			  <Header />
			</Page>
			""");
	}
}
```

- A blank line that a fix adds or removes inside a raw string changes the string's value,
  so `marked` leaves a literal alone until someone marks it as XML.
- A comment marks a literal when it reads `language=xml` or `lang=xml`, in any case and with spaces around the `=` allowed,
  as a `//` comment or a `/* */` comment; more text, such as a prefix, may follow after a space.
- The comment stands right before the literal, at the end of the line before it, or above the statement that holds the literal.
  Outside a statement, such as in a field initializer, it stands above the member.
  A comment above the method or above another statement marks nothing.
- `[StringSyntax]` of `System.Diagnostics.CodeAnalysis` marks a literal that initializes or is assigned to the field or property it sits on,
  or that is passed to the parameter it sits on, as a positional, named or `params` argument.
- The syntax name of the attribute is compared in any case, so `"xml"` and `StringSyntaxAttribute.Xml` both count, and `"Json"` marks nothing.
- The attribute on a positional record parameter also marks the property the parameter declares, and the reverse.
- The analyzer matches the attribute by its name, so a copy that a project declares in that namespace counts too.
- A literal that reaches its target through a variable, a conditional expression or a concatenation is not marked.
- Under any value, an interpolated raw string and a single-line raw string are never read,
  and a literal must parse as one document, as [Limitations](https://github.com/Nickelony/Stanza/wiki/Limitations) says.
- The key picks the literals, and a markup rule still needs its own severity line,
  because no [level](https://github.com/Nickelony/Stanza/wiki/Presets) holds one.
- [`stanza-markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md) reads markup files, not C#,
  so it ignores the key and reports no warning for it.
