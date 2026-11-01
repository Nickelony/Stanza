# stanza_named_argument_literals

Controls which literal arguments [`ZA0502`](https://github.com/Nickelony/Stanza/wiki/ZA0502-Name-the-literal-arguments-of-a-call) names.
Default: `keywords`.
Read by [`ZA0502`](https://github.com/Nickelony/Stanza/wiki/ZA0502-Name-the-literal-arguments-of-a-call).

| Value         | Effect                                                        |
| ------------- | ------------------------------------------------------------- |
| `bool`        | `true` and `false` are named                                  |
| `keywords`    | `true`, `false`, `null`, `default` and `default(T)` are named |
| anything else | as `keywords`                                                 |

```csharp
// Before
Save(path, true, null);

// After
Save(path, overwrite: true, backup: null);

// Before with bool - only the boolean is named
Save(path, true, null);

// After with bool
Save(path, overwrite: true, null);
```

A `null!` or a `default!` is named as the `null` or `default` it carries.
Numbers, strings and characters are never named, whatever the value.
