# ZA3060 - keep-assertion-with-binding

**Level: opt-in.**
Removes the blank line between a statement that binds a name and the single-line `assert` that narrows the type of that name.

```python
# Before
def start(session, event_id):
    publish(event_id)
    event = session.find(event_id)

    assert event is not None
    return event.occurred_at

# After
def start(session, event_id):
    publish(event_id)
    event = session.find(event_id)
    assert event is not None
    return event.occurred_at

# Unchanged - the assert checks a value of the bound name, as the assert phase of a test does
def test_start(client):
    publish(1)
    response = client.get("/start")

    assert response.status_code == 200
    stop()
```

- An assert that narrows the type of a name bound right above it is there for the type checker,
  so it reads as part of the binding.
- A binding is an assignment, an annotated assignment with a value or an augmented assignment,
  and its names are the plain names among its targets, those inside a tuple included.
  An assignment to an attribute or an item binds no name.
- The assert narrows a bound name when its test is the name itself, `name is None`, `name is not None`
  or `isinstance(name, ...)`, or an `and` that holds one of them. The message does not count.
- Any other test, such as `response.status_code == 200` or `total == 3`, checks a value,
  so the blank line that sets the assert phase of a test apart stays.
- A run of asserts narrows only when each of its asserts narrows. A run that holds any other assert
  or an assertion helper is the assert phase of a test, and a comment or a directive ends the run.
- A run of narrowing asserts keeps every binding directly above it whose names it narrows,
  whether [`ZA3012`](ZA3012-separate-checked-stanzas.md) is on or off.
- A narrowing assert that opens a chain of guards keeps every binding directly above it that the assert or the chain reads,
  and [`ZA3058`](ZA3058-keep-guard-with-binding.md) keeps none of them.
- The binding may span several lines, but an assert that spans several lines keeps the blank line,
  and [`ZA3001`](ZA3001-separate-multi-line-constructs.md) asks for it.
- A comment between the two, a trailing comment included, keeps the blank line.
- The rule wins the gap over the rules that write a blank line, such as
  [`ZA3002`](ZA3002-separate-declaration-runs.md), which reads a binding beside other bindings as a run.
  That blank line moves above the binding, as the [rule map](../Rule-map.md#separation-a-keeper-displaces) says.
- The rule judges the gap above the assert only. The gap below it belongs to the other rules.

## C# counterpart

[ZA0060 - Keep a narrowing assertion with its binding](https://github.com/Nickelony/Stanza/wiki/ZA0060-Keep-a-narrowing-assertion-with-its-binding).

## JS counterpart

[`keep-assertion-with-binding`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/keep-assertion-with-binding.md).
