# stanza_max_parameters

Controls how many parameters a method, a local function or a delegate may take before [`ZA1003`](https://github.com/Nickelony/Stanza/wiki/ZA1003-Shorten-a-long-parameter-list) reports it.
Default: `7`.
Read by [`ZA1003`](https://github.com/Nickelony/Stanza/wiki/ZA1003-Shorten-a-long-parameter-list), whose page shows a list above the default limit.

| Value                                           | Effect                                                     |
| ----------------------------------------------- | ---------------------------------------------------------- |
| `0`                                             | the rule reports no list                                   |
| a number from 1 to 1000                         | a list is reported when it holds more parameters than that |
| below zero, above 1000, absent, or not a number | the default                                                |

At the default, seven parameters pass and an eighth is reported:

```csharp
// Unchanged - seven parameters
void Draw(int x, int y, int width, int height, int color, int stroke, bool filled)
{
}

// Before - eight parameters
void Draw(int x, int y, int width, int height, int color, int stroke, bool filled, bool dashed)
{
}

// After
void Draw(Rectangle bounds, int color, int stroke, bool filled, bool dashed)
{
}
```

At a limit of `2`, three parameters are reported:

```csharp
// Before at a limit of 2 - three parameters
void Move(int x, int y, int speed)
{
}

// After at a limit of 2
void Move(Point target, int speed)
{
}
```

The count covers one parameter list. A constructor, an override, an explicit interface implementation and an `extern` method are not counted.
