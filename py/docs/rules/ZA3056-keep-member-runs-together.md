# ZA3056 - keep-member-runs-together

**Level: opt-in.**
Removes the blank lines inside a run of single-line fields or attributes of a class body that the member group rule reads as one.

```python
# Before
class Order:
    id: int

    total: float

    LIMIT = 10

    STEP = 2

# After
class Order:
    id: int
    total: float

    LIMIT = 10
    STEP = 2

# Unchanged - two sections of two fields each
class Order:
    id: int
    total: float

    count: int
    limit: int
```

The example runs with [`ZA3017`](ZA3017-separate-member-groups.md) enabled, which reads the fields as one run and the attributes as another.

- A field is an annotated assignment and an attribute is an assignment without an annotation, as `ZA3017` reads them.
  A field and an attribute are two runs, so the blank line between them stays.
- The rule removes a blank line only from a gap between two single-line members that holds nothing else.
  A comment, a trailing comment or a multi-line member keeps the blank line.
- A blank line between two sections stays as written, a section being at least two members of one run that stand tight against each other.
  A lone member beside a section is absorbed, and so are two lone members.
- A lone member between two sections is a section of one, so both its blank lines stay.
- Two members on one line stand tight against each other, so two such lines with a blank line between are two sections.
- Removing the blank lines inside a run moves none of the blank lines the other rules ask for at its ends,
  so one fix settles the run. The fix writes no blank line of its own.
- The rule judges nothing while `ZA3017` is off.
- A method and a nested class are definitions, and the formatters own the gaps beside them.
- A class body of exactly two single-line members belongs to [`ZA3054`](ZA3054-keep-two-units-together.md).
- The statements of a module and a function belong to [`ZA3055`](ZA3055-keep-statement-runs-together.md).

## C# counterpart

[ZA0056 - Keep a run of members together](https://github.com/Nickelony/Stanza/wiki/ZA0056-Keep-a-run-of-members-together).

## JS counterpart

[`keep-member-runs-together`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/keep-member-runs-together.md).
