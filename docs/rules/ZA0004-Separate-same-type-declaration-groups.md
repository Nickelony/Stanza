# ZA0004 - Separate same-type declaration groups

**Warning, on by default.**
Adds a blank line between a [run](https://github.com/Nickelony/Stanza/wiki/Terms) of two or more neighboring declarations of the same type and the declarations beside it.

```csharp
// Before
long nodeLineSpan = 1;
int startLine = 2;
int startPosition = 3;
bool hasComment = false;
bool hasDirective = false;
string leadingTrivia = string.Empty;

// After
long nodeLineSpan = 1;

int startLine = 2;
int startPosition = 3;

bool hasComment = false;
bool hasDirective = false;

string leadingTrivia = string.Empty;
```

- Whitespace inside a type does not matter, so `Dictionary<string, int>` and `Dictionary<string,int>` match.
- Two neighboring `var` declarations always match, because the rule compares the type as written,
  not the type the compiler infers.
- A `const` declaration runs only with other `const` declarations of its type, never with a non-`const` one.
- A declaration with no neighbor of its type forms no run of its own,
  so it gets a blank line only where a neighboring run begins or ends.
- A [gap](https://github.com/Nickelony/Stanza/wiki/Terms) that [`ZA0001`](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-declaration-and-invocation-runs), [`ZA0002`](https://github.com/Nickelony/Stanza/wiki/ZA0002-Separate-member-groups) or [`ZA0003`](https://github.com/Nickelony/Stanza/wiki/ZA0003-Separate-modifier-groups) owns is left to that rule
  unless that rule is [off](https://github.com/Nickelony/Stanza/wiki/Terms) there.
