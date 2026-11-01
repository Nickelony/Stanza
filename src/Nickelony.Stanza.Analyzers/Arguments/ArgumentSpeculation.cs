using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers.Arguments;

/// <summary>Checks that a call with its argument names added or removed still binds to the same method.</summary>
internal static class ArgumentSpeculation
{
	/// <summary>
	/// Returns <see langword="false"/> for a call it cannot check, such as one that binds to no method today.
	/// </summary>
	public static bool KeepsTheBoundMethod(
		SemanticModel semanticModel,
		SyntaxNode call,
		List<(ArgumentSyntax Argument, string Name)> arguments,
		bool namesThem,
		CancellationToken cancellationToken
	)
	{
		if (BuildSpeculation(call, arguments, namesThem) is not { } speculation)
			return false;

		SyntaxNode scope = speculation.Scope;
		SyntaxNode edited = speculation.Edited;

		SyntaxAnnotation marker = speculation.CallMark;

		if (semanticModel.GetSymbolInfo(call, cancellationToken).Symbol is not { } bound)
			return false;

		var boundOuters = new List<(SyntaxAnnotation Mark, ISymbol Symbol)>();

		foreach ((SyntaxAnnotation mark, SyntaxNode outer) in speculation.Outers)
		{
			if (semanticModel.GetSymbolInfo(outer, cancellationToken).Symbol is not { } outerSymbol)
				return false;

			boundOuters.Add((mark, outerSymbol));
		}

		// Alone, a target-typed creation has no target to take its type from, so it is spelled with the type it creates.
		if (edited is ImplicitObjectCreationExpressionSyntax creation)
		{
			if (BuildTypedCreation(semanticModel, call, creation, cancellationToken) is not { } typed)
				return false;

			edited = typed;
		}

		if (scope == call && edited is ExpressionSyntax expression)
		{
			ISymbol? speculated = semanticModel
				.GetSpeculativeSymbolInfo(call.SpanStart, expression, SpeculativeBindingOption.BindAsExpression)
				.Symbol;

			return SymbolEqualityComparer.Default.Equals(bound, speculated);
		}

		// A query or a `?.` is speculated as a statement, since only a speculative model answers for the call inside it.
		if (edited is ExpressionSyntax outermostAccess)
			edited = SyntaxFactory.ExpressionStatement(outermostAccess);

		if (BuildSpeculativeModel(semanticModel, scope.SpanStart, edited) is not { } speculativeModel)
			return false;

		SyntaxNode editedCall = edited.GetAnnotatedNodes(marker).Single();

		if (!SymbolEqualityComparer.Default.Equals(bound, speculativeModel.GetSymbolInfo(editedCall, cancellationToken).Symbol))
			return false;

		foreach ((SyntaxAnnotation mark, ISymbol outerSymbol) in boundOuters)
		{
			SyntaxNode editedOuter = edited.GetAnnotatedNodes(mark).Single();

			if (!SymbolEqualityComparer.Default.Equals(outerSymbol, speculativeModel.GetSymbolInfo(editedOuter, cancellationToken).Symbol))
				return false;
		}

		return true;
	}

	/// <summary>Cuts the scope down to the path to the call, or <see langword="null"/> without a scope.</summary>
	/// <remarks>
	/// This is the work each speculation costs, so a test counts what it keeps.
	/// </remarks>
	internal static Speculation? BuildSpeculation(SyntaxNode call, List<(ArgumentSyntax Argument, string Name)> arguments, bool namesThem)
	{
		// Names added or removed can change which candidates of a call that takes the lambda around it apply,
		// so each such call is checked too.
		List<SyntaxNode> outers = FindCallsTakingTheLambdaAround(call);

		SyntaxNode? scope = outers.Count > 0 ? FindSpeculationScope(outers[outers.Count - 1], allowsTheCallAlone: false) : FindSpeculationScope(call);

		if (scope is null)
			return null;

		var callMark = new SyntaxAnnotation();
		var marks = new Dictionary<SyntaxNode, SyntaxAnnotation> { [call] = callMark };
		var markedOuters = new List<(SyntaxAnnotation Mark, SyntaxNode Outer)>();

		foreach (SyntaxNode outer in outers)
		{
			var mark = new SyntaxAnnotation();

			marks[outer] = mark;
			markedOuters.Add((mark, outer));
		}

		var names = new Dictionary<SyntaxNode, string>();

		HashSet<SyntaxNode> path = [.. call.AncestorsAndSelf().TakeWhile(node => node != scope.Parent)];

		foreach ((ArgumentSyntax argument, string name) in arguments)
		{
			names[argument] = name;
			path.UnionWith(argument.AncestorsAndSelf().TakeWhile(node => node != call));
		}

		// A statement or a clause holds the statements beside the path to the call, so they are emptied,
		// and each speculation costs the length of the path, however many calls a lambda holds.
		// A call that binds alone has no statements in its scope.
		SyntaxNode outermost = outers.Count > 0 ? outers[outers.Count - 1] : call;
		HashSet<SyntaxNode> cut = [];
		Dictionary<BlockSyntax, List<StatementSyntax>> retained = [];

		if (scope != call)
			CollectCuts(scope, call, outermost, path, cut, retained);

		SyntaxNode edited = new SpeculationRewriter(marks, path, cut, retained, names, namesThem).Visit(scope)!;

		return new Speculation(Scope: scope, Edited: edited, CallMark: callMark, Outers: markedOuters);
	}

