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
	public static void ReportUnnamedArguments(
		SyntaxNodeAnalysisContext context,
		BaseArgumentListSyntax argumentList,
		StanzaOptions options,
		DiagnosticDescriptor descriptor
	)
	{
		if (FindUnnamedArguments(context, argumentList, options) is { } naming)
			ReportNaming(context, argumentList, descriptor, naming);
	}

	public static void ReportUnnamedLiterals(
		SyntaxNodeAnalysisContext context,
		BaseArgumentListSyntax argumentList,
		StanzaOptions options,
		StanzaRule rule
	)
	{
		// A lone argument stays, and so does a call with no literal to name, before the call is bound.
		if (argumentList.Arguments.Count < 2 || !HasAnUnnamedLiteral(argumentList, options))
			return;

		if (ArgumentBinding.HasConditionalDirectiveAmongArguments(argumentList))
			return;

		if (FindPairing(context, argumentList) is not { } pairing)
			return;

		if (FindLiteralsToName(context, argumentList, options, pairing) is not { } naming)
			return;

		// The rule that names every argument covers the literals, so it owns a call it reports.
		StanzaRule namesEveryArgument = rule.GetDeferral(DiagnosticDescriptors.NameArgumentsOfCall);

		if (
			!StanzaRuleState.IsOffAt(scope: context, options, rule: namesEveryArgument, position: argumentList.SpanStart)
			&& FindUnnamedArguments(context, argumentList, options) is not null
		)
		{
			return;
		}

		ReportNaming(context, argumentList, descriptor: rule.Descriptor, naming);
	}

	/// <summary>Finds the arguments the naming rule asks to name, or nothing when it reports no call.</summary>
	private static Naming? FindUnnamedArguments(SyntaxNodeAnalysisContext context, BaseArgumentListSyntax argumentList, StanzaOptions options)
	{
		// A count of zero makes the rule report nothing before the call is bound.
		if (options.NamedArgumentMinArguments == 0)
			return null;

		// A conditional directive among the arguments can shift which parameter an argument binds to in another view,
		// so a name written from this view could be wrong there.
		if (ArgumentBinding.HasConditionalDirectiveAmongArguments(argumentList))
			return null;

		if (argumentList.Arguments.Count < options.NamedArgumentMinArguments)
			return null;

		// A call whose arguments all carry a name has nothing to name, so no lambda around it is bound.
		if (!HasAnUnnamedArgument(argumentList))
			return null;

		if (FindPairing(context, argumentList) is not { } pairing || !CanNameEveryArgument(pairing))
			return null;

		// Before C# 7.2 a named argument cannot precede a positional one,
		// so only the leading run of arguments that carry their names keeps the shorthand, as RemoveRedundantArgumentName reads it.
		bool namesMayPrecedePositions = ArgumentBinding.AllowsNamesBeforePositions(context.Node.SyntaxTree);
		bool inLeadingRun = true;

		var naming = new Naming();

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
				naming.Add(argument, index, name);

			inLeadingRun &= argument.NameColon is null && keepsShorthand;
		}

		return KeepsTheBoundMethod(context, naming) ? naming : null;
	}

	/// <summary>Finds the literals to name, with the run after the first one before C# 7.2, or nothing.</summary>
	private static Naming? FindLiteralsToName(
		SyntaxNodeAnalysisContext context,
		BaseArgumentListSyntax argumentList,
		StanzaOptions options,
		IArgumentOperation?[] pairing
	)
	{
		// Before C# 7.2 a named argument cannot precede a positional one, so the first literal starts a run named to the end of the call.
		bool namesMayPrecedePositions = ArgumentBinding.AllowsNamesBeforePositions(context.Node.SyntaxTree);
		bool inRun = false;

		var naming = new Naming();

		for (int index = 0; index < pairing.Length; index++)
		{
			ArgumentSyntax argument = argumentList.Arguments[index];

			if (argument.NameColon is not null)
				continue;

			// An argument that fills a `params` array has no name of its own, so it stays as it is.
			string? name = pairing[index] is { Parameter: { IsParams: false } parameter } ? parameter.Name : null;
			bool isLiteral = name is not null && IsLiteralToName(argument.Expression, options);

			inRun |= isLiteral && !namesMayPrecedePositions;

			if (!isLiteral && !inRun)
				continue;

			// A name cannot be written for an argument of the run, and no fix could finish the call without it.
			if (name is null)
				return null;

			naming.Add(argument, index, name);
		}

		return KeepsTheBoundMethod(context, naming) ? naming : null;
	}

	/// <summary>Pairs the arguments with their parameters, or finds nothing for a call left as written.</summary>
	private static IArgumentOperation?[]? FindPairing(SyntaxNodeAnalysisContext context, BaseArgumentListSyntax argumentList)
	{
		// An expression tree may not contain a named argument (CS0853).
		if (ExpressionTrees.IsInside(context.SemanticModel, context.Node, context.CancellationToken))
			return null;

		return FindCallBoundInEveryView(context) is { } call ? ArgumentBinding.PairArgumentsWithBindings(argumentList, call.Arguments) : null;
	}

	/// <summary>Names can make another overload applicable, so the call has to bind as it does today.</summary>
	private static bool KeepsTheBoundMethod(SyntaxNodeAnalysisContext context, Naming naming)
	{
		return naming.Arguments.Count > 0
			&& ArgumentSpeculation.KeepsTheBoundMethod(
				semanticModel: context.SemanticModel,
				call: context.Node,
				arguments: naming.Arguments,
				namesThem: true,
				cancellationToken: context.CancellationToken
			);
	}

	private static void ReportNaming(
		SyntaxNodeAnalysisContext context,
		BaseArgumentListSyntax argumentList,
		DiagnosticDescriptor descriptor,
		Naming naming
	)
	{
		// One report covers the call, so one fix names every argument the speculation checked together.
		context.ReportDiagnostic(
			Diagnostic.Create(
				descriptor,
				argumentList.GetLocation(),
				properties: new FixOptions(ArgumentNames: naming.Names.ToImmutable()).ToProperties()
			)
		);
	}

	private static bool HasAnUnnamedLiteral(BaseArgumentListSyntax argumentList, StanzaOptions options)
	{
		foreach (ArgumentSyntax argument in argumentList.Arguments)
		{
			if (argument.NameColon is null && IsLiteralToName(argument.Expression, options))
				return true;
		}

		return false;
	}

	/// <summary>Whether the expression is a literal the setting picks to name.</summary>
	private static bool IsLiteralToName(ExpressionSyntax expression, StanzaOptions options)
	{
		// The `!` of `null!` and `default!` only quiets the nullable warning, so the argument says as little as the bare literal.
		if (expression is PostfixUnaryExpressionSyntax suppression && suppression.IsKind(SyntaxKind.SuppressNullableWarningExpression))
			expression = suppression.Operand;

		return expression.Kind() switch
		{
			SyntaxKind.TrueLiteralExpression or SyntaxKind.FalseLiteralExpression => true,

			SyntaxKind.NullLiteralExpression or SyntaxKind.DefaultLiteralExpression or SyntaxKind.DefaultExpression =>
				options.NamesNullAndDefaultLiterals,

			_ => false,
		};
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

		// A conditional directive among the arguments can shift which parameter an argument binds to in another view,
		// so a name this view finds redundant could be needed there.
		if (ArgumentBinding.HasConditionalDirectiveAmongArguments(argumentList))
			return;

		// A call that writes no name colon has no name to take away, so it skips the binding.
		if (!WritesAName(argumentList))
			return;

		if (FindCallBoundInEveryView(context) is not { } call)
			return;

		int implicitArgumentCount = ArgumentBinding.CountLeadingImplicitArguments(call.Arguments, argumentList);
		IArgumentOperation?[] pairing = ArgumentBinding.PairArgumentsWithBindings(argumentList, call.Arguments);

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

	/// <summary>Finds the call's binding, unless another view could declare its member differently.</summary>
	private static ArgumentBinding.BoundCall? FindCallBoundInEveryView(SyntaxNodeAnalysisContext context)
	{
		ArgumentBinding.BoundCall? call = ArgumentBinding.FindBoundCall(context.SemanticModel, context.Node, context.CancellationToken);
		return call is not null && !ArgumentDeclarations.IsUnderAConditional(call.Member, context.CancellationToken) ? call : null;
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

	/// <summary>The arguments a rule asks to name, each with its name, and with its place for the fix.</summary>
	private sealed class Naming
	{
		public List<(ArgumentSyntax Argument, string Name)> Arguments { get; } = [];
		public ImmutableArray<(int Index, string Name)>.Builder Names { get; } = ImmutableArray.CreateBuilder<(int Index, string Name)>();

		public void Add(ArgumentSyntax argument, int index, string name)
		{
			Arguments.Add((argument, name));
			Names.Add((index, name));
		}
	}
}
