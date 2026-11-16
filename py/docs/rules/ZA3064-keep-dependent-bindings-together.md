# ZA3064 - keep-dependent-bindings-together

**Level: opt-in.**
Removes the blank lines between consecutive single-line bindings that each read the name bound above.

```python
# Before
async def read():
    file = await handle.get_file()

    data = await file.array_buffer()

    stream = MemoryStream(data)

# After
async def read():
    file = await handle.get_file()
    data = await file.array_buffer()
    stream = MemoryStream(data)
```

- Each statement initializes one plain name through an assignment or an annotated assignment with a value.
- The value reads the name bound right above it. An attribute name, an annotation or the text of a string reads none.
- A statement that binds the same name again, as `text = text.strip()` below `text = read(path)`, joins the chain.
- Unpacking, a chained assignment, a member write, an augmented assignment and an annotation without a value
  stay outside the chain.
- Lambda and comprehension scopes read nothing for the chain, and an assignment expression in the value ends it.
- A multi-line statement, a comment, a directive or two statements on one line end the chain.
  A class body is outside the rule.
- A blank line between two tight sections stays as written.
  One finding removes every other blank line of the chain, so one fix and a fix of every finding agree.
- A blank line another rule asks for inside the chain moves above it, as the [rule map](../Rule-map.md#separation-a-keeper-displaces) says.
  The rule asks for no blank line around the chain.

## C# counterpart

[ZA0064 - Keep dependent bindings together](https://github.com/Nickelony/Stanza/wiki/ZA0064-Keep-dependent-bindings-together).

## JS counterpart

[keep-dependent-bindings-together](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/keep-dependent-bindings-together.md).
