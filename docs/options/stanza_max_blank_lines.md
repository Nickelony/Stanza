# stanza_max_blank_lines

Controls how many blank lines in a row a [run](https://github.com/Nickelony/Stanza/wiki/ZA0056-Collapse-extra-blank-lines) of C# code may keep.
Default: `1`.
Read by [`ZA0056`](https://github.com/Nickelony/Stanza/wiki/ZA0056-Collapse-extra-blank-lines), whose page says which runs it counts.

| Value                                           | Effect                                    |
| ----------------------------------------------- | ----------------------------------------- |
| `0`                                             | no cap: the rule reports nothing          |
| a number from 1 to 1000                         | a run keeps at most that many blank lines |
| below zero, above 1000, absent, or not a number | the default                               |

```csharp
// Before
total = 0;



count = 1;

// After
total = 0;

count = 1;

// After at a cap of 2
total = 0;


count = 1;
```

- The key caps C# code only. [`stanza_markup_max_blank_lines`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_max_blank_lines) caps the gaps between markup elements.
- A cap of `0` is no cap, not a ban on blank lines.
  Rules such as [`ZA0203`](https://github.com/Nickelony/Stanza/wiki/ZA0203-Remove-brace-padding) and the tight rules remove blank lines on their own terms.
