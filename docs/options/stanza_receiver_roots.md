# stanza_receiver_roots

Selects the direct receivers that participate in configured receiver grouping.
Default: `none`.
Read by [`ZA0004`](https://github.com/Nickelony/Stanza/wiki/ZA0004-Separate-invocation-runs), [`ZA0010`](https://github.com/Nickelony/Stanza/wiki/ZA0010-Separate-receiver-groups) and [`ZA0064`](https://github.com/Nickelony/Stanza/wiki/ZA0064-Keep-a-receiver-group-together).
[`ZA0052`](https://github.com/Nickelony/Stanza/wiki/ZA0052-Gather-a-run-of-loose-statements) and [`ZA0055`](https://github.com/Nickelony/Stanza/wiki/ZA0055-Keep-a-run-of-statements-together) read it through the runs of those rules, only while one of them is on.

| Value                   | Effect                                                                     |
| ----------------------- | -------------------------------------------------------------------------- |
| a comma separated list  | receivers that contain a matching case-sensitive name segment are selected |
| `*`                     | every eligible concrete receiver is selected                               |
| `none`, empty or absent | no receiver is selected, so ordinary run classification applies            |

With `stanza_receiver_roots = db`, and [`ZA0004`](https://github.com/Nickelony/Stanza/wiki/ZA0004-Separate-invocation-runs) and [`ZA0055`](https://github.com/Nickelony/Stanza/wiki/ZA0055-Keep-a-run-of-statements-together) enabled:

```csharp
// Before
db.Add(cart);

await db.Flush();
await db.Refresh(cart);

// After
db.Add(cart);
await db.Flush();
await db.Refresh(cart);
```

- An entry matches any complete receiver name: `db` selects `service.db.Items`.
  Unselected operations stay outside receiver groups, even when their root matches a selected operation.
- Selection ignores a leading `this` or `base`, so `this.db` selects the same receiver as `db`.
  A call directly on `this` or `base` has no concrete receiver.
- [`stanza_receiver_comparison`](https://github.com/Nickelony/Stanza/wiki/stanza_receiver_comparison) chooses whether two selected receivers are the same by root or by full path.
- The key takes effect only where [`ZA0004`](https://github.com/Nickelony/Stanza/wiki/ZA0004-Separate-invocation-runs), [`ZA0010`](https://github.com/Nickelony/Stanza/wiki/ZA0010-Separate-receiver-groups) or [`ZA0064`](https://github.com/Nickelony/Stanza/wiki/ZA0064-Keep-a-receiver-group-together) is enabled; elsewhere [`ZA0006`](https://github.com/Nickelony/Stanza/wiki/ZA0006-Separate-await-runs) reads awaited calls as usual.
- [`ZA0004`](https://github.com/Nickelony/Stanza/wiki/ZA0004-Separate-invocation-runs) separates the runs of calls on a receiver, and [`ZA0055`](https://github.com/Nickelony/Stanza/wiki/ZA0055-Keep-a-run-of-statements-together) keeps them tight while [`ZA0004`](https://github.com/Nickelony/Stanza/wiki/ZA0004-Separate-invocation-runs) is enabled.
  [`ZA0010`](https://github.com/Nickelony/Stanza/wiki/ZA0010-Separate-receiver-groups) and [`ZA0064`](https://github.com/Nickelony/Stanza/wiki/ZA0064-Keep-a-receiver-group-together) also group member writes, with or without [`ZA0004`](https://github.com/Nickelony/Stanza/wiki/ZA0004-Separate-invocation-runs).
- A receiver is a path of names and members. Parentheses and null suppression are transparent;
  calls, indexers and conditional access do not qualify as receiver paths.
- Multi-line operations, assertions, guards, comments and directives end a receiver run, and so does a statement that shares its line with another.
  A blank line between two sections of a group stays.
- The JS and Python tools read the same selection under their own spelling.
  [JS Stanza](https://github.com/Nickelony/Stanza/blob/main/js/docs/Settings.md) compares each name exactly with a segment of the path, after it removes a leading `this`, `self` or `super`,
  and writes a private member as `#db`.
  [Python Stanza](https://github.com/Nickelony/Stanza/blob/main/py/docs/Configuration.md#receiver-groups) reads each entry as a case-sensitive glob.
