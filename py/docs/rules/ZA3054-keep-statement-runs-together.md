# ZA3054 - keep-statement-runs-together

**Level: opt-in.**
Removes the blank lines inside a run of single-line statements that an enabled grouping rule reads as one.

```python
# Before
print(first)

print(second)

save(first)

# After
print(first)
print(second)

save(first)

# Unchanged - two sections of two statements each
print(first)
print(second)

print(third)
print(fourth)
```

The examples run with [`ZA3004`](ZA3004-separate-invocation-runs.md) enabled, which reads the calls to one function as a run.

- The rule has no list of kinds. It judges the runs of the grouping rules that are enabled,
  so a project picks the kinds by enabling [`ZA3002`](ZA3002-separate-declaration-runs.md), [`ZA3004`](ZA3004-separate-invocation-runs.md) and the other rules that group statements.
- Where [`ZA3003`](ZA3003-separate-same-type-declaration-runs.md) is enabled, two declarations of different types are two runs, and the blank line between them stays.
- The rule removes a blank line only from a gap between two single-line statements that holds nothing else.
  A comment, a trailing comment or a multi-line statement keeps the blank line.
- A blank line between two sections stays as written, a section being at least two statements of one run that stand tight against each other.
  A lone statement beside a section is absorbed, and so are two lone statements.
- A lone statement between two sections is a section of one, so both its blank lines stay.
- Two statements on one line stand tight against each other, so two such lines with a blank line between are two sections.
- The rule never removes a blank line that another rule would write back, such as the one [`ZA3011`](ZA3011-separate-guard-clauses.md) asks for below the guards.
- The rules that write a blank line at the ends of a run read the tree and not the text,
  so removing the blank lines inside a run changes none of their answers.
  The fix writes no blank line of its own.
- A container of exactly two statements belongs to [`ZA3051`](ZA3051-keep-two-units-together.md).
- The members of a class body belong to [`ZA3055`](ZA3055-keep-member-runs-together.md).
- A formatter that joins a statement onto one line can leave a blank line this rule removes on the next run,
  so run the formatter first, as the [formatter page](../Working-with-formatters.md#run-order) says.

## C# counterpart

[ZA0054 - Keep a run of statements together](https://github.com/Nickelony/Stanza/wiki/ZA0054-Keep-a-run-of-statements-together).

## JS counterpart

[`keep-statement-runs-together`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/keep-statement-runs-together.md).
