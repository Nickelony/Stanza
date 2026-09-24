# ZA0106 - Reprint the body of a documentation member

With `check --format msbuild`,
the [`stanza-markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md) tool reports `ZA0106` for each documentation `<member>` body that a [reprint](https://github.com/Nickelony/Stanza/wiki/Terms) would rewrite.

The example uses the default [`stanza_doc_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_max_line_length),
which counts the indentation.

```xml
<!-- Before -->
<doc>
  <members>
    <member name="M:Sample.Type.Example">
      <summary>
      Binds the options from the configuration section and validates every value before the host starts, reporting failures early enough to keep the service from running with invalid settings.
      </summary>
    </member>
  </members>
</doc>

<!-- After -->
<doc>
  <members>
    <member name="M:Sample.Type.Example">
      <summary>
      Binds the options from the configuration section and validates every value
      before the host starts, reporting failures early enough to keep the service from running with
      invalid settings.
      </summary>
    </member>
  </members>
</doc>
```

For the Before file, `check --format msbuild --reprint` reports:

```text
parts/member.xml(3,1): warning ZA0106: Reprint the body of this member
```

- The tool reports once per body, on the member's opening line,
  when [`stanza_markup_doc_reprint`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_doc_reprint) or `--reprint` requests the rewrite.
- `format` writes the same rewrite under the same condition, and `check` reports it without writing.
- A body the reprint leaves as written, such as one with a comment, an entity or a CDATA section,
  gets no diagnostic; [Markup behavior](https://github.com/Nickelony/Stanza/wiki/Markup-behavior#bodies-a-reprint-leaves-as-written) lists every such case.
- A body already in the target layout produces no diagnostic.

[`stanza_markup_doc_reprint`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_doc_reprint) says what a reprint rewrites and which keys its layout reads.
