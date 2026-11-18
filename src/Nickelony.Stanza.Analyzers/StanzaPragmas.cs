using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers;

/// <summary>Reads the <c>#pragma warning</c> directives of a file, once per tree.</summary>
internal static class StanzaPragmas
{
	public static bool HasPragmaWarningDirectives(SyntaxTree syntaxTree, CancellationToken cancellationToken) =>
		GetPragmaIndex(syntaxTree, cancellationToken).Directives.Length > 0;

	public static bool IsSilencedByPragma(SyntaxTree syntaxTree, string ruleId, int position, CancellationToken cancellationToken) =>
		GetPragmaIndex(syntaxTree, cancellationToken).GetTimeline(ruleId).IsSilencedAt(position);

	private static PragmaIndex GetPragmaIndex(SyntaxTree syntaxTree, CancellationToken cancellationToken) =>
		s_pragmaCache.GetOrCompute(syntaxTree, state: cancellationToken, static (tree, token) => new PragmaIndex(ReadPragmaWarningDirectives(tree, token)));

	private static ImmutableArray<PragmaDirective> ReadPragmaWarningDirectives(SyntaxTree syntaxTree, CancellationToken cancellationToken)
	{
		SyntaxNode root = syntaxTree.GetRoot(cancellationToken);

		ImmutableArray<PragmaDirective> directives;

		if (root.ContainsDirectives)
		{
			ImmutableArray<PragmaDirective>.Builder found = ImmutableArray.CreateBuilder<PragmaDirective>();

			foreach (SyntaxTrivia trivia in root.DescendantTrivia())
			{
				cancellationToken.ThrowIfCancellationRequested();

				// The compiler ignores a directive in a branch the build leaves out, so it silences nothing.
				if (trivia.GetStructure() is PragmaWarningDirectiveTriviaSyntax { IsActive: true } pragma)
					found.Add(ReadPragmaDirective(pragma));
			}

			directives = found.ToImmutable();
		}
		else
		{
			directives = [];
		}

		return directives;
	}

	private static PragmaDirective ReadPragmaDirective(PragmaWarningDirectiveTriviaSyntax directive)
	{
		ImmutableArray<string> codes;

		if (directive.ErrorCodes.Count == 0)
		{
			codes = [];
		}
		else
		{
			ImmutableArray<string>.Builder names = ImmutableArray.CreateBuilder<string>(directive.ErrorCodes.Count);

			foreach (ExpressionSyntax code in directive.ErrorCodes)
				names.Add(ReadCode(code));

			codes = names.ToImmutable();
		}

		return new PragmaDirective(directive.Span, directive.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword), codes);
	}

	/// <summary>Reads a code of a pragma, so a Unicode escape in an identifier reads as the character.</summary>
	public static string ReadCode(ExpressionSyntax code) =>
		code is IdentifierNameSyntax identifier ? identifier.Identifier.ValueText : code.ToString();

	private static bool PragmaNamesRule(in PragmaDirective directive, string ruleId)
	{
		if (directive.Codes.Length == 0)
			return true;

		foreach (string code in directive.Codes)
		{
			if (string.Equals(code, ruleId, StringComparison.Ordinal))
				return true;
		}

		return false;
	}

	/// <summary>Reads a tree's directives once for all pragma checks, instead of a walk for each check.</summary>
	private static readonly WeakCache<SyntaxTree, PragmaIndex> s_pragmaCache = new();

	/// <summary>A tree's directives, and for each rule asked about the transitions that apply to it.</summary>
	private sealed class PragmaIndex(ImmutableArray<PragmaDirective> directives)
	{
		private readonly ConcurrentDictionary<string, RuleTimeline> _timelines = new(StringComparer.Ordinal);

		public ImmutableArray<PragmaDirective> Directives { get; } = directives;

		public RuleTimeline GetTimeline(string ruleId)
		{
			if (_timelines.TryGetValue(ruleId, out RuleTimeline? cached))
				return cached;

			// Two threads can build one timeline; the first stored is the one both read.
			return _timelines.GetOrAdd(ruleId, RuleTimeline.Build(Directives, ruleId));
		}
	}

	/// <summary>The directives that name one rule, or name none, in document order, searched by position.</summary>
	private sealed class RuleTimeline(int[] starts, bool[] disabling)
	{
		public static RuleTimeline Build(ImmutableArray<PragmaDirective> directives, string ruleId)
		{
			List<int> starts = [];
			List<bool> disabling = [];

			foreach (PragmaDirective directive in directives)
			{
				if (!PragmaNamesRule(directive, ruleId))
					continue;

				starts.Add(directive.Span.Start);
				disabling.Add(directive.IsDisabling);
			}

			return new RuleTimeline([.. starts], [.. disabling]);
		}

		public bool IsSilencedAt(int position)
		{
			// The last transition that starts before the position is the one in force there;
			// a directive at or after the position is not.
			int low = 0;
			int high = starts.Length;

			while (low < high)
			{
				int middle = low + ((high - low) / 2);

				if (starts[middle] < position)
					low = middle + 1;
				else
					high = middle;
			}

			return low > 0 && disabling[low - 1];
		}
	}

	private readonly struct PragmaDirective(TextSpan span, bool isDisabling, ImmutableArray<string> codes)
	{
		public ImmutableArray<string> Codes { get; } = codes;
		public bool IsDisabling { get; } = isDisabling;
		public TextSpan Span { get; } = span;
	}
}
