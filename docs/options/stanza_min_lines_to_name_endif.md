# stanza_min_lines_to_name_endif

Controls how many lines a conditional block holds before [`ZA0404`](https://github.com/Nickelony/Stanza/wiki/ZA0404-Name-the-closing-conditional-directive) names its `#endif`.
Default: `10`.
Read by [`ZA0404`](https://github.com/Nickelony/Stanza/wiki/ZA0404-Name-the-closing-conditional-directive).

| Value                                           | Effect                                                       |
| ----------------------------------------------- | ------------------------------------------------------------ |
| `0`                                             | every block is named                                         |
| a number from 1 to 1000                         | a block is named from that many lines between its directives |
| below zero, above 1000, absent, or not a number | the default                                                  |

At a threshold of `0`, a block of one line is named too:

```csharp
// Before
#if DEBUG
private int _count;
#endif

// After at a threshold of 0
#if DEBUG
private int _count;
#endif // DEBUG
```

At the default, the same block stays as written:

```csharp
// Unchanged - one line between the directives
#if DEBUG
private int _count;
#endif
```

The count covers the lines between the two directives, so a block with a nested block counts the nested lines.
