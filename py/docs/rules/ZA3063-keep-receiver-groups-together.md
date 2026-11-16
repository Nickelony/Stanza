# ZA3063 - keep-receiver-groups-together

**Level: opt-in.**
Removes the blank lines inside a group of direct operations on a selected receiver, except between sections.

With `receiver-roots = ["*"]` and `receiver-comparison = "path"`:

```python
# Before
object.name = name

object.save()

object.child.name = child_name

object.child.save()

# After
object.name = name
object.save()

object.child.name = child_name
object.child.save()
```

- The rule removes the blank lines inside a group of operations on one selected receiver,
  without [`ZA3054`](ZA3054-keep-statement-runs-together.md) or [`ZA3004`](ZA3004-separate-invocation-runs.md).
  [Receiver groups](../Configuration.md#receiver-groups) says which operations form a group.
- A tight section of two or more operations stays apart from another such section,
  and so does a lone operation between two sections. A lone operation beside a section joins it.
- One finding removes every blank line of its group, so one fix and a fix of every finding agree.
  A `# noqa` silences the gap above its line alone, which the other rules then judge as if this rule were off there.
- [`ZA3052`](ZA3052-gather-loose-statements.md) reads a gap of the group as tight, so no statement of the group joins a loose run.
- A subscription call on the receiver, such as `db.changed.connect(handler)`, joins the group.
  A blank line that [`ZA3007`](ZA3007-separate-subscription-runs.md) or another rule asks for inside the group
  moves above it, as the [rule map](../Rule-map.md#separation-a-keeper-displaces) says.
- The rule asks for no blank line around the group. [`ZA3019`](ZA3019-separate-receiver-groups.md) adds those,
  and both can be enabled.

## Options

| Key | Default | Effect |
| --- | --- | --- |
| `receiver-roots` | `[]` | Patterns selecting direct receivers; empty selects nothing and `"*"` selects all. |
| `receiver-comparison` | `"root"` | Compare the first meaningful root, or the full owner `path`. |

The [configuration page](../Configuration.md#receiver-groups) defines selector normalization and path comparison.

## C# counterpart

[ZA0063 - Keep a receiver group together](https://github.com/Nickelony/Stanza/wiki/ZA0063-Keep-a-receiver-group-together).

## JS counterpart

[`keep-receiver-groups-together`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/keep-receiver-groups-together.md).
