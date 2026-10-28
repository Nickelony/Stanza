using System;
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
	int? RunEnd = null
)
{
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

	private const string LeadingPlacement = "leading";
	private const string OwnLine = "own";
	private const string SpacedSlashShape = "spaced";

	private const int LayoutFieldCount = 10;

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

		return properties.ToImmutable();
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
			RunEnd: ReadCount(properties, RunEndKey)
		);
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
		// The weak tail words hold no ';', because Roslyn ends an .editorconfig value at an inline ';' comment.
		return string.Join(
			";",
			layout.ContentWidth.ToString(CultureInfo.InvariantCulture),
			layout.TabColumns.ToString(CultureInfo.InvariantCulture),
			layout.MinTailWords.ToString(CultureInfo.InvariantCulture),
			layout.MinLineWords.ToString(CultureInfo.InvariantCulture),
			layout.MaxOverflowColumns.ToString(CultureInfo.InvariantCulture),
			layout.IndentSize.ToString(CultureInfo.InvariantCulture),
			layout.IndentText ? "1" : "0",
			layout.IndentWithTabs ? "1" : "0",
			((int)layout.Expansion).ToString(CultureInfo.InvariantCulture),
			layout.WeakTailWords?.ToString()
		);
	}

	private static DocLayoutOptions? ReadLayout(string? text)
	{
		string[] parts = (text ?? string.Empty).Split(';');

		if (
			parts.Length != LayoutFieldCount
			|| ReadCount(parts[0], MarkupLimits.MaxCount) is not { } contentWidth
			|| ReadCount(parts[1], MarkupLimits.MaxIndentColumns) is not { } tabColumns
			|| ReadCount(parts[2], MarkupLimits.MaxCount) is not { } minTailWords
			|| ReadCount(parts[3], MarkupLimits.MaxCount) is not { } minLineWords
			|| ReadCount(parts[4], MarkupLimits.MaxCount) is not { } maxOverflowColumns
			|| ReadCount(parts[5], MarkupLimits.MaxIndentColumns) is not { } indentSize
			|| ReadCount(parts[6], 1) is not { } indentText
			|| ReadCount(parts[7], 1) is not { } indentWithTabs
			|| ReadCount(parts[8], (int)DocExpansion.All) is not { } expansion
		)
		{
			return null;
		}

		return new DocLayoutOptions(
			ContentWidth: contentWidth,
			TabColumns: tabColumns,
			MinTailWords: minTailWords,
			MinLineWords: minLineWords,
			MaxOverflowColumns: maxOverflowColumns,
			IndentSize: indentSize,
			IndentText: indentText == 1,
			Expansion: (DocExpansion)expansion,
			WeakTailWords: DocNameSet.Parse(parts[9]),
			IndentWithTabs: indentWithTabs == 1
		);
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
}
