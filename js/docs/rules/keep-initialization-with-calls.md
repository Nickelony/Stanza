# keep-initialization-with-calls

**Level: opt-in.**
Removes the blank line between a single-line initialization that opens a stanza and its first direct call.

```ts
// Before
print('test');

const item = new Item();

item.call1();
item.call2();
item.call3();

// After
print('test');

const item = new Item();
item.call1();
item.call2();
item.call3();

// Unchanged - the initialization belongs to the stanza above
print('test');
const item = new Item();

item.call1();
item.call2();
item.call3();

// Unchanged - a multi-line first call stays apart
print('test');

const item = new Item();

item.call1(
	first,
	second,
);

item.call2();
item.call3();
```

- The initialization declares one plain local name from a call or constructor, optionally awaited.
  It opens the container or follows a blank line that holds only whitespace.
- The first call must be a single-line expression statement directly on that name, optionally awaited.
  A chained receiver, a computed member, a destructuring declaration and an assignment stay outside the rule.
- The rule keeps only the first gap. The invocation rules judge the calls below it.
  The [receiver setting](separate-invocation-runs.md#settings) can group calls of different methods on a configured receiver.
- A multi-line initialization or first call, a comment on either edge and a keep or run rule holding the upper gap prevent the join.
- A blank line another enabled rule asks for above the initialization counts as standing there,
  unless a rule that removes the blank lines of a run takes that gap.
- A separation another rule asks for inside the kept group moves above the group, as the [rule map](../Rule-map.md#rules-that-keep-a-pair-together) says.
- [`keep-two-units-together`](keep-two-units-together.md) owns a container of two single-line statements while that rule is enabled.

## C# counterpart

[ZA0062 - Keep an initialization with its calls](https://github.com/Nickelony/Stanza/wiki/ZA0062-Keep-an-initialization-with-its-calls).

## Python counterpart

[ZA3062 - keep-initialization-with-calls](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3062-keep-initialization-with-calls.md).
