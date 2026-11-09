using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers.Limits;

/// <summary>Finds the members whose bodies hold more statements than a limit allows.</summary>
internal static class MemberStatements
{
	/// <summary>Collects each member or local function whose body holds more statements than the limit.</summary>
	/// <remarks>
	/// A block does not count, a local function is judged on its own, and a lambda joins its member.
	/// </remarks>
	public static ImmutableArray<SyntaxNode> CollectLongMembers(SyntaxNode root, int limit, CancellationToken cancellationToken)
	{
		List<Member> members = [];
		Stack<(SyntaxNode Node, Member? Member)> pending = new();

		pending.Push((root, null));

		// The walk keeps its own stack, so a deep body cannot overflow the call stack, and it visits each node once.
		while (pending.Count > 0)
		{
			cancellationToken.ThrowIfCancellationRequested();

			(SyntaxNode node, Member? member) = pending.Pop();

			if (member is not null && node is StatementSyntax and not BlockSyntax)
				member.Count++;

			if (node is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax or LocalFunctionStatementSyntax)
			{
				member = new Member(node);
				members.Add(member);
			}

			foreach (SyntaxNode child in node.ChildNodes())
				pending.Push((child, member));
		}

		ImmutableArray<SyntaxNode>.Builder longMembers = ImmutableArray.CreateBuilder<SyntaxNode>();

		foreach (Member member in members)
		{
			if (member.Count > limit)
				longMembers.Add(member.Node);
		}

		return longMembers.ToImmutable();
	}

	/// <summary>Reads the token that names a member, which is where the rule reports it.</summary>
	public static SyntaxToken ReadNameToken(SyntaxNode member)
	{
		return member switch
		{
			MethodDeclarationSyntax method => method.Identifier,
			ConstructorDeclarationSyntax constructor => constructor.Identifier,
			DestructorDeclarationSyntax destructor => destructor.Identifier,
			OperatorDeclarationSyntax declaration => declaration.OperatorToken,
			ConversionOperatorDeclarationSyntax conversion => conversion.OperatorKeyword,
			AccessorDeclarationSyntax accessor => accessor.Keyword,
			LocalFunctionStatementSyntax function => function.Identifier,
			_ => member.GetFirstToken(),
		};
	}

	private sealed class Member(SyntaxNode node)
	{
		public SyntaxNode Node { get; } = node;
		public int Count { get; set; }
	}
}
