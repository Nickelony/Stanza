# ZA0007 - Separate the import section

**Warning, on by default.**
Adds a blank line between the `extern alias` and `using` directives of a file or namespace and the first member after them.

```csharp
// Before
using System;
using System.Linq;
namespace App;

class Widget
{
	internal int Count;
}

// After
using System;
using System.Linq;

namespace App;

class Widget
{
	internal int Count;
}
```

- The rule fires whether or not a neighbor spans more than one line,
  because the import section is one [unit](https://github.com/Nickelony/Stanza/wiki/Terms) however its directives are written.
- The rule never writes a blank line inside the section; `ZA0008` owns the [gaps](https://github.com/Nickelony/Stanza/wiki/Terms) between directives of different kinds.
