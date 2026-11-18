using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers;

/// <summary>A kind of directive block that a padding rule judges: its rule, option, markers and padded sides.</summary>
/// <remarks>
/// The rules that read runs and the rules that write the blank lines read these sides, so both agree on each gap.
/// </remarks>
internal sealed record DirectiveBlock(
	DiagnosticDescriptor Rule,
	Func<StanzaOptions, bool> ReadPadding,
	Func<SyntaxTree, CancellationToken, ImmutableArray<SyntaxTrivia>> ReadMarkers,
	Func<DirectiveTriviaSyntax, bool, MarkerSides> ReadSides,
	Func<DirectiveTriviaSyntax, MarkerSides> ReadInsideSides
)
{
	/// <summary>The region markers that ZA0401 pads.</summary>
	public static DirectiveBlock Region { get; } =
		new(
			Rule: DiagnosticDescriptors.SeparateRegionMarker,
			ReadPadding: static options => options.PadsRegions,
			ReadMarkers: StanzaCommentCache.GetRegionMarkers,
			ReadSides: ReadRegionSides,
			ReadInsideSides: ReadInsideRegionSides
		);

	/// <summary>The conditional directives between units that ZA0402 pads.</summary>
	public static DirectiveBlock ConditionalBlock { get; } =
		new(
			Rule: DiagnosticDescriptors.SeparateConditionalBlock,
			ReadPadding: static options => options.PadsConditionalBlocks,
			ReadMarkers: StanzaCommentCache.GetConditionalDirectiveMarkers,
			ReadSides: ReadConditionalBlockSides,
			ReadInsideSides: ReadInsideConditionalBlockSides
		);

	/// <summary>The suppression pragmas that ZA0403 pads.</summary>
	public static DirectiveBlock SuppressionBlock { get; } =
		new(
			Rule: DiagnosticDescriptors.SeparateSuppressionBlock,
			ReadPadding: static options => options.PadsSuppressionBlocks,
			ReadMarkers: StanzaCommentCache.GetPragmaMarkers,
			ReadSides: ReadSuppressionBlockSides,
			ReadInsideSides: ReadInsideSuppressionBlockSides
		);

	public static ImmutableArray<DirectiveBlock> All { get; } = [Region, ConditionalBlock, SuppressionBlock];

	private static MarkerSides ReadRegionSides(DirectiveTriviaSyntax directive, bool padded)
	{
		if (directive is not (RegionDirectiveTriviaSyntax or EndRegionDirectiveTriviaSyntax))
			return MarkerSides.None;

		if (padded)
			return MarkerSides.Both;

		return directive is RegionDirectiveTriviaSyntax ? MarkerSides.Above : MarkerSides.Below;
	}

	/// <summary>The sides inside a region, which a marker with no partner or in inactive code has none of.</summary>
	private static MarkerSides ReadInsideRegionSides(DirectiveTriviaSyntax directive)
	{
		if (!IsPairedActiveRegion(directive))
			return MarkerSides.None;

		return directive is RegionDirectiveTriviaSyntax ? MarkerSides.Below : MarkerSides.Above;
	}

	private static bool IsPairedActiveRegion(DirectiveTriviaSyntax directive)
	{
		if (!directive.IsActive)
			return false;

		List<DirectiveTriviaSyntax> related = directive.GetRelatedDirectives();

		return related.Count == 2 && related[0] is RegionDirectiveTriviaSyntax { IsActive: true } && related[1] is EndRegionDirectiveTriviaSyntax { IsActive: true };
	}

	private static MarkerSides ReadSuppressionBlockSides(DirectiveTriviaSyntax directive, bool padded)
	{
		if (SuppressionBlocks.IsOpener(directive))
			return padded ? MarkerSides.Both : MarkerSides.Above;

		if (!SuppressionBlocks.IsCloser(directive))
			return MarkerSides.None;

		return padded ? MarkerSides.Both : MarkerSides.Below;
	}

	private static MarkerSides ReadInsideSuppressionBlockSides(DirectiveTriviaSyntax directive)
	{
		if (SuppressionBlocks.IsOpener(directive))
			return MarkerSides.Below;

		return SuppressionBlocks.IsCloser(directive) ? MarkerSides.Above : MarkerSides.None;
	}

	private static MarkerSides ReadConditionalBlockSides(DirectiveTriviaSyntax directive, bool padded)
	{
		bool isOuter = directive is IfDirectiveTriviaSyntax or EndIfDirectiveTriviaSyntax;

		if ((!padded && !isOuter) || !ConditionalBlocks.IsBetweenUnits(directive))
			return MarkerSides.None;

		if (padded)
			return MarkerSides.Both;

		return directive is IfDirectiveTriviaSyntax ? MarkerSides.Above : MarkerSides.Below;
	}

	/// <summary>The sides inside a conditional block: below an opening branch and above a closing one.</summary>
	private static MarkerSides ReadInsideConditionalBlockSides(DirectiveTriviaSyntax directive)
	{
		if (!ConditionalBlocks.IsBetweenUnits(directive))
			return MarkerSides.None;

		return directive switch
		{
			IfDirectiveTriviaSyntax => MarkerSides.Below,
			EndIfDirectiveTriviaSyntax => MarkerSides.Above,
			_ => MarkerSides.Both,
		};
	}
}
