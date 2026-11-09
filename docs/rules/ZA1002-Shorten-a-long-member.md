# ZA1002 - Shorten a long member

**Suggestion, off by default.**
Reports a member whose body holds more statements than
[`stanza_max_member_statements`](https://github.com/Nickelony/Stanza/wiki/stanza_max_member_statements) allows.
It is a [report-only rule](https://github.com/Nickelony/Stanza/wiki/Terms), so it has no code fix.

The examples set `stanza_max_member_statements` to `5`.

```csharp
// Before - six statements, so a reader follows every step of the job at once
void Process(Order order)
{
	Validate(order);
	Reserve(order);
	Charge(order);
	Ship(order);
	Notify(order);
	Archive(order);
}

// After - rewritten by hand, with each part of the job in a method named for it
void Process(Order order)
{
	Prepare(order);
	Fulfil(order);
}

void Prepare(Order order)
{
	Validate(order);
	Reserve(order);
	Charge(order);
}

void Fulfil(Order order)
{
	Ship(order);
	Notify(order);
	Archive(order);
}
```

- The rule judges a method, a constructor, a destructor, an operator, an accessor and a local function.
  It reports at the name of the member, or at the keyword of an accessor.
- A statement counts wherever it nests, so the body of an `if` or a loop adds to the member.
  A block counts for nothing, and neither do the braces of a lambda.
- A local function is judged on its own and adds one statement to the member around it, for its declaration.
- The statements of a lambda or an anonymous method count toward the member that holds it, because only members are judged.
- A judged member with an expression body holds no statement, unless a lambda inside it does.
- The top-level statements of a file, and a lambda in a field initializer or in the expression body of a property or an indexer,
  belong to no member and are not judged.
- A method that registers, maps or tests runs long by nature, so a `#pragma warning disable ZA1002` above it,
  with a `restore` below, silences the rule where the length is the point.
- The message asks for smaller methods, but the rule has no fix, because each part needs a name.
