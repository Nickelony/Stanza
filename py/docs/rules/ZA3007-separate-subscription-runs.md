# ZA3007 - separate-subscription-runs

**Level: strong.**
Separates a run of two or more neighboring event subscriptions from the statements beside it, so the wiring of a component reads as one stanza.

```python
# Before
setup()
saved.connect(on_saved)
closed.connect(on_closed)
start()

# After
setup()

saved.connect(on_saved)
closed.connect(on_closed)

start()
```

- A subscription is a call statement, awaited or not, that `subscription-roots` matches, such as `saved.connect(on_saved)`.
- Python has no event syntax, so only a name tells `saved.connect(on_saved)` from `database.connect(url)`.
  The rule reads names and no types, so narrow `subscription-roots` when a call of a listed name is no subscription.
- Subscribing and unsubscribing join one run, and the event does not split it,
  so `saved.disconnect(on_saved)` then `closed.connect(on_closed)` stays together.
- A call that `assertion-roots` matches is an assertion and stays with [`ZA3005`](ZA3005-separate-assertion-runs.md).
- An assignment such as `connection = database.connect()` is a declaration, not a subscription.
- A lone subscription stays tight, as a lone call does.
- While this rule is off, a subscription is an invocation, and [`ZA3004`](ZA3004-separate-invocation-runs.md) joins only calls of one callee path.
  With this rule on, `saved.connect(...)` beside `closed.connect(...)` is one run.
- [`ZA3054`](ZA3054-keep-statement-runs-together.md) removes the blank lines inside a run of subscriptions where it is enabled.

## Options

| Key | Default | Effect |
| --- | --- | --- |
| `subscription-roots` | `["connect", "disconnect", "subscribe", "unsubscribe", "add_listener", "remove_listener", "add_done_callback"]` | The patterns that make a call a subscription. |

A pattern is a shell-style glob that matches the dotted path of the callee or any name along it, as the [configuration page](../Configuration.md#patterns) says.
Unlike the exact names of the C# and JS roots, it can match a family of names.

## C# counterpart

[ZA0007 - Separate event subscription runs](https://github.com/Nickelony/Stanza/wiki/ZA0007-Separate-event-subscription-runs).

## JS counterpart

None. JS Stanza reads `addEventListener(...)` and `emitter.on(...)` runs as invocation runs of one callee path.
