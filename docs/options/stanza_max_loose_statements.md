# stanza_max_loose_statements

Controls how many statements a [loose run](https://github.com/Nickelony/Stanza/wiki/Terms) may hold before [`ZA0052`](https://github.com/Nickelony/Stanza/wiki/ZA0052-Gather-a-run-of-loose-statements) gathers it.
Default: `3`.
Read by [`ZA0052`](https://github.com/Nickelony/Stanza/wiki/ZA0052-Gather-a-run-of-loose-statements), whose page shows a run at the default limit and above it.

| Value                                           | Effect                                                    |
| ----------------------------------------------- | --------------------------------------------------------- |
| `0`                                             | the rule reports no run                                   |
| a number from 1 to 1000                         | a run is gathered when it holds more statements than that |
| below zero, above 1000, absent, or not a number | the default                                               |

At a limit of `2`, two statements stay as written and three are gathered:

```csharp
// Unchanged at a limit of 2 - two statements
total = 0;

count = 1;

// Before
total = 0;

count = 1;

size = 2;

// After at a limit of 2
total = 0;
count = 1;
size = 2;
```

- The count covers the statements of one run, not of the whole block, so a comment or a multi-line statement starts the count again.
- With a limit of `1`, a block of two statements belongs to [`ZA0006`](https://github.com/Nickelony/Stanza/wiki/ZA0006-Keep-the-two-units-of-a-container-together) where it judges the container.
