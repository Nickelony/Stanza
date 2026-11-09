# separate-commented-siblings

**Level: strong.**
Adds a blank line above a comment that leads a single-line statement or member, so a comment introduces a group.

```ts
// Before
open();
// Reads the file.
read();

// After
open();

// Reads the file.
read();
```

- The rule asks for the gap only when both neighbors are single-line, because [`separate-multi-line-constructs`](separate-multi-line-constructs.md) owns the others.
- A comment separated from the unit below by a blank line is not leading it.
- A comment that trails the unit above stays with it.
- A tool directive does not lead a group:
  `// @ts-expect-error`, `// @ts-ignore`, `// eslint-disable-next-line`, `/* istanbul ignore next */`, `// prettier-ignore`
  and similar comments annotate the line below and stay adjacent.
- A documentation comment counts, so documented interface members and class members stand apart.

## C# counterpart

[ZA0012 - Separate a commented sibling](https://github.com/Nickelony/Stanza/wiki/ZA0012-Separate-a-commented-sibling).
