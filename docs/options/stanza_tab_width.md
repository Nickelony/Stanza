# stanza_tab_width

Controls the columns between the tab stops of width measurements, so a tab reaches the next multiple of that many columns.
Default: the file's `tab_width`, then its `indent_size`, then `4`.
Read by the rules that measure a line's width, such as [`ZA0043`](https://github.com/Nickelony/Stanza/wiki/ZA0043-Break-the-expression-off-the-signature-line) and [`ZA0201`](https://github.com/Nickelony/Stanza/wiki/ZA0201-Lay-out-a-documentation-comment),
by the rules that compare indentation, [`ZA0038`](https://github.com/Nickelony/Stanza/wiki/ZA0038-Remove-redundant-braces) and [`ZA0045`](https://github.com/Nickelony/Stanza/wiki/ZA0045-Put-the-arrow-on-the-line-after-the-signature),
and by stanza-markup when it rewrites `<member>` bodies.

| Value                                                     | Effect                                                                  |
| --------------------------------------------------------- | ----------------------------------------------------------------------- |
| a number from 1 to 64                                     | a tab stop every that many columns                                      |
| `0`, below zero, above 64, empty, absent, or not a number | the default; the tool also reports a warning for a value it cannot read |

A space is one column whatever the file sets; only a tab is affected.

In code, a tab after other text on its line counts the columns left to the next stop, so at `4` a tab at column 3 ends at column 4.
Documentation text measures each word from its own start, so a tab inside a word reaches the next stop counted from where the word begins.

A `tab_width` or `indent_size` above 64 is skipped the same way, so the next key in the default chain applies,
and stanza-markup reports a warning for it too.

The table measures `int GetValue() => _value;` with one tab of indentation, as [`stanza_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_max_line_length) counts a line:

| Setting                                  | Effect                                                  |
| ---------------------------------------- | ------------------------------------------------------- |
| `stanza_tab_width = 2`                   | signature line 16, arrow line 19, whole line 27 columns |
| no indent key set (4 columns)            | signature line 18, arrow line 21, whole line 29 columns |
| `tab_width = 8` beside `indent_size = 2` | signature line 22, arrow line 25, whole line 33 columns |

With `stanza_max_line_length = 29`, the member fits at a tab of 4 and not at a tab of 8:

```csharp
// no indent key set (a tab is 4 columns) - the whole line is 29 columns, so the member fits
class C
{
	int GetValue() => _value;
}

// tab_width = 8 - the whole line is 33 columns, so the expression takes a line of its own
class C
{
	int GetValue() =>
		_value;
}
```

Where a fix invents the indentation of a line it writes, a member indented with a tab gets one more tab.
A member indented with spaces gets as many spaces as `indent_size` sets, otherwise as `tab_width` sets, otherwise 4,
and this key does not set that step.

[`stanza_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_max_line_length) says how to keep this key in step with the formatter.
