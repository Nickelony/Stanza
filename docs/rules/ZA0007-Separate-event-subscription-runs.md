# ZA0007 - Separate event subscription runs

**Suggestion, off by default.**
Separates a [run](https://github.com/Nickelony/Stanza/wiki/Terms) of two or more neighboring event subscriptions from its neighbors,
so the wiring of a component reads as one stanza.

```csharp
// Before
Initialize();
Saved += OnSaved;
Closed += OnClosed;
Start();

// After
Initialize();

Saved += OnSaved;
Closed += OnClosed;

Start();
```

- A subscription is a statement `x += handler;` or `x -= handler;` whose left side is an event,
  so `Saved += OnSaved;`, `button.Click -= OnClick;` and `Button.Shared += OnShared;` are all subscriptions,
  and so is `button?.Click += OnClick;` in C# 14.
- Subscribing and unsubscribing join one run, and an event name does not split it,
  so `Click -= OnClick;` then `Click += OnClick;` stays together.
- A lone subscription stays tight, as a lone call does.
- Only the symbol tells `Saved += OnSaved;` from `count += step;`, so this rule reads a semantic model.
  It reads the symbol of a `+=` or `-=` statement alone, once for each statement, and reads nothing while the rule is [off](https://github.com/Nickelony/Stanza/wiki/Terms).
- A statement with no symbol, as in a file with a missing reference, is no subscription, and this rule reports nothing there.
  A number, and a field of a delegate type, is no event either.
- The rule only adds blank lines, so two projects that compile one file with different references cannot undo each other's fixes through it.
- A declaration on its own, directly above a run of subscriptions, takes its blank line from this rule,
  and stays tight against the run while this rule is [off](https://github.com/Nickelony/Stanza/wiki/Terms).
- A gap where two runs meet goes to the rule with the lower ID, as [One run of statements, several owners](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#one-run-of-statements-several-owners) says.
- [`ZA0008`](https://github.com/Nickelony/Stanza/wiki/ZA0008-Separate-assignment-runs) takes a subscription for a compound assignment,
  and for an assignment with no target kind where [`stanza_assignment_targets`](https://github.com/Nickelony/Stanza/wiki/stanza_assignment_targets) is `split`.
- Where both rules are on, this rule owns the gap at each end of its run, and `ZA0008` leaves it.
  So `count = 1;` above `Saved += OnSaved;` and `Closed += OnClosed;` gets a blank line below the assignment.
- Where this rule is off, `ZA0008` reads the subscriptions as assignments,
  and under its default `mixed` it sets that whole run apart from its neighbors.
- [`ZA0054`](https://github.com/Nickelony/Stanza/wiki/ZA0054-Keep-a-run-of-statements-together) removes the blank lines inside a run of event subscriptions where it is on.
