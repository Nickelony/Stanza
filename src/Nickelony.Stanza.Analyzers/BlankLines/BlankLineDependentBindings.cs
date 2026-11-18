using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Reads adjacent plain local bindings whose values consume their immediate predecessor.</summary>
internal static class BlankLineDependentBindings
{
	public readonly record struct Groups(ImmutableHashSet<int> Inner, ImmutableHashSet<int> Joined, ImmutableHashSet<int> Sections);

	private readonly record struct Binding(ILocalSymbol Symbol, ExpressionSyntax Value);

	/// <summary>Shares one dependency scan across keeper claims and projected borders.</summary>
	public sealed class Reading(object gate)
	{
		private Groups? _groups;

		public Groups Get(ContainerContext container)
		{
			lock (gate)
				return _groups ??= ReadGroups(container);
		}
	}

	public static ImmutableHashSet<int> GetInner(ContainerContext container) => container.DependentBindings?.Get(container).Inner ?? [];
	public static ImmutableHashSet<int> GetJoined(ContainerContext container) => container.DependentBindings?.Get(container).Joined ?? [];
	public static ImmutableHashSet<int> GetSections(ContainerContext container) => container.DependentBindings?.Get(container).Sections ?? [];

	private static Groups ReadGroups(ContainerContext container)
	{
		ImmutableArray<SyntaxNode> siblings = container.Siblings;
		var bindings = new Binding?[siblings.Length];

		for (int index = 0; index < bindings.Length; index++)
		{
			container.CancellationToken.ThrowIfCancellationRequested();
			bindings[index] = FindBinding(container, siblings[index]);
		}

		bool[] dependent = new bool[siblings.Length];

		for (int boundary = 1; boundary < dependent.Length; boundary++)
		{
			dependent[boundary] =
				bindings[boundary - 1] is { } previous
				&& bindings[boundary] is { } next
				&& BlankLineKeptPairs.IsWhitespaceGap(siblings[boundary - 1], siblings[boundary], container.CancellationToken)
				&& !StanzaRuleState.IsOffAt(
					scope: container,
					options: container.Options,
					rule: StanzaRules.KeepDependentBindingsTogether,
					position: siblings[boundary].SpanStart
				)
				&& ReadsValue(
					container.Context.SemanticModel.GetOperation(
						BlankLineExpressions.UnwrapAwaitParenthesesAndSuppression(next.Value),
						container.CancellationToken
					),

					previous.Symbol
				);
		}

		bool CanJoin(int boundary) => boundary > 0 && boundary < dependent.Length && dependent[boundary];
		bool IsLoose(int boundary) => CanJoin(boundary) && BlankLineGaps.HasBlankLineBetween(siblings[boundary - 1], siblings[boundary]);

		ImmutableHashSet<int>.Builder inner = ImmutableHashSet.CreateBuilder<int>();
		ImmutableHashSet<int>.Builder sections = ImmutableHashSet.CreateBuilder<int>();

		for (int boundary = 1; boundary < dependent.Length; boundary++)
		{
			if (!CanJoin(boundary))
				continue;

			if (IsLoose(boundary) && BlankLineSections.IsSectionBreak(container, boundary, isInside: CanJoin, isLoose: IsLoose))
				sections.Add(boundary);
			else
				inner.Add(boundary);
		}

		ImmutableHashSet<int> connected = BlankLineCheckedStanzas.ReadConnectedBindingGaps(container);
		ImmutableHashSet<int> joined = inner.ToImmutable().Union(connected.Where(CanJoin));

		return new Groups(inner.ToImmutable().Except(connected), joined, sections.ToImmutable().Except(connected));
	}

	private static Binding? FindBinding(ContainerContext container, SyntaxNode unit)
	{
		if (unit is not LocalDeclarationStatementSyntax local)
			return null;

		bool ordinary = local.UsingKeyword.RawKind == 0 && local.Declaration.Type is not RefTypeSyntax && local.Declaration.Variables.Count == 1;

		if (!ordinary || StanzaSyntax.IsMultiLine(unit) || BlankLineReceiverCalls.IsJoinedStatement(unit))
			return null;

		VariableDeclaratorSyntax variable = local.Declaration.Variables[0];

		return
			variable.Initializer?.Value is { } value
			&& BlankLineKeptPairs.FindDeclaredLocal(container.Context.SemanticModel, variable, container.CancellationToken) is { } symbol
			? new Binding(symbol, value)
			: null;
	}

	private static bool ReadsValue(IOperation? root, ILocalSymbol symbol)
	{
		if (root is null)
			return false;

		bool reads = false;
		Stack<IOperation> remaining = new();

		remaining.Push(root);

		while (remaining.Count > 0)
		{
			IOperation operation = remaining.Pop();

			if (
				operation.Kind
				is OperationKind.AnonymousFunction
					or OperationKind.LocalFunction
					or OperationKind.NameOf
					or OperationKind.TranslatedQuery
					or OperationKind.AddressOf
			)
			{
				continue;
			}

			if (operation is ILocalReferenceOperation local && SymbolEqualityComparer.Default.Equals(local.Local, symbol))
			{
				if (WritesVariable(local))
					return false;

				reads = true;
			}

			foreach (IOperation child in operation.ChildOperations)
				remaining.Push(child);
		}

		return reads;
	}

	private static bool WritesVariable(ILocalReferenceOperation local)
	{
		IOperation target = local;

		while (target.Parent is ITupleOperation or IConversionOperation { IsImplicit: true })
			target = target.Parent;

		return target.Parent switch
		{
			IAssignmentOperation assignment when assignment.Target == target => true,
			IIncrementOrDecrementOperation update when update.Target == target => true,
			_ when ReadArgumentRefKind(target) is RefKind.Out or RefKind.Ref => true,
			_ => false,
		};
	}

	private static RefKind ReadArgumentRefKind(IOperation target)
	{
		if (target.Parent is IArgumentOperation { Parameter: { } parameter })
			return parameter.RefKind;

		SyntaxNode syntax = target.Syntax;

		while (
			syntax.Parent is { } parent && (parent is ParenthesizedExpressionSyntax || parent.IsKind(SyntaxKind.SuppressNullableWarningExpression))
		)
		{
			syntax = parent;
		}

		return syntax.Parent is ArgumentSyntax argument
			? argument.RefKindKeyword.Kind() switch
			{
				SyntaxKind.OutKeyword => RefKind.Out,
				SyntaxKind.RefKeyword => RefKind.Ref,
				SyntaxKind.InKeyword => RefKind.In,
				_ => RefKind.None,
			}
			: RefKind.None;
	}
}
