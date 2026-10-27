# Disabling and silencing a rule

A rule is disabled with one line in an `.editorconfig` for a set of files, or silenced with a `#pragma` pair for one region of code.

```ini
# .editorconfig
[tests/**.cs]
dotnet_diagnostic.ZA0036.severity = none
```

```csharp
#pragma warning disable ZA0036
public void Process()
{

	DoWork();

}
#pragma warning restore ZA0036
```

- A `<NoWarn>` entry for the rule in the project file disables it for the whole project, as [Terms](https://github.com/Nickelony/Stanza/wiki/Terms) says.
- A `silent` severity leaves the rule [enabled](https://github.com/Nickelony/Stanza/wiki/Terms), so it still owns its [gap](https://github.com/Nickelony/Stanza/wiki/Terms) in a pair.
- A pragma over the owning half of a pair hands its gap to the other half only when the file enables a half of the pair,
  as [Which rule owns which gap](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap) describes.
- Disabling or silencing `ZA0005` lets the [grouping rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#gaps-and-owners) report a gap beside a multi-line unit,
  because they defer that gap to it.
  A `[SuppressMessage]` attribute does not, as [Limitations](https://github.com/Nickelony/Stanza/wiki/Limitations) explains.
- Another analyzer can ask for the opposite shape of a gap a Stanza rule owns.
  Disable either the other analyzer's rule or the Stanza rule for the gap, because otherwise each fix undoes the other.
