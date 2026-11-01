# ZA3111 - separate-multi-line-cases

**Level: moderate.**
Adds a blank line between two `case` clauses of a `match` statement when either of them spans more than one line.

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

- Two single-line clauses stay as written, and [`ZA3113`](ZA3113-keep-single-line-cases-together.md) judges them.
- A comment directly above a clause moves with it, so the blank line goes above the comment.
- The rule is the separating half of the pair with [`ZA3112`](ZA3112-keep-multi-line-cases-together.md);
  [the pair table](../Rule-map.md#pairs-and-their-default-halves) names the default half and the owner when both are enabled.
- Turn the rule off with `disable = ["ZA3111"]`, and `ZA3112` and `ZA3113` keep working.
- [`ZA3001`](ZA3001-separate-multi-line-constructs.md) judges the statements around the `match`, not the clauses inside it.

## C# counterpart

[ZA0111 - Separate multi-line switch sections](https://github.com/Nickelony/Stanza/wiki/ZA0111-Separate-multi-line-switch-sections).

## JS counterpart

[`separate-multi-line-switch-cases`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-multi-line-switch-cases.md).
