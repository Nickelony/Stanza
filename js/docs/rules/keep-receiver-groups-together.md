# keep-receiver-groups-together

**Level: opt-in.**
Removes eligible blank lines inside a group of ordinary operations on one selected receiver.

```ts
// Before
db.add(cart);

await db.flush();

// After
db.add(cart);
await db.flush();
```

This example sets `receiverRoots: ['db']` and uses the default `receiverComparison: 'root'`.
The [invocation settings](separate-invocation-runs.md#settings) define selection and root/path comparison.

- Direct calls, awaited calls, member assignments, compound assignments and updates can share a group.
- Only selected receivers participate; global [statement keeping](keep-statement-runs-together.md) is independent.
- Comments, directives, multi-line operations and unsupported receivers end a group. Assertions, guard helpers
  and test calls keep their own roles. Declarations and bare-local assignments remain outside.
- Authored sections of multiple tight operations keep their separating blank line. Gaps between isolated
  operations can join.
- The keeper wins ordinary family edges inside its group.
- A separation another rule asks for inside the kept group moves above the group, as the [rule map](../Rule-map.md#rules-that-keep-a-pair-together) says.
- [Receiver separation](separate-receiver-groups.md) adds outer gaps independently. This rule alone demands none.
- Initialization keeping retains its own eligibility and stanza-anchor conditions.
- [`keep-two-units-together`](keep-two-units-together.md) owns a container of two single-line statements while that rule is enabled.

## C# counterpart

[ZA0064 - Keep a receiver group together](https://github.com/Nickelony/Stanza/wiki/ZA0064-Keep-a-receiver-group-together).
