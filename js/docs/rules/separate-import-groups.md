# separate-import-groups

**Level: strong.**
Adds a blank line between two adjacent imports of different kinds, so a side-effect import stands apart from the value imports.

```ts
// Before
import './polyfills.ts';
import { readFile } from 'node:fs/promises';
import { parse } from './parse.ts';

// After
import './polyfills.ts';

import { readFile } from 'node:fs/promises';
import { parse } from './parse.ts';
```

With the `typeImports` option, a type import stands apart from the value imports too:

```ts
// Before
import type { Order } from './order.ts';
import { parse } from './parse.ts';

// After
import type { Order } from './order.ts';

import { parse } from './parse.ts';
```

- The kinds are the value import, the side-effect import and, with `typeImports`, the type import.
  A side-effect import binds nothing, as in `import './polyfills.ts'`.
- Two imports of one kind stay as written, and the rule removes no blank line.
- `import { type Id } from 'x'` and `import x = require('x')` are value imports.
  A re-export, such as `export { a } from 'x'`, is no import, so the rule leaves its gaps alone.
- A group of one import gets a blank line between it and each neighbor of another kind, as a longer group does.
- A comment directly above an import moves with it, so the blank line goes above the comment.
- The rule judges the imports of a file and of a `declare module` block, and only the gap between two imports.
  [`separate-import-section`](separate-import-section.md) owns the gap between the last leading import and the first statement.
- `@ianvs/prettier-plugin-sort-imports` puts a blank line around each side-effect import, so the rule and the plugin agree on that kind.
- The plugin keeps a type import with the value imports of its group unless `importOrder` holds a `<TYPES>` group between two `""` entries.
  Without that group it removes the blank line the option writes, so the two never settle.
- Set `typeImports` only when `importOrder` separates the types, or when no import sorter runs.

## Options

| Option | Default | Effect |
| --- | --- | --- |
| `typeImports` | `false` | Also separates `import type` declarations from the value imports beside them. |

## C# counterpart

[ZA0016 - Separate the import directive groups](https://github.com/Nickelony/Stanza/wiki/ZA0016-Separate-the-import-directive-groups).
