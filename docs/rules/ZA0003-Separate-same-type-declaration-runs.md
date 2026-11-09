# ZA0003 - Separate same-type declaration runs

**Suggestion, off by default.**
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
- Two neighboring `var` declarations always match, a `using var` among them, because the rule compares the type as written,
  not the type the compiler infers.
- A `const` declaration runs only with other `const` declarations of its type, never with a non-`const` one.
- A declaration with no neighbor of its type forms no run of its own,
  so it gets a blank line only where a neighboring run begins or ends.
- A [gap](https://github.com/Nickelony/Stanza/wiki/Terms) that another [grouping rule](https://github.com/Nickelony/Stanza/wiki/Terms) owns is left to that rule
  unless that rule is [off](https://github.com/Nickelony/Stanza/wiki/Terms) there.
- A run ends where one of those rules asks for a blank line, so `static int a; int b; string c;` holds no `int` run while `ZA0014` is on.
- [`ZA0054`](https://github.com/Nickelony/Stanza/wiki/ZA0054-Keep-a-run-of-statements-together) and [`ZA0055`](https://github.com/Nickelony/Stanza/wiki/ZA0055-Keep-a-run-of-members-together) remove the blank lines inside a run of declarations of one type where they are on.
