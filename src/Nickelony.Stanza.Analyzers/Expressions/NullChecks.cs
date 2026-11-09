using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers.Expressions;

/// <summary>Decides whether <c>== null</c> and <c>is null</c> mean the same for an operand.</summary>
/// <remarks>
/// A pattern calls no operator, so the two agree only where <c>==</c> calls none or one that only tests for it.
/// </remarks>
internal static class NullChecks
{
	/// <summary>Whether <c>==</c> and the pattern test the operand alike, in every view.</summary>
	public static bool IsPlainNullCheck(SemanticModel semanticModel, ExpressionSyntax operand, CancellationToken cancellationToken)
	{
		// A constant folds into another constant, which a pattern does not, so flow analysis would read the code another way.
		if (semanticModel.GetConstantValue(operand, cancellationToken).HasValue)
			return false;

		if (semanticModel.GetTypeInfo(operand, cancellationToken).Type is not { } type || !IsPlainType(type))
			return false;

		return !HasEqualityInAnotherView(ReadInspectedTypes(type), cancellationToken);
	}

	/// <summary>Whether the operator <c>==</c> binds to only tests the operand, as a built-in one does.</summary>
	public static bool IsNullTestingOperator(ISymbol? symbol, ITypeSymbol operandType)
	{
		if (symbol is not IMethodSymbol { MethodKind: MethodKind.UserDefinedOperator } method)
			return true;

		if (IsNullTestingType(method.ContainingType) || method.IsImplicitlyDeclared)
			return true;

		// A lifted operator answers a null operand itself and never calls the operator of the underlying type.
		return ReadUnderlyingType(operandType) is { } underlying && SymbolEqualityComparer.Default.Equals(method.ContainingType, underlying);
	}

	private static bool IsPlainType(ITypeSymbol type)
	{
		if (type.TypeKind is TypeKind.Error or TypeKind.Dynamic)
			return false;

		// A pointer compares by address alone, and Roslyn counts it as a value type.
		if (type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer)
			return true;

		if (ReadUnderlyingType(type) is { } underlying)
			return !HasOwnEquality(underlying, liftsOnly: true);

		// A value type that cannot hold null makes `==` lift and answer false, while `is null` does not compile.
		return !type.IsValueType && !HasOwnEquality(type, liftsOnly: false);
	}

	private static ITypeSymbol? ReadUnderlyingType(ITypeSymbol type) =>
		type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable ? nullable.TypeArguments[0] : null;

	/// <summary>Whether the type or one it takes operators from declares an equality of its own.</summary>
	/// <remarks>
	/// Under a nullable type, an operator that takes the underlying type twice lifts, and lifting answers alone.
	/// </remarks>
	private static bool HasOwnEquality(ITypeSymbol type, bool liftsOnly)
	{
		foreach (ITypeSymbol inspected in ReadInspectedTypes(type))
		{
			if (inspected is INamedTypeSymbol { IsRecord: true } record && HasOwnRecordEquality(record))
				return true;

			if (IsNullTestingType(inspected))
				continue;

			foreach (IMethodSymbol method in inspected.GetMembers().OfType<IMethodSymbol>())
			{
				if (!IsEqualityOperator(method) || method.IsImplicitlyDeclared)
					continue;

				bool lifts = method.Parameters.All(parameter => SymbolEqualityComparer.Default.Equals(parameter.Type, type));

				if (!liftsOnly || !lifts || type is ITypeParameterSymbol)
					return true;
			}
		}

		return false;
	}

	/// <summary>Reads the type with its base types, or a type parameter with its constraints.</summary>
	private static List<ITypeSymbol> ReadInspectedTypes(ITypeSymbol type)
	{
		List<ITypeSymbol> types = [];
		var pending = new Stack<ITypeSymbol>([ReadUnderlyingType(type) ?? type]);

		while (pending.Count > 0)
		{
			ITypeSymbol next = pending.Pop();

			if (types.Contains(next, SymbolEqualityComparer.Default))
				continue;

			types.Add(next);

			if (next is ITypeParameterSymbol parameter)
			{
				foreach (ITypeSymbol constraint in parameter.ConstraintTypes)
					pending.Push(constraint);

				continue;
			}

			if (next.BaseType is { } baseType)
				pending.Push(baseType);

			// An interface lends a static abstract or virtual operator to the type parameter it constrains.
			if (next.TypeKind == TypeKind.Interface)
			{
				foreach (INamedTypeSymbol inherited in next.Interfaces)
					pending.Push(inherited);
			}
		}

		return types;
	}

	/// <summary>Whether a record declares the <c>Equals</c> that its generated operators call.</summary>
	private static bool HasOwnRecordEquality(INamedTypeSymbol record)
	{
		return record
			.GetMembers(nameof(Equals))
			.OfType<IMethodSymbol>()
			.Any(method =>
				!method.IsImplicitlyDeclared
				&& method.Parameters.Length == 1
				&& SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, record)
			);
	}

	/// <summary>Whether the operators of the type answer <see langword="null"/> by reference alone.</summary>
	private static bool IsNullTestingType(ITypeSymbol type) =>
		type.SpecialType is SpecialType.System_String or SpecialType.System_Delegate or SpecialType.System_MulticastDelegate;

	private static bool IsEqualityOperator(IMethodSymbol method)
	{
		return method.MethodKind == MethodKind.UserDefinedOperator
			&& method.Name is WellKnownMemberNames.EqualityOperatorName or WellKnownMemberNames.InequalityOperatorName;
	}

	/// <summary>Whether a declaration of the types holds an operator in text that another view compiles.</summary>
	/// <remarks>
	/// The generated operators of a record call its <c>Equals</c>, so an <c>Equals</c> there counts as well.
	/// </remarks>
	private static bool HasEqualityInAnotherView(List<ITypeSymbol> types, CancellationToken cancellationToken)
	{
		foreach (ITypeSymbol type in types)
		{
			string[] words = type.IsRecord ? ["operator", nameof(Equals)] : ["operator"];

			foreach (SyntaxReference reference in type.DeclaringSyntaxReferences)
			{
				SyntaxNode declaration = reference.GetSyntax(cancellationToken);

				if (declaration.ContainsDirectives && HoldsDisabledWord(declaration, words))
					return true;
			}
		}

		return false;
	}

	/// <summary>Whether the text a directive in the declaration disables holds a word.</summary>
	/// <remarks>
	/// It reads the directives alone, so the cost follows their count, not the size of the declaration.
	/// </remarks>
	private static bool HoldsDisabledWord(SyntaxNode declaration, string[] words)
	{
		for (DirectiveTriviaSyntax? directive = declaration.GetFirstDirective(); directive is not null; directive = directive.GetNextDirective())
		{
			if (directive.SpanStart > declaration.Span.End)
				break;

			SyntaxTriviaList trivia = directive.ParentTrivia.Token.LeadingTrivia;
			int index = trivia.IndexOf(directive.ParentTrivia);

			if (index < 0 || index + 1 >= trivia.Count || !trivia[index + 1].IsKind(SyntaxKind.DisabledTextTrivia))
				continue;

			string text = trivia[index + 1].ToString();

			if (words.Any(word => text.IndexOf(word, StringComparison.Ordinal) >= 0))
				return true;
		}

		return false;
	}
}
