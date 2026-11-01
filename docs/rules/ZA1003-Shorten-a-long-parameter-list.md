# ZA1003 - Shorten a long parameter list

**Suggestion, off by default.**
Reports a method, a local function or a delegate that takes more parameters than
[`stanza_max_parameters`](https://github.com/Nickelony/Stanza/wiki/stanza_max_parameters) allows.
It is a [report-only rule](https://github.com/Nickelony/Stanza/wiki/Terms), so it has no code fix.

```csharp
// Before - eight parameters, so a call site passes them in a fixed order
void Send(
	string to,
	string cc,
	string bcc,
	string subject,
	string body,
	bool html,
	bool urgent,
	DateTime sendAt
)
{
}

// After - rewritten by hand, with the parameters grouped in a type
record Message(string To, string Cc, string Bcc, string Subject, string Body, bool Html, bool Urgent, DateTime SendAt);

void Send(Message message)
{
}
```

- The rule reports once for a list, at the whole parameter list.
- A constructor is left out, because dependency injection makes it long.
  That covers a primary constructor and a record's parameters too.
- A signature that something else fixes is left out: an `override`, an explicit interface implementation and an `extern` method.
  The declaration they follow owns the parameters, and that declaration is reported.
- The rule reads syntax only, so a method that implements an interface without naming it is reported beside the interface member.
- A lambda and an anonymous method are left out, because their parameters come from the delegate they are given to.
- The `this` parameter of an extension method counts, and so does each optional, `out`, `ref` and `params` parameter.
- The message asks for the parameters to be grouped into a type or the method to be split,
  but the rule has no fix, because each choice reshapes every call.
