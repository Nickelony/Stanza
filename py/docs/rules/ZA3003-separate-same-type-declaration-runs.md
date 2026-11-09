# ZA3003 - separate-same-type-declaration-runs

**Level: strong.**
Separates a run of two or more neighboring declarations of one type from the statements beside it, declarations of another type included.

```python
# Before
width: int = 640
height: int = 480
smooth: bool = True
sharp: bool = False
title: str = "Orders"
show(title)

# After
width: int = 640
height: int = 480

smooth: bool = True
sharp: bool = False

title: str = "Orders"
show(title)
```

- The type of a declaration is its annotation, compared by syntax, so `dict[str, int]` and `dict[str,int]` match.
- An assignment to names with no annotation, such as `limit = 3` or `a, b = 1, 2`, has no written type.
  Two of them always match, as two `var` declarations do in C#.
- A `global`, `nonlocal` or `type` statement and an assignment to an attribute or an item declare no type and join no run.
- A declaration with no neighbor of its type forms no run of its own,
  so it gets a blank line only where a neighboring run begins or ends.
- The rule judges the statements of a module, a function and a block.
  A class body is judged by [`ZA3013`](ZA3013-separate-member-groups.md), which reads fields and attributes.
- [`ZA3002`](ZA3002-separate-declaration-runs.md) asks for the same blank line at the ends of a run of declarations,
  and this rule adds the one between two runs of different types.
- [`ZA3054`](ZA3054-keep-statement-runs-together.md) removes the blank lines inside a run of one type where it is enabled.

## C# counterpart

[ZA0003 - Separate same type declaration runs](https://github.com/Nickelony/Stanza/wiki/ZA0003-Separate-same-type-declaration-runs).

## JS counterpart

None. JS Stanza has no rule that reads the written type of a declaration.
