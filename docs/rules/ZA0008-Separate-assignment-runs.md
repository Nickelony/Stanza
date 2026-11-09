# ZA0008 - Separate assignment runs

**Suggestion, off by default.**
Separates a [run](https://github.com/Nickelony/Stanza/wiki/Terms) of two or more neighboring assignments from its neighbors.

```csharp
// Before
Validate(input);
_name = input.Name;
_size = input.Size;
Initialize();

// After
Validate(input);

_name = input.Name;
_size = input.Size;

Initialize();
```

- An assignment is `=`, a compound assignment such as `+=`, or an increment or decrement,
  so `a = 1;`, `this.b = 2;`, `c += 3;` and `d++;` are one run, and a deconstruction such as `(a, b) = Read();` joins them
  unless it declares every name, as `(int a, int b) = Read();` does.
- An `await` or a call on the right side still reads as an assignment.
- By default, assignments form one run whatever their targets are.
  [`stanza_assignment_targets`](https://github.com/Nickelony/Stanza/wiki/stanza_assignment_targets) set to `split` splits a run
  between fields, properties, locals and elements, and its page says which target kind each assignment takes.
- By default, the rule reads syntax only, so `Click += OnClick;` is a compound assignment here, as `count += step;` is.
  Under `split`, a subscription has no target kind and splits nothing.
- Where [`ZA0007`](https://github.com/Nickelony/Stanza/wiki/ZA0007-Separate-event-subscription-runs) is on, it reads the symbol and owns the gap at each end of a run of subscriptions.
  This rule leaves those gaps, so `ZA0007` sets apart a run of subscriptions inside a run of assignments.
- The assignments on either side of such a run are runs of their own, so a lone one asks for no blank line from this rule.
- A lone assignment stays tight, as a lone call does.
- A discard of a call, such as `_ = Read();`, is a call to `Read` and belongs to [`ZA0004`](https://github.com/Nickelony/Stanza/wiki/ZA0004-Separate-invocation-runs),
  and `var (a, b) = Read();` is a declaration that [`ZA0002`](https://github.com/Nickelony/Stanza/wiki/ZA0002-Separate-declaration-runs) judges.
  A discard of a value, such as `_ = value;`, is an assignment.
- A discard of an await, such as `_ = await task;`, is an await statement where [`ZA0006`](https://github.com/Nickelony/Stanza/wiki/ZA0006-Separate-await-runs) is on.
- A declaration on its own, directly above a run of assignments, takes its blank line from this rule,
  and stays tight against the run while this rule is [off](https://github.com/Nickelony/Stanza/wiki/Terms).
- A gap where two runs meet goes to the rule with the lower ID, as [One run of statements, several owners](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#one-run-of-statements-several-owners) says.
- Only the `strong` level enables the rule, because it reports every run of assignments, including one that a call finishes,
  so `button.Text = text; button.Size = size; panel.Add(button);` gets a blank line above `panel.Add(button);`.
- [`ZA0054`](https://github.com/Nickelony/Stanza/wiki/ZA0054-Keep-a-run-of-statements-together) removes the blank lines inside a run of assignments where it is on.
