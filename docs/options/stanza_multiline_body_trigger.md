# stanza_multiline_body_trigger

Controls which parts of a member make it multi-line for the body-shape rules,
and whether the arrow counts toward width.
Default: `either`.
Read by the body-shape rules, such as [`ZA0040`](https://github.com/Nickelony/Stanza/wiki/ZA0040-Use-a-block-body-for-a-multi-line-member).

| Value         | Effect                                                                                                | Counts the arrow |
| ------------- | ----------------------------------------------------------------------------------------------------- | ---------------- |
| `body`        | a member is multi-line when its expression body spans more than one line, whatever the signature does | no               |
| `signature`   | a member is multi-line when its signature does, whatever the expression body does                     | yes              |
| `either`      | a member is multi-line when its signature or its expression body does                                 | yes              |
| `both`        | a member is multi-line when its signature and its expression body each span more than one line        | no               |
| anything else | as `either`                                                                                           | yes              |

Where the arrow counts, its space and `=>` must fit on the line the arrow lands on,
for the body-shape rules that measure a width, such as [`ZA0041`](https://github.com/Nickelony/Stanza/wiki/ZA0041-Use-an-expression-body-for-a-single-line-member) and [`ZA0044`](https://github.com/Nickelony/Stanza/wiki/ZA0044-Break-the-expression-off-the-signature-line).

The body-shape rules that give a member a block body ask for one of each member this key marks as multi-line.

The code below holds three expression-bodied methods that wrap in different places.
Under `either`, ZA0040 asks for a block body on every one of them.
`body` leaves `WrappedSignature` as an expression body, `signature` leaves `WrappedBody`,
and `both` leaves `WrappedSignature` and `WrappedBody` as expression bodies and asks for a block body only on `Both`.

```csharp
internal sealed class C
{
	internal int WrappedSignature(
		int first,
		int second
	) => first + second;

	internal int WrappedBody(int first) =>
		first
		+ first;

	internal int Both(
		int first,
		int second
	) =>
		first
		+ second;
}
```
