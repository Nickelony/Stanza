# separate-import-section

**Level: minimal.**
Adds a blank line between the imports that open a file and the first statement below them.

```ts
// Before
import { readFile } from 'node:fs/promises';
import { parse } from './parse.ts';
export const config = parse(await readFile('a.json', 'utf8'));

// After
import { readFile } from 'node:fs/promises';
import { parse } from './parse.ts';

export const config = parse(await readFile('a.json', 'utf8'));
```

- The section is the `import` statements and `import x = require()` statements at the top, after a shebang and the directives.
- The rule fires whether or not a neighbor spans several lines.
- The gaps between two imports of different kinds belong to [`separate-import-groups`](separate-import-groups.md).
  The groups by origin belong to the sort rule of the team,
  such as `@ianvs/prettier-plugin-sort-imports` or `import-x/order` with `newlines-between`.
- A `declare module` block has an import section of its own.

## C# counterpart

[ZA0015 - Separate the import section](https://github.com/Nickelony/Stanza/wiki/ZA0015-Separate-the-import-section).
