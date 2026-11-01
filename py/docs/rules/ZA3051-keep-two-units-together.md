# ZA3051 - keep-two-units-together

**Level: moderate.**
Removes the blank line between the only two units of a container when each spans one line.

```python
# Before
def describe(data):
    result = give_result(data)

    return result

# After
def describe(data):
    result = give_result(data)
    return result
```

A function docstring is documentation and no unit, as the `///` comment of a C# member lies outside its body:

```python
# Before
def indent_width(self, number: int) -> int:
    """Reads the width of the whitespace that opens a line."""
    line = self.lines[number - 1]

    return len(line)

# After
def indent_width(self, number: int) -> int:
    """Reads the width of the whitespace that opens a line."""
    line = self.lines[number - 1]
    return len(line)
```

- With only two units, neither has a neighbor outside the pair to be separated from,
  so the rules that add blank lines ask for nothing there and the one present is padding.
- A container is a module, a class body, a function body, or the body of a compound statement.
- A comment between the two units, a trailing comment included, keeps the blank line.
- A unit that spans several lines keeps the blank line, and [`ZA3001`](ZA3001-separate-multi-line-constructs.md) asks for it.
- The rule skips a container that holds a function, a class, an import, a shebang, or a module or class docstring,
  because a formatter forces the gap there.
- A function docstring does not count as a unit, so a docstring above exactly two single-line statements leaves a pair.
  The blank line below the docstring stays as written.
- A single-line function docstring above one statement loses its blank line, as pydocstyle `D202` asks.
- It is the only rule that removes a blank line.

## C# counterpart

[ZA0051 - Keep the two units of a container together](https://github.com/Nickelony/Stanza/wiki/ZA0051-Keep-the-two-units-of-a-container-together).

## JS counterpart

[`keep-two-units-together`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/keep-two-units-together.md).
