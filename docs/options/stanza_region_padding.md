# stanza_region_padding

Controls whether [`ZA0401`](https://github.com/Nickelony/Stanza/wiki/ZA0401-Separate-a-region-marker) also writes a blank line inside a region, below `#region` and above `#endregion`.
Default: `padded`.
Read by [`ZA0401`](https://github.com/Nickelony/Stanza/wiki/ZA0401-Separate-a-region-marker), and by [`ZA0404`](https://github.com/Nickelony/Stanza/wiki/ZA0404-Remove-directive-block-padding), which removes the blank lines inside a `tight` region.

| Value         | Effect                                                                      |
| ------------- | --------------------------------------------------------------------------- |
| `padded`      | a blank line stands on each side of `#region` and of `#endregion`           |
| `tight`       | a blank line stands above `#region` and below `#endregion`, and none inside |
| anything else | as `padded`                                                                 |

```csharp
// Before
private int _total;
#region Fields
private int _count;
#endregion
private int _sum;

// After
private int _total;

#region Fields

private int _count;

#endregion

private int _sum;

// After with tight
private int _total;

#region Fields
private int _count;
#endregion

private int _sum;
```

- `tight` leaves the blank lines inside a region to [`ZA0404`](https://github.com/Nickelony/Stanza/wiki/ZA0404-Remove-directive-block-padding), which removes them where it is on.
- The exceptions of [`ZA0401`](https://github.com/Nickelony/Stanza/wiki/ZA0401-Separate-a-region-marker), such as a brace, a switch label or another directive beside a marker, apply to both values.
- [`stanza_conditional_block_padding`](https://github.com/Nickelony/Stanza/wiki/stanza_conditional_block_padding) and [`stanza_suppression_block_padding`](https://github.com/Nickelony/Stanza/wiki/stanza_suppression_block_padding) choose the same for the other directive blocks.
- CSharpier keeps a blank line inside a region as written, so both values survive it.
