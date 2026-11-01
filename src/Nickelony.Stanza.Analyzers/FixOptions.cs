using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Nickelony.Stanza.Markup.Core;
using Nickelony.Stanza.Markup.Core.DocComments;
using Nickelony.Stanza.Markup.Core.Options;

namespace Nickelony.Stanza.Analyzers;

/// <summary>The options a diagnostic carries to its fix, which reads no analyzer options.</summary>
/// <remarks>
/// The constructor takes one parameter for each option a fix reads, since the one job is to carry them across.
/// </remarks>
internal sealed record FixOptions(
	int TabColumns = StanzaText.DefaultTabColumns,
	int IndentStepColumns = StanzaText.DefaultIndentStepColumns,
	ArrowPlacement Placement = ArrowPlacement.Trailing,
	bool OpensOwnLine = false,
	ImmutableArray<(int Index, string Name)> ArgumentNames = default,
	DocLayoutOptions? DocLayout = null,
	bool SpacedSlash = false,
	int? KeptBlankLines = null,
	TextChange? Change = null,
	int? RunStart = null,
	int? RunEnd = null,
	MarkupNameSet? Keywords = null,
	int? MergedIfs = null,
	bool SeparatesNeighbors = false,
	bool JoinsNeighbors = false,
	bool WritesGapAbove = false,
	int SeparatedUnits = 0,
	ImmutableArray<int> Boundaries = default
)
{
	internal const string WritesGapAboveKey = "writesGapAbove";

	private const string TabColumnsKey = "tabColumns";
	private const string IndentStepColumnsKey = "indentStepColumns";
	private const string PlacementKey = "arrowPlacement";
	private const string ExpressionLineKey = "expressionLine";
	private const string ArgumentNamesKey = "argumentNames";
	private const string DocLayoutKey = "docLayout";
	private const string SlashKey = "selfClosingSlash";
	private const string KeptBlankLinesKey = "keptBlankLines";
	private const string ChangeStartKey = "changeStart";
	private const string ChangeLengthKey = "changeLength";
	private const string ChangeTextKey = "changeText";
	private const string RunStartKey = "runStart";
	private const string RunEndKey = "runEnd";
	private const string KeywordsKey = "keywords";
	private const string MergedIfsKey = "mergedIfs";
	private const string SeparatesNeighborsKey = "separatesNeighbors";
	private const string JoinsNeighborsKey = "joinsNeighbors";
	private const string SeparatedUnitsKey = "separatedUnits";
	private const string BoundariesKey = "boundaries";

	private const string LeadingPlacement = "leading";
	private const string OwnLine = "own";
	private const string SpacedSlashShape = "spaced";

	/// <summary>The counts of a layout in the order the value writes them, each with the largest it reads.</summary>
	/// <remarks>
	/// The weak line end words follow them as a text. A new count takes one row here, which is its whole change.
	/// </remarks>
	private static readonly LayoutCount[] s_layoutCounts =
	[
		new(
			Max: MarkupLimits.MaxCount,
			Get: static layout => layout.ContentWidth,
			With: static (layout, value) => layout with { ContentWidth = value }
		),

		new(
			Max: MarkupLimits.MaxIndentColumns,
			Get: static layout => layout.TabColumns,
			With: static (layout, value) => layout with { TabColumns = value }
		),

		new(
			Max: MarkupLimits.MaxCount,
			Get: static layout => layout.MinTailWords,
			With: static (layout, value) => layout with { MinTailWords = value }
		),

		new(
			Max: MarkupLimits.MaxCount,
			Get: static layout => layout.MinLineWords,
			With: static (layout, value) => layout with { MinLineWords = value }
		),

		new(
			Max: MarkupLimits.MaxCount,
			Get: static layout => layout.MaxOverflowColumns,
			With: static (layout, value) => layout with { MaxOverflowColumns = value }
		),

		new(
			Max: MarkupLimits.MaxIndentColumns,
			Get: static layout => layout.IndentSize,
			With: static (layout, value) => layout with { IndentSize = value }
		),

		new(Max: 1, Get: static layout => layout.IndentText ? 1 : 0, With: static (layout, value) => layout with { IndentText = value == 1 }),
		new(Max: 1, Get: static layout => layout.IndentWithTabs ? 1 : 0, With: static (layout, value) => layout with { IndentWithTabs = value == 1 }),

		new(
			Max: (int)DocBlockElements.All,
			Get: static layout => (int)layout.BlockElements,
			With: static (layout, value) => layout with { BlockElements = (DocBlockElements)value }
		),
	];

	/// <summary>Writes each option that differs from its default; a missing property parses as the default.</summary>
	public ImmutableDictionary<string, string?> ToProperties()
	{
		ImmutableDictionary<string, string?>.Builder properties = ImmutableDictionary.CreateBuilder<string, string?>();

		if (TabColumns != StanzaText.DefaultTabColumns)
			properties[TabColumnsKey] = TabColumns.ToString(CultureInfo.InvariantCulture);

		if (IndentStepColumns != StanzaText.DefaultIndentStepColumns)
			properties[IndentStepColumnsKey] = IndentStepColumns.ToString(CultureInfo.InvariantCulture);

		if (Placement == ArrowPlacement.Leading)
			properties[PlacementKey] = LeadingPlacement;

		if (OpensOwnLine)
			properties[ExpressionLineKey] = OwnLine;

		// Each argument writes its index and the name it takes, as in "0=width;2=smooth".
		if (!ArgumentNames.IsDefaultOrEmpty)
		{
			properties[ArgumentNamesKey] = string.Join(
				";",
				ArgumentNames.Select(static argument => argument.Index.ToString(CultureInfo.InvariantCulture) + "=" + argument.Name)
			);
		}

		if (DocLayout is { } layout)
			properties[DocLayoutKey] = WriteLayout(layout);

		if (SpacedSlash)
			properties[SlashKey] = SpacedSlashShape;

		if (KeptBlankLines is { } kept)
			properties[KeptBlankLinesKey] = kept.ToString(CultureInfo.InvariantCulture);

		if (Change is { } change)
		{
			properties[ChangeStartKey] = change.Span.Start.ToString(CultureInfo.InvariantCulture);
			properties[ChangeLengthKey] = change.Span.Length.ToString(CultureInfo.InvariantCulture);
			properties[ChangeTextKey] = change.NewText;
		}

		if (RunStart is { } runStart)
			properties[RunStartKey] = runStart.ToString(CultureInfo.InvariantCulture);

		if (RunEnd is { } runEnd)
			properties[RunEndKey] = runEnd.ToString(CultureInfo.InvariantCulture);

		if (Keywords is { } keywords)
			properties[KeywordsKey] = keywords.ToString();

		if (MergedIfs is { } mergedIfs)
			properties[MergedIfsKey] = mergedIfs.ToString(CultureInfo.InvariantCulture);

		if (SeparatesNeighbors)
			properties[SeparatesNeighborsKey] = "1";

		if (JoinsNeighbors)
			properties[JoinsNeighborsKey] = "1";

		if (SeparatedUnits != 0)
			properties[SeparatedUnitsKey] = SeparatedUnits.ToString(CultureInfo.InvariantCulture);

		if (WritesGapAbove)
			properties[WritesGapAboveKey] = "1";

		// Each boundary writes its index, as in "3,7".
		if (!Boundaries.IsDefaultOrEmpty)
			properties[BoundariesKey] = string.Join(",", Boundaries.Select(static boundary => boundary.ToString(CultureInfo.InvariantCulture)));

		return properties.ToImmutable();
	}

	/// <summary>Whether no earlier diagnostic claimed the same run, which this one then claims.</summary>
	/// <remarks>
	/// Every gap of a run carries the fix of the whole run, so the first one claims it and the rest are skipped.
	/// </remarks>
	public static bool TryClaimRun(SyntaxNode root, Diagnostic diagnostic, HashSet<(string Id, int? Start, int? End, int Container)> claimedRuns)
	{
		FixOptions fixOptions = Parse(diagnostic);

		// A diagnostic with no run bounds is not part of a run.
		if (fixOptions.RunStart is null || diagnostic.AdditionalLocations.Count == 0)
			return true;

		// The bounds are indexes into a container, so the container, found from the previous sibling, joins the key.
		int container = root.FindNode(diagnostic.AdditionalLocations[0].SourceSpan).Parent?.SpanStart ?? -1;

		return claimedRuns.Add((diagnostic.Id, fixOptions.RunStart, fixOptions.RunEnd, container));
	}

	/// <summary>Whether the diagnostic carries a kept count at all, a non-numeric one included.</summary>
	public static bool CarriesKeptBlankLines(Diagnostic diagnostic) => diagnostic.Properties.ContainsKey(KeptBlankLinesKey);

	public static FixOptions Parse(Diagnostic diagnostic)
	{
		ImmutableDictionary<string, string?> properties = diagnostic.Properties;

		bool leading = properties.TryGetValue(PlacementKey, out string? placement) && placement == LeadingPlacement;
		bool ownLine = properties.TryGetValue(ExpressionLineKey, out string? line) && line == OwnLine;
		bool spacedSlash = properties.TryGetValue(SlashKey, out string? slash) && slash == SpacedSlashShape;

		return new FixOptions(
			TabColumns: ReadCount(properties, TabColumnsKey) ?? StanzaText.DefaultTabColumns,
			IndentStepColumns: ReadCount(properties, IndentStepColumnsKey) ?? StanzaText.DefaultIndentStepColumns,
			Placement: leading ? ArrowPlacement.Leading : ArrowPlacement.Trailing,
			OpensOwnLine: ownLine,
			ArgumentNames: properties.TryGetValue(ArgumentNamesKey, out string? names) ? ReadArgumentNames(names) : default,
			DocLayout: properties.TryGetValue(DocLayoutKey, out string? layout) ? ReadLayout(layout) : null,
			SpacedSlash: spacedSlash,
			KeptBlankLines: ReadCount(properties, KeptBlankLinesKey),
			Change: ReadChange(properties),
			RunStart: ReadCount(properties, RunStartKey),
			RunEnd: ReadCount(properties, RunEndKey),
			Keywords: properties.TryGetValue(KeywordsKey, out string? keywords) ? MarkupNameSet.Parse(keywords ?? string.Empty) : null,
			MergedIfs: ReadCount(properties, MergedIfsKey),
			SeparatesNeighbors: properties.ContainsKey(SeparatesNeighborsKey),
			JoinsNeighbors: properties.ContainsKey(JoinsNeighborsKey),
			WritesGapAbove: properties.ContainsKey(WritesGapAboveKey),
			SeparatedUnits: ReadCount(properties, SeparatedUnitsKey) ?? 0,
			Boundaries: properties.TryGetValue(BoundariesKey, out string? boundaries) ? ReadBoundaries(boundaries) : default
		);
	}

	/// <summary>Drops the whole list at an entry that is not an index, so a fix writes none of it.</summary>
	private static ImmutableArray<int> ReadBoundaries(string? boundaries)
	{
		ImmutableArray<int>.Builder parsed = ImmutableArray.CreateBuilder<int>();

		foreach (string entry in (boundaries ?? string.Empty).Split([','], StringSplitOptions.RemoveEmptyEntries))
		{
			if (ReadCount(entry, int.MaxValue) is not { } boundary)
				return default;

			parsed.Add(boundary);
		}

		return parsed.ToImmutable();
	}

	/// <summary>
	/// Drops the whole list at a pair that is not an index and a name, so a fix writes nothing rather than a part.
	/// </summary>
	private static ImmutableArray<(int Index, string Name)> ReadArgumentNames(string? names)
	{
		ImmutableArray<(int Index, string Name)>.Builder arguments = ImmutableArray.CreateBuilder<(int Index, string Name)>();

		foreach (string pair in (names ?? string.Empty).Split([';'], StringSplitOptions.RemoveEmptyEntries))
		{
			int separator = pair.IndexOf('=');

			if (separator <= 0 || separator == pair.Length - 1 || ReadCount(pair.Substring(0, separator), int.MaxValue) is not { } index)
				return default;

			arguments.Add((index, pair.Substring(separator + 1)));
		}

		return arguments.ToImmutable();
	}

	/// <summary>
	/// Writes the layout as one semicolon-separated value, so a fix uses the settings the rule judged the comment by.
	/// </summary>
	private static string WriteLayout(DocLayoutOptions layout)
	{
		// The weak line end words hold no ';', because Roslyn ends an .editorconfig value at an inline ';' comment.
		IEnumerable<string> counts = s_layoutCounts.Select(count => count.Get(layout).ToString(CultureInfo.InvariantCulture));
		return string.Join(";", counts.Append(layout.WeakLineEndWords?.ToString() ?? string.Empty));
	}

	private static DocLayoutOptions? ReadLayout(string? text)
	{
		string[] parts = (text ?? string.Empty).Split(';');

		if (parts.Length != s_layoutCounts.Length + 1)
			return null;

		DocLayoutOptions layout = default;

		for (int index = 0; index < s_layoutCounts.Length; index++)
		{
			if (ReadCount(parts[index], s_layoutCounts[index].Max) is not { } value)
				return null;

			layout = s_layoutCounts[index].With(layout, value);
		}

		return layout with
		{
			WeakLineEndWords = MarkupNameSet.Parse(parts[s_layoutCounts.Length]),
		};
	}

	private static TextChange? ReadChange(ImmutableDictionary<string, string?> properties)
	{
		if (
			ReadCount(properties, ChangeStartKey) is not { } start
			|| ReadCount(properties, ChangeLengthKey) is not { } length
			|| !properties.TryGetValue(ChangeTextKey, out string? newText)
		)
		{
			return null;
		}

		return new TextChange(new TextSpan(start, length), newText ?? string.Empty);
	}

	private static int? ReadCount(ImmutableDictionary<string, string?> properties, string key) =>
		properties.TryGetValue(key, out string? value) ? ReadCount(value, int.MaxValue) : null;

	private static int? ReadCount(string? text, int max) => MarkupOptionReader.TryReadCount(text, max, out int count) ? count : null;

	/// <summary>One count of a layout, with the largest value it reads and how a layout gives and takes it.</summary>
	private readonly record struct LayoutCount(int Max, Func<DocLayoutOptions, int> Get, Func<DocLayoutOptions, int, DocLayoutOptions> With);
}
