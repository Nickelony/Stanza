# ZA3113 - keep-single-line-cases-together

**Level: minimal.**
Removes the blank line between two `case` clauses of a `match` statement when each of them spans one line.

```python
# Before
match command:
    case "go": run()

    case "stop": halt()

# After
match command:
    case "go": run()
    case "stop": halt()
```

- Black and `ruff format` write the body of a clause on its own line, so a clause stays on one line only in a file no formatter has laid out.
  The rule matters for a project that runs neither.
- The rule is in no pair: it never shares a gap with [`ZA3111`](ZA3111-separate-multi-line-cases.md) and [`ZA3112`](ZA3112-keep-multi-line-cases-together.md),
  which judge a gap beside a clause of several lines.
- A comment between the clauses keeps the blank line, and so does a `# fmt: off` region.

## C# counterpart

[ZA0113 - Keep single-line switch sections together](https://github.com/Nickelony/Stanza/wiki/ZA0113-Keep-single-line-switch-sections-together).

## JS counterpart

[`keep-single-line-switch-cases-together`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/keep-single-line-switch-cases-together.md).
