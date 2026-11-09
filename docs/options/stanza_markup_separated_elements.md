# stanza_markup_separated_elements

Controls which markup elements take the blank lines of a multi-line element, whatever the lines they span.
Default: `none`.
Read by the markup gap rules of the analyzer, such as [`ZA2001`](https://github.com/Nickelony/Stanza/wiki/ZA2001-Separate-multi-line-markup-elements),
on the literals that [`stanza_markup_literals`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_literals) picks, and by stanza-markup.

| Value                                         | Effect                                                             |
| --------------------------------------------- | ------------------------------------------------------------------ |
| a comma separated list of local element names | a listed element is multi-line, so it gets the blank lines of one  |
| `none`, empty, absent, or only separators     | no element is listed: an element is multi-line when it spans lines |

With `stanza_markup_separated_elements = Separator`, a single-line `<Separator />` is split from both neighbors:

```xml
<!-- Before -->
<MenuItem Header="Open" />
<Separator />
<MenuItem Header="Exit" />

<!-- After -->
<MenuItem Header="Open" />

<Separator />

<MenuItem Header="Exit" />
```

- A name matches the local name of an element, so `Separator` matches `<Separator />`, `<x:Separator />` and `<ui:Separator>...</ui:Separator>`.
  Write the name alone, because a colon in an `.editorconfig` value starts a severity suffix, which the reader drops and the tool reports.
- A name is compared whole and with its case, as XML names are, so `separator` and `Separators` match no `Separator`.
- Only the lowercase `none` lists no element, so `None` names the element of an MSBuild `<None>` item.
- Two listed elements side by side are split from each other, as two multi-line elements are.
- A listed element is multi-line for every pair rule, so beside it [`ZA2002`](https://github.com/Nickelony/Stanza/wiki/ZA2002-Keep-multi-line-markup-elements-together) is the tight rule,
  and [`ZA2003`](https://github.com/Nickelony/Stanza/wiki/ZA2003-Keep-single-line-markup-elements-together) judges none of its gaps.
- A listed leaf is multi-line under [`stanza_markup_scope`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_scope) `containers` too.
- Elements that share a line are left as written, listed or not.
- The tool reports an entry that is not a local name, such as one with a space in it, and the entry matches no element.