	/// <summary>The scope cut down to the path to the call, and the marks that find the calls in it.</summary>
	internal sealed record Speculation(
		SyntaxNode Scope,
		SyntaxNode Edited,
		SyntaxAnnotation CallMark,
		List<(SyntaxAnnotation Mark, SyntaxNode Outer)> Outers
	);

	/// <summary>
	/// Returns the call when it binds alone, else its outermost query or <c>?.</c> in a statement, or its clause.
	/// </summary>
	public static SyntaxNode? FindSpeculationScope(SyntaxNode call) => FindSpeculationScope(call, allowsTheCallAlone: true);

	private static SyntaxNode? FindSpeculationScope(SyntaxNode call, bool allowsTheCallAlone)
	{
		// Speculating only the call costs the call's own size, however many calls its statement or initializer holds.
		// The call binds alone until a query or a `?.` stands between it and its statement, or its member outside one,
		// since only those give it what it binds with.
		bool bindsAlone = allowsTheCallAlone && call is ExpressionSyntax;

		SyntaxNode? outermostAccess = null;
		SyntaxNode? stop = null;

		SyntaxNode child = call;

		for (SyntaxNode? node = call; node is not null; child = node, node = node.Parent)
		{
			if (bindsAlone && node != call)
			{
				if (node is StatementSyntax)
					return call;

				// A lambda's body binds with the types its parameters were given, so a call in it binds alone, in an expression body as in a block.
				if (node is AnonymousFunctionExpressionSyntax { ExpressionBody: { } expressionBody } && expressionBody == child)
					return call;

				// A query gives its range variables their types from the clauses around it, and a call after `?.` binds only with its access.
				if (node is AnonymousFunctionExpressionSyntax or QueryExpressionSyntax or ConditionalAccessExpressionSyntax)
					bindsAlone = false;

				// The stop was reached below the point where the call stopped binding alone, so it is the scope.
				if (!bindsAlone && stop is not null)
					return stop;
			}

			if (node is QueryExpressionSyntax or ConditionalAccessExpressionSyntax)
				outermostAccess = node;

			SyntaxNode? reached = FindStop(node, outermostAccess);

			if (reached is null)
				continue;

			if (!bindsAlone)
				return reached;

			// The call may still bind alone, so the walk goes on to find out before it settles on this stop.
			stop ??= reached;
		}

		return bindsAlone ? call : stop;
	}

	/// <summary>Returns the scope a node ends the walk at: its statement or access, or its clause.</summary>
	private static SyntaxNode? FindStop(SyntaxNode node, SyntaxNode? outermostAccess)
	{
		// A statement can hold far more than the call, such as an `if` and every `else if` below it.
		// One in a lambda of an initializer of a field, property or event cannot be speculated alone, so its clause is taken.
		if (node is StatementSyntax && !IsInInitializerLambda(node))
			return outermostAccess ?? node;

		if (node is ArrowExpressionClauseSyntax or ConstructorInitializerSyntax or PrimaryConstructorBaseTypeSyntax)
			return node;

		// A local declaration's initializer is speculated with its statement, and a field, property or parameter initializer as a clause.
		return node is EqualsValueClauseSyntax && node.FirstAncestorOrSelf<StatementSyntax>() is null ? node : null;
	}

	/// <summary>Finds each call, inside out, that takes a lambda argument which holds the call.</summary>
	private static List<SyntaxNode> FindCallsTakingTheLambdaAround(SyntaxNode call)
	{
		List<SyntaxNode> outers = [];

		for (SyntaxNode? node = call.Parent; node is not null; node = node.Parent)
		{
			if (
				node is AnonymousFunctionExpressionSyntax
				&& node.Parent is ArgumentSyntax { Parent: BaseArgumentListSyntax { Parent: { } owner } }
				&& ArgumentBinding.FindArgumentList(owner) is not null
			)
			{
				outers.Add(owner);
			}
		}

		return outers;
	}

