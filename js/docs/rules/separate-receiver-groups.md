# separate-receiver-groups

**Level: opt-in.**
Separates consecutive ordinary operations on a selected receiver from neighboring groups.

```ts
// Before - path comparison
object.call1();
object.call2();
object.child.call1();
object.child.call2();

// After - path comparison
object.call1();
object.call2();

object.child.call1();
object.child.call2();
```

```ts
// Unchanged - root comparison
object.call1();
object.call2();
object.child.call1();
object.child.call2();
```

Both examples set `receiverRoots: ['*']`; the first sets `receiverComparison: 'path'` and the second keeps the default `'root'`.
The [invocation settings](separate-invocation-runs.md#settings) define selection and comparison.

- A group consists of consecutive direct calls or member writes on the same selected receiver.
- Calls, awaited calls, assignments, compound assignments and updates can share a group.
- At least one group beside a boundary must contain two operations. Distinct singleton roots stay tight.
- A declaration, bare-local assignment, unsupported receiver, comment or multi-line operation ends a group.
- Receiver paths exclude the called method or assigned member. Root comparison groups `object` and `object.child`;
  path comparison separates them.
- Assertions, guard helpers and test calls retain their own policies, even with wildcard selection.
- Existing separators retain priority. The rule adds no padding at either end of a container.
- [Receiver keeping](keep-receiver-groups-together.md) removes inner gaps independently. Both rules can be enabled.
- [`keep-two-units-together`](keep-two-units-together.md) owns a container of two single-line statements while that rule is enabled.

## C# counterpart

[ZA0019 - Separate receiver groups](https://github.com/Nickelony/Stanza/wiki/ZA0019-Separate-receiver-groups).
