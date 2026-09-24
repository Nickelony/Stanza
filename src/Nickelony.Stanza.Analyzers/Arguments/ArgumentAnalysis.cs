using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Nickelony.Stanza.Analyzers.Arguments;

/// <summary>Reports the call arguments that lack a name and the argument names a call does not need.</summary>
internal static class ArgumentAnalysis
{
	public static readonly ImmutableArray<SyntaxKind> CallKinds =
	[
		SyntaxKind.BaseConstructorInitializer,
		SyntaxKind.ElementAccessExpression,
		SyntaxKind.ImplicitObjectCreationExpression,
		SyntaxKind.InvocationExpression,
		SyntaxKind.ObjectCreationExpression,
		SyntaxKind.PrimaryConstructorBaseType,
		SyntaxKind.ThisConstructorInitializer,
	];

	public static void ReportUnnamedArguments(
		SyntaxNodeAnalysisContext context,
		BaseArgumentListSyntax argumentList,
		StanzaOptions options,
		DiagnosticDescriptor descriptor
	)
	{
		// A count of zero makes the rule report nothing before the call is bound.
		if (options.NamedArgumentCount == 0)
			return;

		if (argumentList.Arguments.Count < options.NamedArgumentCount)
			return;

		// A call whose arguments all carry a name has nothing to name, so no lambda around it is bound.
		if (!HasAnUnnamedArgument(argumentList))
			return;

		// An expression tree may not contain a named argument (CS0853).
		if (ArgumentBinding.IsInsideExpressionTree(context.SemanticModel, context.Node, context.CancellationToken))
			return;

		ImmutableArray<IArgumentOperation> bound = ArgumentBinding.FindBoundArguments(context.SemanticModel, context.Node, context.CancellationToken);

		if (bound.IsDefaultOrEmpty)
			return;

		IArgumentOperation?[] pairing = ArgumentBinding.PairArgumentsWithBindings(argumentList, bound);

		if (!CanNameEveryArgument(pairing))
			return;

		// Before C# 7.2 a named argument cannot precede a positional one,
		// so only the leading run of arguments that carry their names keeps the shorthand, as RemoveRedundantArgumentName reads it.
		bool namesMayPrecedePositions = ArgumentBinding.AllowsNamesBeforePositions(context.Node.SyntaxTree);
		bool inLeadingRun = true;

		var unnamed = new List<(ArgumentSyntax Argument, string Name)>();
		ImmutableArray<(int Index, string Name)>.Builder names = ImmutableArray.CreateBuilder<(int Index, string Name)>();

		for (int index = 0; index < pairing.Length; index++)
		{
			// CanNameEveryArgument holds, so every argument has a parameter and the non-null assertions hold.
			ArgumentSyntax argument = argumentList.Arguments[index];
			string name = pairing[index]!.Parameter!.Name;

			bool keepsShorthand =
				options.AllowsNamedArgumentShorthand
				&& (namesMayPrecedePositions || inLeadingRun)
				&& CarriesTheName(argument.Expression, name, options.NamedArgumentComparison);

			if (argument.NameColon is null && !keepsShorthand)
			{
				unnamed.Add((argument, name));
				names.Add((index, name));
			}

			inLeadingRun &= argument.NameColon is null && keepsShorthand;
		}

		// Names can make another overload applicable, so the call has to bind as it does today.
		if (
			unnamed.Count == 0
			|| !ArgumentSpeculation.KeepsTheBoundMethod(
				semanticModel: context.SemanticModel,
				call: context.Node,
				arguments: unnamed,
				namesThem: true,
				cancellationToken: context.CancellationToken
			)
		)
		{
			return;
		}

		// One report covers the call, so one fix names every argument the speculation above checked together.
		context.ReportDiagnostic(
			Diagnostic.Create(descriptor, argumentList.GetLocation(), properties: new FixOptions(ArgumentNames: names.ToImmutable()).ToProperties())
		);
	}

