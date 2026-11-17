# ZA0406 - Separate a suppression block

**Suggestion, on by default.**
Writes a blank line above a `#pragma warning disable` directive and below the `#pragma warning restore` directive that closes it,
except beside a brace of the block that holds it, or another directive.

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
```

- A `restore` closes the latest open `disable` of the same codes, whatever the order they are listed in; a pragma with no code names every warning.
- By default only the outside of the block is judged: the lines inside it stay as written.
  [`stanza_suppression_block_padding`](https://github.com/Nickelony/Stanza/wiki/stanza_suppression_block_padding) set to `padded` also writes a blank line below the `disable` and above the `restore`.
- A pragma with no pair, such as a `disable` at the top of a file that nothing restores, stays as written.
  So does a `disable` and a `restore` of different codes, and a pair that stands in two containers.
- Both directives must stand between members, statements or `using` directives of one container.
  A pragma inside an argument list, a parameter list or an initializer keeps its gap.
- Only a directive in active code is judged, so a pragma in disabled text is left alone.
- This rule shares with [`ZA0402`](https://github.com/Nickelony/Stanza/wiki/ZA0402-Separate-the-conditional-block-from-its-neighbors) its exceptions for a brace, a switch label, another directive and the edge of a file,
  and its rule that a comment directly above the directive moves with it.
- The grouping rules end a run at the directive where this rule is enabled, as [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap) say.
- [`ZA0407`](https://github.com/Nickelony/Stanza/wiki/ZA0407-Remove-directive-block-padding) removes the blank lines inside a block that is `tight`, which is the default.
  Leave this rule disabled to leave the outside of a block as written.
