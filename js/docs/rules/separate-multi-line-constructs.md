# separate-multi-line-constructs

**Level: moderate.**
Adds a blank line at a gap when either neighbor spans more than one line.

```ts
// Before
const options = buildOptions();
for (const section of options.sections) {
	bind(section);
}
logger.info('Starting the host.');

// After
const options = buildOptions();

for (const section of options.sections) {
	bind(section);
}

logger.info('Starting the host.');
```

- A construct needs no block: a wrapped call, a chain of calls, a template literal over several lines
  and a member with a decorator on its own line all count.
- A comment directly above the second unit travels with it, so the blank line goes above the comment.
- A comment that trails the first unit on its last line stays with that unit.
- Two imports in a row stay together, even when one spans several lines.
- Overload signatures, and a getter with its setter, stay together, as the [quirks](../Quirks.md#gaps-no-rule-owns) say.
- The rule judges the statements of a file, a block, a `static` block, a `declare module` block and a switch case,
  and the members of a class, an interface and a type literal.
- The children of a JSX element belong to [`separate-multi-line-jsx-children`](separate-multi-line-jsx-children.md),
  and the cases of a `switch` statement to [`separate-multi-line-switch-cases`](separate-multi-line-switch-cases.md).

## C# counterpart

[ZA0001 - Separate multi-line constructs](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-multi-line-constructs).
