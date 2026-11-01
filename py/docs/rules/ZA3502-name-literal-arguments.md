# ZA3502 - name-literal-arguments

**Level: strong.**
Writes the name of the bound parameter before each `True`, `False` or `None` argument of a call that passes two or more arguments,
as `named-argument-literals` picks the words, and before every argument after the first one it names.

```python
# Before
def save(path, overwrite, backup): ...


save("notes.txt", True, None)

# After
def save(path, overwrite, backup): ...


save("notes.txt", overwrite=True, backup=None)
```

```python
# Before
def save(path, overwrite, backup): ...


save("notes.txt", True, "notes.bak")

# After
def save(path, overwrite, backup): ...


save("notes.txt", overwrite=True, backup="notes.bak")
```

- A call that passes one argument is left alone, as in `set_enabled(True)`.
- Numbers, strings and `...` stay unnamed, as in `max(a, 0)`.
- Python allows no positional argument after a named one, so the first literal and every argument after it are named,
  and a call whose later arguments cannot be named is left alone.
- A literal that fills a positional-only parameter stays positional, and the rule names from the next literal that can take a name.
- An argument that already has a name is left as it is.
- The rule reports once per call, at the `(` of its argument list, and silences the way [`ZA3501`](ZA3501-name-call-arguments.md) does.
- [`ZA3501`](ZA3501-name-call-arguments.md) owns a call it reports, because naming every argument covers the literals,
  so the two never report one call. This rule works with `ZA3501` off, and reports a call below the argument minimum of that rule.
- The calls the rule resolves and the ones it leaves alone are those of [`ZA3501`](ZA3501-name-call-arguments.md#what-it-names).

## Options

| Key | Default | Effect |
| --- | --- | --- |
| `named-argument-literals` | `"keywords"` | `"keywords"` starts the names at the first `True`, `False` or `None`, and `"bool"` at the first `True` or `False`, so a `None` after it is named too. |

The `named-argument-sources`, `named-argument-methods`, `named-argument-ignore` and `source-roots` keys of [`ZA3501`](ZA3501-name-call-arguments.md#options) apply to this rule too.

## C# counterpart

[ZA0502 - Name the literal arguments of a call](https://github.com/Nickelony/Stanza/wiki/ZA0502-Name-the-literal-arguments-of-a-call),
whose option is [`stanza_named_argument_literals`](https://github.com/Nickelony/Stanza/wiki/stanza_named_argument_literals).

## JS counterpart

None.
