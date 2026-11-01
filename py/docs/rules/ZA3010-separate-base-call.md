# ZA3010 - separate-base-call

**Level: strong.**
Adds a blank line below a `super()` call that opens a function body, and above one that closes it,
so the base call stands apart from the work of the method.

```python
# Before
class Timer(Base):
    def start(self):
        super().start()
        self.ticks = 0
        self.running = True
        log("started")

# After
class Timer(Base):
    def start(self):
        super().start()

        self.ticks = 0
        self.running = True
        log("started")
```

```python
# Before
class Timer(Base):
    def stop(self):
        self.running = False
        self.ticks = 0
        super().stop()

# After
class Timer(Base):
    def stop(self):
        self.running = False
        self.ticks = 0

        super().stop()
```

- A base call is a statement that calls a member through a chain that starts at a `super()` call,
  such as `super().start()`, `await super().start()`, `super(Timer, self).start()` or `super().items.clear()`.
- An assignment such as `value = super().load()` is a declaration, and a returned base call is a jump that [`ZA3009`](ZA3009-separate-jump-statements.md) owns.
  A call that passes `super()` as an argument, and an explicit parent call such as `Base.start(self)`, are no base calls.
- The rule judges function bodies and leaves the body of an `if`, a loop or any other block alone.
- The opening call is the first statement below the docstring and the guard clauses that open the body,
  as [`ZA3011`](ZA3011-separate-guard-clauses.md) reads them, including the helpers that `guard-roots` lists, and the rule owns the gap below it.
- The closing call is the last statement, and the rule owns the gap above it.
  A body that opens and closes with a base call gets both blank lines, and a base call alone in its body has no gap to set apart.
- No rule asks for a blank line below a function docstring, so the first statement stays against it.
- A body of exactly two single-line statements belongs to [`ZA3051`](ZA3051-keep-two-units-together.md).
- A comment directly above the statement below the call moves with it, so the blank line goes above the comment.
- [`ZA3011`](ZA3011-separate-guard-clauses.md) asks for the same blank line above an opening call that follows guards.
  The first rule in the [owner order](../Rule-map.md#one-owner-per-gap) reports it, and the fixed text is the same.

## Options

| Key | Default | Effect |
| --- | --- | --- |
| `guard-roots` | `[]` | The patterns that make a bare call a guard, so the opening base call may follow it. |

A pattern is a shell-style glob that matches the dotted path of the callee or any name along it, as the [configuration page](../Configuration.md#patterns) says.

## C# counterpart

[ZA0010 - Separate the base call](https://github.com/Nickelony/Stanza/wiki/ZA0010-Separate-the-base-call).

## JS counterpart

[`separate-base-call`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-base-call.md).
