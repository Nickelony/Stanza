using System;
using Nickelony.Stanza.Markup.Core.DocComments;

namespace Nickelony.Stanza.Markup.Core.Options;

/// <summary>The names the closed keys take, each held once next to <see cref="OptionKeys"/>.</summary>
/// <remarks>
/// The readers, the tool's checks and their warnings all read these, so a new name is one edit.
/// </remarks>
internal static class OptionValues
{
	/// <summary>The switch names of <see cref="OptionKeys.MarkupEnabled"/>, <c>root</c> and <c>csharp_*</c>.</summary>
	public static readonly OptionChoice<bool> Boolean = new(("true", true), ("false", false));

	/// <summary>Whether <see cref="OptionKeys.MarkupScope"/> limits the rules to containers.</summary>
	public static readonly OptionChoice<bool> MarkupScope = new(("containers", true), ("all", false));

	/// <summary>Whether <see cref="OptionKeys.MarkupDocReprint"/> reprints the members.</summary>
	public static readonly OptionChoice<bool> MarkupDocReprint = new(("members", true), ("off", false));

	/// <summary>Which documentation elements <see cref="OptionKeys.DocBlockElements"/> gives a block form.</summary>
	public static readonly OptionChoice<DocBlockElements> DocBlockElements = new(
		("minimal", DocComments.DocBlockElements.Minimal),
		("sections", DocComments.DocBlockElements.Sections),
		("all", DocComments.DocBlockElements.All)
	);

	/// <summary>Whether <see cref="OptionKeys.DocTextIndent"/> indents nested text.</summary>
	public static readonly OptionChoice<bool> DocTextIndent = new(("nested", true), ("aligned", false));

	/// <summary>The name the two indent keys take beside a count, which indents with a tab per level.</summary>
	public const string IndentTab = "tab";

	/// <summary>Which parts of a member make it multi-line for <see cref="OptionKeys.MultiLineMemberPart"/>.</summary>
	public static readonly OptionChoice<MultiLineMemberPart> MultiLineMemberPart = new(
		("body", Options.MultiLineMemberPart.Body),
		("signature", Options.MultiLineMemberPart.Signature),
		("both", Options.MultiLineMemberPart.Both),
		("either", Options.MultiLineMemberPart.Either)
	);

	/// <summary>Which raw string literals <see cref="OptionKeys.MarkupLiterals"/> has the markup rules read.</summary>
	public static readonly OptionChoice<MarkupLiteralReading> MarkupLiterals = new(
		("marked", MarkupLiteralReading.Marked),
		("all", MarkupLiteralReading.All),
		("off", MarkupLiteralReading.Off)
	);

	/// <summary>Whether <see cref="OptionKeys.NamedArgumentMatch"/> lets a matching argument stay unnamed.</summary>
	public static readonly OptionChoice<bool> NamedArgumentMatch = new(("allow", true), ("require", false));

	/// <summary>Whether <see cref="OptionKeys.NamedArgumentLiterals"/> names more than the booleans.</summary>
	public static readonly OptionChoice<bool> NamedArgumentLiterals = new(("keywords", true), ("bool", false));

	/// <summary>How <see cref="OptionKeys.NamedArgumentCase"/> compares an identifier with a name.</summary>
	public static readonly OptionChoice<StringComparison> NamedArgumentCase = new(
		("sensitive", StringComparison.Ordinal),
		("insensitive", StringComparison.OrdinalIgnoreCase)
	);

	/// <summary>Whether <see cref="OptionKeys.AssignmentTargets"/> splits a run by target kind.</summary>
	public static readonly OptionChoice<bool> AssignmentTargets = new(("mixed", false), ("split", true));
}
