# ZA0007 - Separate the import directive groups

**Suggestion, off by default.**
Separates adjacent import directives of different kinds with a blank line.

```csharp
// Before
global using System;
global using System.Linq;
using System.Numerics;
using System.Text;

// After
global using System;
global using System.Linq;

using System.Numerics;
using System.Text;
```

- A directive's kind comes from its `global` and `static` keywords and from its alias form, `Name = ...`,
  not from the namespace it names.
- `extern alias` is a kind of its own.
- A group of one directive gets a blank line between it and each neighbor of another kind,
  the same as a longer group.
