# stanza_suppression_block_padding

Controls whether [`ZA0406`](https://github.com/Nickelony/Stanza/wiki/ZA0406-Separate-a-suppression-block) also writes a blank line inside a suppression block, below its `#pragma warning disable` and above its `restore`.
Default: `tight`.
Read by [`ZA0406`](https://github.com/Nickelony/Stanza/wiki/ZA0406-Separate-a-suppression-block), and by [`ZA0407`](https://github.com/Nickelony/Stanza/wiki/ZA0407-Remove-directive-block-padding), which removes the blank lines inside a `tight` block.

| Value         | Effect                                                                       |
| ------------- | ---------------------------------------------------------------------------- |
| `tight`       | a blank line stands above the `disable` and below the `restore`, none inside |
| `padded`      | a blank line stands on each side of the `disable` and of the `restore`       |
| anything else | as `tight`                                                                   |

```csharp
// Before
var options = Load();
#pragma warning disable CS0618
var legacy = options.OldSetting;
#pragma warning restore CS0618
Apply(legacy);

// After
var options = Load();

#pragma warning disable CS0618
var legacy = options.OldSetting;
#pragma warning restore CS0618

Apply(legacy);

// After with padded
var options = Load();

#pragma warning disable CS0618

var legacy = options.OldSetting;

#pragma warning restore CS0618

Apply(legacy);
```

- A block is the `disable` and the `restore` of the same codes that [`ZA0406`](https://github.com/Nickelony/Stanza/wiki/ZA0406-Separate-a-suppression-block) pairs, and a pragma with no pair keeps its gaps.
- `tight` leaves the blank lines inside a block to [`ZA0407`](https://github.com/Nickelony/Stanza/wiki/ZA0407-Remove-directive-block-padding), which removes them where it is on.
- CSharpier keeps a blank line inside a suppression block as written, so both values survive it.
- [`stanza_region_padding`](https://github.com/Nickelony/Stanza/wiki/stanza_region_padding) and [`stanza_conditional_block_padding`](https://github.com/Nickelony/Stanza/wiki/stanza_conditional_block_padding) choose the same for the other directive blocks.
