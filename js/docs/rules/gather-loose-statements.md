# gather-loose-statements

**Level: strong.**
Removes the blank lines between the statements of a loose run that holds more than `maxLooseRunSize` statements,
so a block does not read as a column of lines that each stand alone.

```ts
// Before
const base = load();

const rows = parse(base);

const total = sum(rows);

const label = name(total);

// After
const base = load();
const rows = parse(base);
const total = sum(rows);
const label = name(total);
```

- A loose run is a chain of single-line statements that only a blank line separates and no other rule asks to separate.
  The rule reports every blank line of a run past the limit, and the fix removes the blank lines of the whole run.
- A multi-line statement, a comment, a trailing comment, an import, a directive, a shebang or a gap with no blank line ends a run.
- A statement that sits tight against its other neighbor stays out of the run, so the blank line that sets that group apart stays.
  A neighbor that a rule asks to set apart, as `separate-multi-line-constructs` does beside a multi-line statement, is not tight.
- The rule never removes a blank line that another enabled rule asks for, so a run that a run rule,
  [`separate-multi-line-constructs`](separate-multi-line-constructs.md) or [`separate-commented-siblings`](separate-commented-siblings.md) ends is counted up to that gap.
  A rule that is off hands its gaps back, so the gap counts as loose again.
- Where [`separate-jump-statements`](separate-jump-statements.md) is enabled, the blank line above a jump stays when the stanza above it spans two lines once the run is gathered.
  The jump then ends the run, and a run that holds no more statements than the limit is left as written.
- A jump below a single line joins the run, because `separate-jump-statements` asks for nothing there.
- The fix of any gap removes the blank lines of its whole run, so every fix order gathers the run alike.
- The rule judges the statements of a block, a switch case, a static block, a module block and a file.
  The members of a class, an interface and a type literal belong to [`gather-loose-members`](gather-loose-members.md).
- A container of two statements belongs to [`keep-two-units-together`](keep-two-units-together.md), and the elements of a list keep their blank lines.
- Where [`keep-statement-runs-together`](keep-statement-runs-together.md) is also enabled, it reports a blank line that both rules remove.

## Settings

```js
export default [{ settings: { stanza: { maxLooseRunSize: 3 } } }];
```

`maxLooseRunSize` is the number of units a loose run may hold before the gather rules gather it.
It is a whole number from 0 to 1000, and 3 by default; with `0` neither rule gathers a run.
A value that is no whole number in that range is an error that names the key.
[`gather-loose-members`](gather-loose-members.md) reads the key too, so both rules count a run by the same limit.

## C# counterpart

[ZA0052 - Gather a run of loose statements](https://github.com/Nickelony/Stanza/wiki/ZA0052-Gather-a-run-of-loose-statements),
whose limit is [`stanza_max_loose_run_size`](https://github.com/Nickelony/Stanza/wiki/stanza_max_loose_run_size).
