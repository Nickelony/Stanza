# stanza_markup_scope

Controls which markup elements count as multi-line for the [gap](https://github.com/Nickelony/Stanza/wiki/Terms) rules.
Default: `all` in the analyzer, and the profile's own scope in the tool.
Read by the markup gap rules, such as [`ZA0101`](https://github.com/Nickelony/Stanza/wiki/ZA0101-Separate-multi-line-markup-elements),
and by stanza-markup.

| Value         | Effect                                                                               |
| ------------- | ------------------------------------------------------------------------------------ |
| `all`         | an element is multi-line when it spans more than one line, whatever it holds         |
| `containers`  | an element is multi-line when it spans more than one line and holds element children |
| anything else | `all` in the analyzer; the tool uses the profile's own scope and reports a warning   |

`containers` is the narrower reading:
an element without element children, a [leaf](https://github.com/Nickelony/Stanza/wiki/Terms), never counts as multi-line.
The two values part ways on a wrapped leaf, a leaf that spans more than one line:

```xml
<!-- all - the wrapped <PackageReference> spans two lines, so the gap needs a blank line -->
<ItemGroup>
  <PackageReference
    Include="a" />

  <PackageReference Include="b" />
</ItemGroup>

<!-- containers - neither leaf counts as multi-line, so no blank line is added between them -->
<ItemGroup>
  <PackageReference
    Include="a" />
  <PackageReference Include="b" />
</ItemGroup>

<!-- containers with ZA0103 enabled - the blank line stays, because the wrapped leaf spans two lines -->
<ItemGroup>
  <PackageReference
    Include="a" />

  <PackageReference Include="b" />
</ItemGroup>
```

- A gap counts as multi-line when either of its two neighboring elements does.
- The [pair](https://github.com/Nickelony/Stanza/wiki/Terms) [`ZA0101`](https://github.com/Nickelony/Stanza/wiki/ZA0101-Separate-multi-line-markup-elements) (separating) and [`ZA0102`](https://github.com/Nickelony/Stanza/wiki/ZA0102-Keep-multi-line-markup-elements-together) (tight) owns a multi-line gap.
- Any other gap falls to [`ZA0103`](https://github.com/Nickelony/Stanza/wiki/ZA0103-Keep-single-line-markup-elements-together),
  the [single-line tight rule](https://github.com/Nickelony/Stanza/wiki/Terms), unless one of its elements spans more than one line.
- A gap that no rule owns keeps its blank lines,
  apart from the cap in [`stanza_markup_max_blank_lines`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_max_blank_lines).
- The analyzer applies this key only to markup inside C# raw string literals.
- [`stanza_markup_profile`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_profile) decides which files a run reads and how each one is laid out.
