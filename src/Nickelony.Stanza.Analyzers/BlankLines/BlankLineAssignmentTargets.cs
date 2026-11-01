using System;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Reads what an assignment writes, so a run of assignments splits by its targets.</summary>
/// <remarks>
/// Only the symbol tells a field from a property, so it binds a semantic model where the setting is <c>split</c>.
/// </remarks>
internal static class BlankLineAssignmentTargets
{
	private static readonly WeakCache<SemanticModel, StatementReads<BlankLineGrouping.AssignmentTarget>> s_reads = new();

	/// <summary>Finds the reader of the targets where the setting splits the runs, or none.</summary>
	public static Func<SyntaxNode, BlankLineGrouping.AssignmentTarget>? FindReader(ContainerContext container) =>
		container.Options.SplitsAssignmentTargets ? GetReads(container.Context.SemanticModel).GetReader(container.CancellationToken) : null;

	/// <summary>Gets the answers of a model, so a statement is bound once however often it is read.</summary>
	public static StatementReads<BlankLineGrouping.AssignmentTarget> GetReads(SemanticModel semanticModel) =>
		s_reads.GetOrCompute(semanticModel, static model => new StatementReads<BlankLineGrouping.AssignmentTarget>(model, TryReadTarget));

	/// <summary>Reads what an assignment statement writes, or declines any other statement.</summary>
	private static bool TryReadTarget(
		SemanticModel semanticModel,
		SyntaxNode statement,
		CancellationToken cancellationToken,
		out BlankLineGrouping.AssignmentTarget target
	)
	{
		target = default;

		if (statement is not ExpressionStatementSyntax { Expression: { } expression })
			return false;

		switch (BlankLineExpressions.FindAssignment(expression))
		{
			case AssignmentExpressionSyntax assignment:
				bool subscribes = assignment.IsKind(SyntaxKind.AddAssignmentExpression) || assignment.IsKind(SyntaxKind.SubtractAssignmentExpression);
				target = ReadWritten(semanticModel, written: assignment.Left, subscribes, cancellationToken);

				return true;

			case PrefixUnaryExpressionSyntax step:
				target = ReadWritten(semanticModel, written: step.Operand, subscribes: false, cancellationToken);
				return true;

			case PostfixUnaryExpressionSyntax step:
				target = ReadWritten(semanticModel, written: step.Operand, subscribes: false, cancellationToken);
				return true;

			default:
				return false;
		}
	}

	/// <summary>Reads the kind and the root an expression writes, where a subscription has neither.</summary>
	private static BlankLineGrouping.AssignmentTarget ReadWritten(
		SemanticModel semanticModel,
		ExpressionSyntax written,
		bool subscribes,
		CancellationToken cancellationToken
	)
	{
		written = BlankLineExpressions.UnwrapAwaitParenthesesAndSuppression(written);

		switch (written)
		{
			// An element is read off its form, so it keeps its kind whether or not it binds.
			case ElementAccessExpressionSyntax or ElementBindingExpressionSyntax:
			case PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PointerIndirectionExpression }:
				return new BlankLineGrouping.AssignmentTarget(
					Kind: BlankLineGrouping.AssignmentKind.Element,
					Root: FindRoot(semanticModel, expression: written, cancellationToken)
				);

			case DeclarationExpressionSyntax:
				return new BlankLineGrouping.AssignmentTarget(Kind: BlankLineGrouping.AssignmentKind.Local);

			case TupleExpressionSyntax tuple:
				return new BlankLineGrouping.AssignmentTarget(Kind: ReadDeconstructed(semanticModel, tuple, cancellationToken));
		}

		ISymbol? symbol = semanticModel.GetSymbolInfo(written, cancellationToken).Symbol;

		BlankLineGrouping.AssignmentKind kind = symbol switch
		{
			IFieldSymbol => BlankLineGrouping.AssignmentKind.Field,
			IPropertySymbol { IsIndexer: true } => BlankLineGrouping.AssignmentKind.Element,
			IPropertySymbol => BlankLineGrouping.AssignmentKind.Property,
			ILocalSymbol or IParameterSymbol => BlankLineGrouping.AssignmentKind.Local,
			// A field-like event written with `=` writes its delegate field, and a subscription is left to ZA0007.
			IEventSymbol => subscribes ? BlankLineGrouping.AssignmentKind.None : BlankLineGrouping.AssignmentKind.Field,
			_ => BlankLineGrouping.AssignmentKind.None,
		};

