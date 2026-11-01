# separate-directives

**Level: minimal.**
Adds a blank line below the shebang and the directive prologue of a file or function.

```ts
// Before
#!/usr/bin/env node
'use strict';
run();

// After
#!/usr/bin/env node

'use strict';

run();
```

- A directive is a string statement at the top, such as `'use strict'` or `'use client'`, and the statements of the prologue stay together.
- A function body has a prologue of its own.
- A comment directly above the first statement moves with it, so the blank line goes above the comment.
- A string statement below other code is not a directive and is left alone.

## C# counterpart

[ZA0017 - Separate a file-scoped namespace declaration from its first member](https://github.com/Nickelony/Stanza/wiki/ZA0017-Separate-a-file-scoped-namespace-declaration-from-its-first-member),
the header of a C# file.
