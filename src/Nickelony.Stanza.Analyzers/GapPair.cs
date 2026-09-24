using System;
using Microsoft.CodeAnalysis;

namespace Nickelony.Stanza.Analyzers;

/// <summary>Two rules that judge one gap, read as one value; the file's settings pick the half that reports.</summary>
internal sealed record GapPair(
	DiagnosticDescriptor First,
	DiagnosticDescriptor Second,
	DiagnosticDescriptor DefaultHalf,
	DiagnosticDescriptor OwnerWhenBothEnabled
)
{
	public DiagnosticDescriptor GetOtherHalf(string ruleId)
	{
		if (string.Equals(ruleId, First.Id, StringComparison.Ordinal))
			return Second;

		return string.Equals(ruleId, Second.Id, StringComparison.Ordinal)
			? First
			: throw new ArgumentException($"{ruleId} is not a half of this pair.", nameof(ruleId));
	}
}
