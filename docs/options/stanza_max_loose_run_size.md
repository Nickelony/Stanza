# stanza_max_loose_run_size

Controls how many siblings a [loose run](https://github.com/Nickelony/Stanza/wiki/Terms) may hold before [`ZA0052`](https://github.com/Nickelony/Stanza/wiki/ZA0052-Gather-a-run-of-loose-statements) or [`ZA0053`](https://github.com/Nickelony/Stanza/wiki/ZA0053-Gather-a-run-of-loose-members) gathers it.
Default: `3`.
Read by both rules, which count statements and members. Their pages show a run above the default limit.

| Value                                           | Effect                                                  |
| ----------------------------------------------- | ------------------------------------------------------- |
| `0`                                             | the rules report no run                                 |
| a number from 1 to 1000                         | a run is gathered when it holds more siblings than that |
| below zero, above 1000, absent, or not a number | the default                                             |

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

- The count covers the siblings of one run, not of the whole container, so a comment or a multi-line sibling starts the count again.
- The rules share the limit and read separate parts of a file. `ZA0052` counts its top-level statements, and `ZA0053` counts the members below them.
  Imports count for neither.
- With a limit of `1`, a container of two siblings belongs to [`ZA0051`](https://github.com/Nickelony/Stanza/wiki/ZA0051-Keep-the-two-units-of-a-container-together) where it judges the container.
  The loose run rules judge the pair where `ZA0051` is off, or where it skips a container that holds a local function.
- Neither rule judges a container where a conditional directive can change the runs, on the terms [`ZA0051`](https://github.com/Nickelony/Stanza/wiki/ZA0051-Keep-the-two-units-of-a-container-together) states.
