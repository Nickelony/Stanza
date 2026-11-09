# ZA0012 - Separate a commented sibling

**Suggestion, off by default.**
Adds a blank line above the comment that leads a single-line unit, so the comment stays with the unit it describes.

```csharp
// Before
Prepare();
// Run the step.
Run();

// After
Prepare();

// Run the step.
Run();
```

A commented-out line counts as a comment, because the rule does not guess whether a comment holds code:

```csharp
// Before
var options = new Options
{
	Name = "a",
	// Count = 2,
	Size = 3,
};

// After
var options = new Options
{
	Name = "a",

	// Count = 2,
	Size = 3,
};
```

The example holds with [`ZA0133`](https://github.com/Nickelony/Stanza/wiki/ZA0133-Keep-single-line-initializer-members-together) off.
That rule ships on, and while it is on it overrides this rule in a list like that one:
this rule writes nothing there, and `ZA0133` removes a blank line above `// Count = 2,` if one stands.

The comment starts a new group, so the grouping rules end the run of declarations there, as [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap) say, and `int b` stays with `Build(1)`:

```csharp
// Before
int a = 1;
// Build once.
int b = 2;
Build(1);

// After
int a = 1;

// Build once.
int b = 2;
Build(1);
```

- The rule judges the statements of a block or switch section, the members of a type, enum, namespace or file, and the import directives.
- It also judges the elements of a delimited list:
  arguments, parameters, tuple elements, collection elements, initializer members, switch sections and arms, accessors and declarators.
- A comment of any kind counts: `//`, `/* */`, `///` and `/** */`.
  It has to stand on the line directly above the unit, with no blank line between them.
- The blank line goes above the first line of a run of comment lines, never between the comment and the unit and never below the unit.
- The rule judges only a [gap](https://github.com/Nickelony/Stanza/wiki/Terms) where both units span one line,
  so a gap beside a multi-line unit stays with [`ZA0001`](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-multi-line-constructs) and the multi-line pairs.
- The rule stays quiet for the first unit of a container, for a gap that already holds a blank line or a directive,
  and for a comment that trails the previous unit's line.
  A comment on its own line below that trailing comment still fires.
- The grouping and import rules leave a gap that this rule reports to it, so the blank line is reported once;
  [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#gaps-and-owners) list them.
  Where the rule is off, they judge the gap by their own terms.
- Where the single-line tight rule of a delimited list is on, it wins the gaps of that list.
  It removes the blank line above a comment, and this rule writes nothing there.
- Where [`ZA0051`](https://github.com/Nickelony/Stanza/wiki/ZA0051-Keep-the-two-units-of-a-container-together) is on,
  it wins the gap of a container of exactly two units.
  It leaves a gap that holds a comment, and this rule writes nothing there.
- A pragma that silences a tight rule over a gap hands the gap back to this rule,
  and a pragma over this rule leaves the tight rule working.
- A comment or directive in the gap follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
