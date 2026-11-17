# stanza_conditional_block_padding

Controls whether [`ZA0402`](https://github.com/Nickelony/Stanza/wiki/ZA0402-Separate-the-conditional-block-from-its-neighbors) also writes a blank line inside a conditional block, beside `#if`, `#elif`, `#else` and `#endif`.
Default: `tight`.
Read by [`ZA0402`](https://github.com/Nickelony/Stanza/wiki/ZA0402-Separate-the-conditional-block-from-its-neighbors), and by [`ZA0407`](https://github.com/Nickelony/Stanza/wiki/ZA0407-Remove-directive-block-padding), which removes the blank lines inside a `tight` block.

| Value         | Effect                                                                   |
| ------------- | ------------------------------------------------------------------------ |
| `tight`       | a blank line stands above `#if` and below `#endif`, and none inside      |
| `padded`      | a blank line stands on each side of `#if`, `#elif`, `#else` and `#endif` |
| anything else | as `tight`                                                               |

```csharp
// Before
Prepare();
#if TRACE
Trace();
#else
Log();
#endif
Finish();

// After
Prepare();

#if TRACE
Trace();
#else
Log();
#endif

Finish();

// After with padded
Prepare();

#if TRACE

Trace();

#else

Log();

#endif

Finish();
```

- `padded` writes the blank lines inside only where the units on both sides of the gap are whole units,
  which is the test [`ZA0402`](https://github.com/Nickelony/Stanza/wiki/ZA0402-Separate-the-conditional-block-from-its-neighbors) applies to the outside of a block.
- `tight` leaves the blank lines inside a block to [`ZA0407`](https://github.com/Nickelony/Stanza/wiki/ZA0407-Remove-directive-block-padding), which removes them where it is on.
- A directive beside another directive gets no blank line, so an empty branch keeps its two directives together.
- Between members CSharpier drops the blank line above an `#elif`, an `#else` and an `#endif` that a member follows,
  as [Working with formatters](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#layout-that-csharpier-undoes) says.
  Among statements it keeps them.
- [`stanza_region_padding`](https://github.com/Nickelony/Stanza/wiki/stanza_region_padding) and [`stanza_suppression_block_padding`](https://github.com/Nickelony/Stanza/wiki/stanza_suppression_block_padding) choose the same for the other directive blocks.
