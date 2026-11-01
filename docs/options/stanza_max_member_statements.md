# stanza_max_member_statements

Controls how many statements a member may hold before [`ZA1002`](https://github.com/Nickelony/Stanza/wiki/ZA1002-Shorten-a-long-member) reports it.
Default: `40`.
Read by [`ZA1002`](https://github.com/Nickelony/Stanza/wiki/ZA1002-Shorten-a-long-member), whose page shows a member above a limit of `5`.

| Value                                           | Effect                                                       |
| ----------------------------------------------- | ------------------------------------------------------------ |
| `0`                                             | the rule reports no member                                   |
| a number from 1 to 1000                         | a member is reported when it holds more statements than that |
| below zero, above 1000, absent, or not a number | the default                                                  |

At the default, forty statements pass and a forty-first is reported.
At a limit of `3`, three statements pass and a fourth is reported:

```csharp
// Unchanged at a limit of 3 - three statements
void Run(Order order)
{
	Validate(order);
	Charge(order);
	Ship(order);
}

// Before at a limit of 3 - four statements
void Run(Order order)
{
	Validate(order);
	Charge(order);
	Ship(order);
	Notify(order);
}

// After at a limit of 3
void Run(Order order)
{
	Validate(order);
	Charge(order);
	Fulfil(order);
}

void Fulfil(Order order)
{
	Ship(order);
	Notify(order);
}
```

The count covers one member, and a block is not a statement.
