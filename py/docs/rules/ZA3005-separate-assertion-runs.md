# ZA3005 - separate-assertion-runs

**Level: moderate.**
Separates a run of two or more neighboring assertions from the statements beside it,
so the arrange, act and assert steps of a test read as three stanzas.

```python
# Before
result = parse(text)
assert result.count == 2
assert result.first is not None
cleanup()

# After
result = parse(text)

assert result.count == 2
assert result.first is not None

cleanup()
```

- An assertion is an `assert` statement, a call that `assertion-roots` matches, or a `with` block whose items are all such calls.
- `self.assertEqual(...)`, `self.assertTrue(...)` and `mock.assert_called_once()` are assertions by the default `assert*` pattern,
  and `pytest.raises(...)` by its own entry.
- Assertions group by kind and not by method, so `assertEqual` and `assertTrue` are one run.
- An awaited assertion, such as `await client.assert_ok()`, stays an assertion.
- A lone assertion stays tight.

## Options

| Key | Default | Effect |
| --- | --- | --- |
| `assertion-roots` | `["assert*", "expect", "pytest.raises", "pytest.warns", "pytest.deprecated_call"]` | The patterns that make a call an assertion. |

A pattern is a shell-style glob that matches the dotted path of the callee or any name along it, as the [configuration page](../Configuration.md#patterns) says.
Unlike the exact names of the C# and JS roots, it can match a family of assertions such as `assert*`.
The invocation, await and subscription rules read the same list, so they agree on which statements are assertions.

## C# counterpart

[ZA0005 - Separate assertion runs](https://github.com/Nickelony/Stanza/wiki/ZA0005-Separate-assertion-runs),
whose option is [`stanza_assertion_roots`](https://github.com/Nickelony/Stanza/wiki/stanza_assertion_roots).

## JS counterpart

[`separate-assertion-runs`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-assertion-runs.md).
