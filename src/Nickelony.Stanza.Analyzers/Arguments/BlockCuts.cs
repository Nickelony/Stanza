using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers.Arguments;

/// <summary>Reads once which statements of a block a speculation keeps.</summary>
/// <remarks>
/// Every call in the block reads these facts, so a large block costs its size and not its square.
/// </remarks>
internal sealed class BlockCuts
{
	/// <summary>A statement a speculation keeps, and whether it walks into it to cut the branches of a guard.</summary>
	internal readonly record struct Kept(int Index, bool WalksIn);

	private enum Role
	{
		Cut,
		Guard,
		Keep,
	}

	private readonly BlockSyntax _block;

	/// <summary>The statements kept when no exit needs to stay.</summary>
	private readonly ImmutableArray<Kept> _plain;

	/// <summary>The statements kept when one that returns or throws also stays.</summary>
	private readonly ImmutableArray<Kept> _keepingExits;

	private BlockCuts(BlockSyntax block)
	{
		_block = block;

		ImmutableArray<Kept>.Builder plain = ImmutableArray.CreateBuilder<Kept>();
		ImmutableArray<Kept>.Builder keepingExits = ImmutableArray.CreateBuilder<Kept>();

		for (int index = 0; index < block.Statements.Count; index++)
		{
			StatementSyntax statement = block.Statements[index];

			switch (ReadRole(statement))
			{
				case Role.Keep:
					plain.Add(new Kept(index, WalksIn: false));
					keepingExits.Add(new Kept(index, WalksIn: false));

					break;

				case Role.Guard:
					plain.Add(new Kept(index, WalksIn: true));
					keepingExits.Add(new Kept(index, WalksIn: !HoldsAnExit(statement)));

					break;

				default:
					if (HoldsAnExit(statement))
						keepingExits.Add(new Kept(index, WalksIn: false));

					break;
			}
		}

		_plain = plain.ToImmutable();
		_keepingExits = keepingExits.ToImmutable();
	}

	public static BlockCuts For(BlockSyntax block) => s_blocks.GetOrCompute(block, static block => new BlockCuts(block));

	/// <summary>Whether the node returns or throws out of the lambda or method it stands in.</summary>
	/// <remarks>
	/// It is cached because each call below the outermost one reads the other branch of a guard in full.
	/// </remarks>
	public static bool HoldsAnExit(SyntaxNode node) => s_exits.GetOrCompute(node, static node => ReadHoldsAnExit(node));

	/// <summary>Lists the statements a speculation keeps, and cuts the branches of the guards among them.</summary>
	public List<StatementSyntax> CollectRetained(StatementSyntax child, bool keepsExits, HashSet<SyntaxNode> cut, HashSet<SyntaxNode> path)
	{
		SyntaxList<StatementSyntax> statements = _block.Statements;
		ImmutableArray<Kept> kept = keepsExits ? _keepingExits : _plain;

		int childIndex = FindIndex(statements, child);
		List<StatementSyntax> retained = new(kept.Length + 1);
		bool placed = false;

		foreach (Kept entry in kept)
		{
			if (entry.Index == childIndex)
				continue;

			if (!placed && entry.Index > childIndex)
			{
				retained.Add(child);
				placed = true;
			}

			StatementSyntax statement = statements[entry.Index];
			retained.Add(statement);

			if (entry.WalksIn)
				WalkIntoGuard((IfStatementSyntax)statement, cut, path);
		}

		if (!placed)
			retained.Add(child);

		return retained;
	}

	/// <summary>A guard keeps its condition, so the walk goes into the <see langword="if"/> to its branches.</summary>
	private static void WalkIntoGuard(IfStatementSyntax ifStatement, HashSet<SyntaxNode> cut, HashSet<SyntaxNode> path)
	{
		path.Add(ifStatement);
		cut.Add(ifStatement.Statement);

		if (ifStatement.Else is { } elseClause)
			cut.Add(elseClause);
	}

	private static int FindIndex(SyntaxList<StatementSyntax> statements, StatementSyntax child)
	{
		int low = 0;
		int high = statements.Count - 1;

		while (low < high)
		{
			int middle = low + ((high - low) / 2);

			if (statements[middle].SpanStart < child.SpanStart)
				low = middle + 1;
			else
				high = middle;
		}

		return low;
	}

	/// <summary>Reads whether a statement beside the path is cut, kept, or a guard kept for its condition.</summary>
	private static Role ReadRole(StatementSyntax statement)
	{
		if (statement is LocalDeclarationStatementSyntax or LocalFunctionStatementSyntax)
			return Role.Keep;

		if (statement is not IfStatementSyntax ifStatement)
			return DeclaresAVariable(statement) ? Role.Keep : Role.Cut;

		return DeclaresAVariable(ifStatement.Condition) ? Role.Guard : Role.Cut;
	}

	private static bool DeclaresAVariable(SyntaxNode node)
	{
		return node.DescendantNodesAndSelf()
			.Any(static descendant => descendant is SingleVariableDesignationSyntax or ParenthesizedVariableDesignationSyntax);
	}

	private static bool ReadHoldsAnExit(SyntaxNode node)
	{
		return node.DescendantNodesAndSelf(static inner => inner is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
			.Any(static inner => inner is ReturnStatementSyntax or ThrowStatementSyntax);
	}

	private static readonly WeakCache<BlockSyntax, BlockCuts> s_blocks = new();

	private static readonly WeakCache<SyntaxNode, bool> s_exits = new();
}