		if (kind == BlankLineGrouping.AssignmentKind.None)
			return default;

		// A name on its own is its own root, so only a chain binds again.
		ISymbol? root = FindReceiver(written) is null ? ToRoot(symbol) : FindRoot(semanticModel, expression: written, cancellationToken);

		return new BlankLineGrouping.AssignmentTarget(Kind: kind, Root: root);
	}

	/// <summary>Finds the symbol a chain starts from, or none where it does not bind.</summary>
	/// <remarks>
	/// <c>this.</c>, <c>base.</c>, a type and a namespace qualify the member after them, which is the root.
	/// </remarks>
	private static ISymbol? FindRoot(SemanticModel semanticModel, ExpressionSyntax expression, CancellationToken cancellationToken)
	{
		expression = BlankLineExpressions.UnwrapAwaitParenthesesAndSuppression(expression);

		if (FindReceiver(expression) is not { } receiver)
			return ToRoot(semanticModel.GetSymbolInfo(expression, cancellationToken).Symbol);

		if (receiver is ThisExpressionSyntax or BaseExpressionSyntax)
			return expression is MemberAccessExpressionSyntax ? ToRoot(semanticModel.GetSymbolInfo(expression, cancellationToken).Symbol) : null;

		if (FindRoot(semanticModel, expression: receiver, cancellationToken) is { } root)
			return root;

		bool qualifies =
			expression is MemberAccessExpressionSyntax && semanticModel.GetSymbolInfo(receiver, cancellationToken).Symbol is INamespaceOrTypeSymbol;

		return qualifies ? ToRoot(semanticModel.GetSymbolInfo(expression, cancellationToken).Symbol) : null;
	}

	/// <summary>Finds what a member or an element is reached through, or none for a name on its own.</summary>
	private static ExpressionSyntax? FindReceiver(ExpressionSyntax expression)
	{
		return expression switch
		{
			MemberAccessExpressionSyntax access => access.Expression,
			ElementAccessExpressionSyntax access => access.Expression,
			ConditionalAccessExpressionSyntax access => access.Expression,
			PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PointerIndirectionExpression } indirection => indirection.Operand,
			MemberBindingExpressionSyntax or ElementBindingExpressionSyntax => FindConditionalReceiver(expression),
			_ => null,
		};
	}

	/// <summary>Finds the receiver a binding such as <c>?.Name</c> continues.</summary>
	private static ExpressionSyntax? FindConditionalReceiver(SyntaxNode binding)
	{
		for (SyntaxNode node = binding; node.Parent is { } parent; node = parent)
		{
			if (parent is ConditionalAccessExpressionSyntax access && access.WhenNotNull == node)
				return access.Expression;
		}

		return null;
	}

	/// <summary>Takes a symbol that holds a value as a root, and declines a method, a type or no symbol.</summary>
	private static ISymbol? ToRoot(ISymbol? symbol) =>
		symbol is IFieldSymbol or IPropertySymbol { IsIndexer: false } or ILocalSymbol or IParameterSymbol or IEventSymbol ? symbol : null;

	/// <summary>Reads the one kind a deconstruction writes, where a target of no kind adds none.</summary>
	private static BlankLineGrouping.AssignmentKind ReadDeconstructed(
		SemanticModel semanticModel,
		TupleExpressionSyntax tuple,
		CancellationToken cancellationToken
	)
	{
		BlankLineGrouping.AssignmentKind kind = BlankLineGrouping.AssignmentKind.None;

		foreach (ArgumentSyntax argument in tuple.Arguments)
		{
			BlankLineGrouping.AssignmentKind element = ReadWritten(
				semanticModel,
				written: argument.Expression,
				subscribes: false,
				cancellationToken
			).Kind;

			if (element == BlankLineGrouping.AssignmentKind.None || element == kind)
				continue;

			if (kind != BlankLineGrouping.AssignmentKind.None || element == BlankLineGrouping.AssignmentKind.Several)
				return BlankLineGrouping.AssignmentKind.Several;

			kind = element;
		}

		return kind;
	}
}
