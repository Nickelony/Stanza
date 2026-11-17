# keep-dependent-bindings-together

**Level: opt-in.**
Removes eligible blank lines between consecutive single-line bindings whose initializers consume the preceding value.

```ts
// Before
const file = await fileHandle.getFileAsync();

const bytes = await file.arrayBufferAsync();

const stream = new MemoryStream(bytes);

// After
const file = await fileHandle.getFileAsync();
const bytes = await file.arrayBufferAsync();
const stream = new MemoryStream(bytes);
```

- Each declaration initializes one plain name with `const`, `let` or `var`.
  The next initializer must read that immediately preceding name as a value.
- Object and array destructuring, multiple declarators, exports, ambient declarations and resource declarations are excluded.
  [Declaration shapes](separate-declaration-runs.md#settings) retain their boundaries even when a destructured name feeds the next declaration.
- A property name, object key, type annotation, string or write-only assignment target does not count as a value read.
  References inside function or class bodies do not connect the outer bindings.
  Class heritage and JSX component values count when they read the preceding name, and `import.meta` and `new.target` read none.
- An initializer that reassigns or updates the preceding name does not connect the bindings.
  A legal `var` redeclaration can connect when its initializer reads that name without changing it inside the initializer.
- Comments, directives, statements that share a line with another and multi-line declarations end chains.
  The rule does not search ahead or connect statements that merely share an earlier input.
- Authored sections of multiple tight bindings remain separate.
  Each report of a chain carries the fix that removes every eligible blank line of the chain, and only the first of a chain over 64 blank lines does,
  so applying that fix alone and applying a batch produce the same layout.
- The keeper owns inner gaps independently of ordinary declaration-kind grouping, and asks for no blank line around the chain.
- A separation another rule asks for inside the kept group moves above the group, as the [rule map](../Rule-map.md#rules-that-keep-a-pair-together) says.
- [`keep-two-units-together`](keep-two-units-together.md) owns a container of two single-line statements while that rule is enabled.
- Jump and initialization keeping cannot treat a last binding as lone when this keeper joins its upper gap.

## C# counterpart

[ZA0061 - Keep dependent bindings together](https://github.com/Nickelony/Stanza/wiki/ZA0061-Keep-dependent-bindings-together).
