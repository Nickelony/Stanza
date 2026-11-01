# ZA1001 - Shorten a long line

**Suggestion, off by default.**
Reports a line that runs past [`stanza_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_max_line_length),
at the part of the line past the width.
It is a [report-only rule](https://github.com/Nickelony/Stanza/wiki/Terms), so it has no code fix.

```csharp
// Before - 70 columns, so past a width of 60
var report = BuildReport(order.Customer, order.Lines, order.Discount);

// After - rewritten by hand, with the arguments on lines of their own
var report = BuildReport(
	order.Customer,
	order.Lines,
	order.Discount
);
```

- The rule reports once for a line, from the first column past the width to the end of the line.
- The columns count as [`stanza_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_max_line_length) says, and a width of `0` turns the rule off.
- A line is left alone when its first column past the width lies inside a string literal or a URL that starts before that column,
  because no wrapping splits either one.
- Only `)`, `]`, `}`, `;`, `,` or `+` may follow such a string, and only the `*/` that closes its comment may follow such a URL.
- Code or words after such a string or URL could move to the next line, so their line is reported.
- A string or a URL that starts past the width does not excuse its line, because breaking the line before it moves it.
- A string literal is a regular, verbatim, raw or UTF-8 literal, or one text part of an interpolated string.
  The code inside an interpolation is judged like other code.
- A URL is a run of characters without whitespace that starts with `http://` or `https://` in lower case, inside a `//` or `/* */` comment.
  A URL in the text of a directive is not excused.
- A documentation comment line, one in a `///` or `/** */` comment, is left to
  [`ZA0901`](https://github.com/Nickelony/Stanza/wiki/ZA0901-Lay-out-a-documentation-comment),
  which wraps it at [`stanza_doc_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_max_line_length), so each line has one reporter.
  A build that writes no documentation file reads such a comment as a plain one, and its lines are still left alone.
- Every other line is judged, including a `#` directive line, a line of text in a block that an `#if` leaves out, and an ordinary comment.
- A generated file is skipped, and a markup file is not read.
- A `#pragma warning disable ZA1001` silences the lines below it, as it does for any rule.
- No level enables this rule, because CSharpier owns the width, as
  [Working with formatters](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#layout-that-csharpier-undoes) says.
- Roslynator's `RCS0056` ("A line is too long") reports the same fault under its own `roslynator_max_line_length` key,
  so a project enables one of the two.

```csharp
// Unchanged - the overflow starts inside a string literal
var message = "An order shipped to a customer who has not confirmed the address";

// Unchanged - the overflow starts inside a URL in a comment
// See https://github.com/Nickelony/Stanza/wiki/ZA1001-Shorten-a-long-line
```
