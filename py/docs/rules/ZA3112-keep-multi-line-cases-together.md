# ZA3112 - keep-multi-line-cases-together

**Level: opt-in.**
Removes the blank line between two `case` clauses of a `match` statement when either of them spans more than one line.

```python
# Before
match state:
    case "pending":
        log.info("Waiting for the payment.")

    case "shipped":
        log.info("On its way.")

# After
match state:
    case "pending":
        log.info("Waiting for the payment.")
    case "shipped":
        log.info("On its way.")
```

- The rule is the tight half of the pair with [`ZA3111`](ZA3111-separate-multi-line-cases.md).
  Where both are enabled, this rule owns the gaps it judges and `ZA3111` writes none of them,
  as [the pair table](../Rule-map.md#pairs-and-their-default-halves) says.
- A comment between the clauses keeps the blank line, and so does a `# fmt: off` region.
- A case that ends in a function or class keeps the blank line, because Black writes it there.
- A pair of single-line clauses belongs to [`ZA3113`](ZA3113-keep-single-line-cases-together.md).
- No level enables the rule, because `ZA3111` separates the clauses at the `moderate` level and the formatters keep the blank line either way.

## C# counterpart

[ZA0112 - Keep multi-line switch sections together](https://github.com/Nickelony/Stanza/wiki/ZA0112-Keep-multi-line-switch-sections-together).

## JS counterpart

[`keep-multi-line-switch-cases-together`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/keep-multi-line-switch-cases-together.md).
