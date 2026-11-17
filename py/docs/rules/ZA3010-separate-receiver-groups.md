# ZA3010 - separate-receiver-groups

**Level: opt-in.**
Separates consecutive direct operations selected by their receiver.

With `receiver-roots = ["*"]` and `receiver-comparison = "path"`:

```python
# Before - path comparison
object.first()
object.second()
object.child.first()
object.child.second()

# After - path comparison
object.first()
object.second()

object.child.first()
object.child.second()
```

- A blank line goes between two neighboring groups, or between a group and another statement,
  when at least one side holds two or more operations. [Receiver groups](../Configuration.md#receiver-groups) says which operations form a group.
- The rule adds no blank line at the start or at the end of a body.
- An earlier rule of the [owner order](../Rule-map.md#one-owner-per-gap) reports a gap both ask for.
- A receiver border ends the runs of the other grouping rules, so `ZA3052` and `ZA3055` never remove it.
- A subscription call on the receiver, such as `db.changed.connect(handler)`, belongs to its group,
  while [`ZA3008`](ZA3008-separate-subscription-runs.md) still sets a run of subscription calls apart inside it.
- [`ZA3064`](ZA3064-keep-receiver-groups-together.md) removes the blank lines inside a group, and both can be enabled.

## Options

| Key | Default | Effect |
| --- | --- | --- |
| `receiver-roots` | `[]` | Patterns selecting direct receivers; empty selects nothing and `"*"` selects all. |
| `receiver-comparison` | `"root"` | Compare the first name of the owner, or the full owner `path`. |

The [configuration page](../Configuration.md#receiver-groups) defines selector normalization and path comparison.

## C# counterpart

[ZA0010 - Separate receiver groups](https://github.com/Nickelony/Stanza/wiki/ZA0010-Separate-receiver-groups).

## JS counterpart

[`separate-receiver-groups`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-receiver-groups.md).