	/// <summary>Finds what stands beside the path to the call, which the call's binding cannot see.</summary>
	/// <remarks>
	/// Declarations, local functions and expression variables stay, as later statements see them.
	/// </remarks>
	private static void CollectCuts(
		SyntaxNode scope,
		SyntaxNode call,
		SyntaxNode outermost,
		HashSet<SyntaxNode> path,
		HashSet<SyntaxNode> cut,
		Dictionary<BlockSyntax, List<StatementSyntax>> retained
	)
	{
		SyntaxNode child = call;

		// Below the outermost call checked, a statement that exits its lambda stays, as that call's binding reads it.
		bool keepsExits = call != outermost;

		for (SyntaxNode? node = call.Parent; node is not null && node != scope; child = node, node = node.Parent)
		{
			if (node == outermost)
				keepsExits = false;

			switch (node)
			{
				case IfStatementSyntax ifStatement:
					if (ifStatement.Statement != child && !(keepsExits && BlockCuts.HoldsAnExit(ifStatement.Statement)))
						cut.Add(ifStatement.Statement);

					if (ifStatement.Else is { } elseClause && elseClause != child && !(keepsExits && BlockCuts.HoldsAnExit(elseClause)))
						cut.Add(elseClause);

					break;

				case BlockSyntax block:
					// The facts of a block are read once, so each call in it costs the statements it keeps and not all of them.
					retained[block] = BlockCuts.For(block).CollectRetained(child: (StatementSyntax)child, keepsExits, cut, path);
					break;
			}
		}
	}

	/// <summary>Whether the statement sits in a lambda of an initializer that no statement holds.</summary>
	private static bool IsInInitializerLambda(SyntaxNode statement)
	{
		StatementSyntax outermost = statement.AncestorsAndSelf().OfType<StatementSyntax>().Last();
		return outermost.Ancestors().Any(static node => node is EqualsValueClauseSyntax);
	}

	/// <summary>Spells a target-typed creation with the type it creates, so it binds without its target.</summary>
	private static ObjectCreationExpressionSyntax? BuildTypedCreation(
		SemanticModel semanticModel,
		SyntaxNode call,
		ImplicitObjectCreationExpressionSyntax creation,
		CancellationToken cancellationToken
	)
	{
		// The created type, not the converted one: `S? value = new(...)` creates an `S`.
		if (semanticModel.GetTypeInfo(call, cancellationToken).Type is not INamedTypeSymbol type)
			return null;

		// An initializer plays no part in choosing the constructor, so it is left out of the speculation.
		TypeSyntax typeName = SyntaxFactory.ParseTypeName(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
		return SyntaxFactory.ObjectCreationExpression(typeName, creation.ArgumentList, initializer: null);
	}

	private static SemanticModel? BuildSpeculativeModel(SemanticModel semanticModel, int position, SyntaxNode edited)
	{
		SemanticModel? speculativeModel = null;

		bool found = edited switch
		{
			StatementSyntax statement => semanticModel.TryGetSpeculativeSemanticModel(position, statement, out speculativeModel),
			ArrowExpressionClauseSyntax arrow => semanticModel.TryGetSpeculativeSemanticModel(position, arrow, out speculativeModel),
			EqualsValueClauseSyntax value => semanticModel.TryGetSpeculativeSemanticModel(position, value, out speculativeModel),
			ConstructorInitializerSyntax initializer => semanticModel.TryGetSpeculativeSemanticModel(position, initializer, out speculativeModel),
			PrimaryConstructorBaseTypeSyntax primary => semanticModel.TryGetSpeculativeSemanticModel(position, primary, out speculativeModel),
			_ => false,
		};

		return found ? speculativeModel : null;
	}

	private static NameColonSyntax BuildNameColon(string name)
	{
		return SyntaxFactory.NameColon(
			SyntaxFactory.IdentifierName(
				SyntaxFactory.Identifier(
					leading: default,
					contextualKind: SyntaxKind.IdentifierToken,
					text: ArgumentBinding.EscapeName(name),
					valueText: name,
					trailing: default
				)
			)
		);
	}

	/// <summary>Rewrites only the path to the call, and carries the rest of the scope over unwalked.</summary>
	private sealed class SpeculationRewriter(
		Dictionary<SyntaxNode, SyntaxAnnotation> marks,
		HashSet<SyntaxNode> path,
		HashSet<SyntaxNode> cut,
		Dictionary<BlockSyntax, List<StatementSyntax>> retained,
		Dictionary<SyntaxNode, string> names,
		bool namesThem
	) : CSharpSyntaxRewriter
	{
		public override SyntaxNode? Visit(SyntaxNode? node)
		{
			if (node is null)
				return null;

			if (cut.Contains(node))
				return node is ElseClauseSyntax ? null : SyntaxFactory.EmptyStatement();

			if (!path.Contains(node))
				return node;

			SyntaxNode rewritten = base.Visit(node)!;

			if (marks.TryGetValue(node, out SyntaxAnnotation? mark))
				return rewritten.WithAdditionalAnnotations(mark);

			return names.TryGetValue(node, out string? name)
				? ((ArgumentSyntax)rewritten).WithNameColon(namesThem ? BuildNameColon(name) : null)
				: rewritten;
		}

		/// <summary>Rebuilds a block from the statements it keeps, without visiting the ones it cuts.</summary>
		public override SyntaxNode? VisitBlock(BlockSyntax node)
		{
			if (!retained.TryGetValue(node, out List<StatementSyntax>? statements))
				return base.VisitBlock(node);

			List<StatementSyntax> visited = new(statements.Count);

			foreach (StatementSyntax statement in statements)
				visited.Add((StatementSyntax)Visit(statement)!);

			return node.WithStatements(SyntaxFactory.List(visited));
		}
	}
}