	public static void ReportRedundantNames(
		SyntaxNodeAnalysisContext context,
		BaseArgumentListSyntax argumentList,
		StanzaOptions options,
		DiagnosticDescriptor descriptor
	)
	{
		// A file that requires names would have the naming rule write back the names this rule removes,
		// so the rule runs only where the shorthand is allowed.
		if (!options.AllowsNamedArgumentShorthand)
			return;

		// A call that writes no name colon has no name to take away, so it skips the binding.
		if (!WritesAName(argumentList))
			return;

		ImmutableArray<IArgumentOperation> bound = ArgumentBinding.FindBoundArguments(context.SemanticModel, context.Node, context.CancellationToken);

		if (bound.IsDefaultOrEmpty)
			return;

		int implicitArgumentCount = ArgumentBinding.CountLeadingImplicitArguments(bound, argumentList);
		IArgumentOperation?[] pairing = ArgumentBinding.PairArgumentsWithBindings(argumentList, bound);

		int positional = CountPositionalPrefix(argumentList, pairing, implicitArgumentCount);
		List<(ArgumentSyntax Argument, string Name)> redundant = CollectRedundantNames(context, options, argumentList, pairing, positional);

		// Positions can match while another overload wins once the names are gone.
		if (
			redundant.Count == 0
			|| !ArgumentSpeculation.KeepsTheBoundMethod(
				semanticModel: context.SemanticModel,
				call: context.Node,
				arguments: redundant,
				namesThem: false,
				cancellationToken: context.CancellationToken
			)
		)
		{
			return;
		}

		// CollectRedundantNames keeps only arguments that write a name, so each has its name colon.
		foreach ((ArgumentSyntax argument, _) in redundant)
			context.ReportDiagnostic(Diagnostic.Create(descriptor, argument.NameColon!.GetLocation()));
	}

	/// <summary>
	/// Gates any report, as naming one argument while another cannot carry a name leaves a shape no fix could finish.
	/// </summary>
	private static bool CanNameEveryArgument(IArgumentOperation?[] pairing)
	{
		foreach (IArgumentOperation? binding in pairing)
		{
			if (binding is not { Parameter.IsParams: false })
				return false;
		}

		return true;
	}

	/// <summary>Counts the leading arguments whose position does a name's job, each at its parameter's place.</summary>
	private static int CountPositionalPrefix(BaseArgumentListSyntax argumentList, IArgumentOperation?[] pairing, int implicitArgumentCount)
	{
		int positional = 0;

		// A reduced extension call's receiver is an implicit argument, so the places start one along.
		while (positional < argumentList.Arguments.Count)
		{
			if (
				pairing[positional] is not { } target
				|| target.Parameter is not { IsParams: false }
				|| target.Parameter.Ordinal != positional + implicitArgumentCount
			)
			{
				break;
			}

			positional++;
		}

		return positional;
	}

	private static List<(ArgumentSyntax Argument, string Name)> CollectRedundantNames(
		SyntaxNodeAnalysisContext context,
		StanzaOptions options,
		BaseArgumentListSyntax argumentList,
		IArgumentOperation?[] pairing,
		int positional
	)
	{
		var redundant = new List<(ArgumentSyntax Argument, string Name)>();

		// Before C# 7.2 a named argument has to come after every positional one,
		// so a name that stays ends the run of names that can go.
		bool namesMayPrecedePositions = ArgumentBinding.AllowsNamesBeforePositions(context.Node.SyntaxTree);
		bool keptAName = false;

		for (int index = 0; index < positional; index++)
		{
			ArgumentSyntax argument = argumentList.Arguments[index];
			string name = pairing[index]!.Parameter!.Name;

			bool carriesTheName = CarriesTheName(argument.Expression, name, options.NamedArgumentComparison);

			// An unnamed argument that carries no name gets one from the naming rule, and so does every argument after it,
			// so it ends the run of names that can go as a name that stays does.
			if (argument.NameColon is null)
			{
				keptAName |= !carriesTheName;
				continue;
			}

			if (!carriesTheName)
			{
				keptAName = true;
				continue;
			}

			if (keptAName && !namesMayPrecedePositions)
				break;

			redundant.Add((argument, name));
		}

		return redundant;
	}

	private static bool HasAnUnnamedArgument(BaseArgumentListSyntax argumentList)
	{
		foreach (ArgumentSyntax argument in argumentList.Arguments)
		{
			if (argument.NameColon is null)
				return true;
		}

		return false;
	}

	private static bool WritesAName(BaseArgumentListSyntax argumentList)
	{
		foreach (ArgumentSyntax argument in argumentList.Arguments)
		{
			if (argument.NameColon is not null)
				return true;
		}

		return false;
	}

	/// <summary>Only an identifier or the variable of an <see langword="out"/> declaration carries the name.</summary>
	private static bool CarriesTheName(ExpressionSyntax expression, string name, StringComparison comparison)
	{
		// `this.width`, `options.Width` and `(width)` do not carry the name,
		// so a name on them is never redundant and the naming rule asks for one.
		string? carried = expression switch
		{
			IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
			DeclarationExpressionSyntax { Designation: SingleVariableDesignationSyntax designation } => designation.Identifier.ValueText,
			_ => null,
		};

		return carried is not null && string.Equals(carried, name, comparison);
	}
}
