using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Expressions;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class UsePatternForNullCheckAnalyzer() : StanzaRuleAnalyzer(StanzaRules.UsePatternForNullCheck)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		var comparison = (BinaryExpressionSyntax)context.Node;

		if (NullCheckEdits.FindComparedOperand(comparison) is not { } operand)
			return;

		// The constant pattern came with C# 7, and `not` with C# 9.
		LanguageVersion needed = comparison.IsKind(SyntaxKind.NotEqualsExpression) ? LanguageVersion.CSharp9 : LanguageVersion.CSharp7;

		if (!StanzaSyntax.SupportsLanguage(comparison.SyntaxTree, needed) || NullCheckEdits.CreatePattern(comparison).IsEmpty)
			return;

		var options = StanzaOptions.From(context.Options, comparison.SyntaxTree);

		if (!StanzaRuleState.IsTheOwningHalf(scope: context, options, rule: Rule, position: comparison.SpanStart))
			return;

		SemanticModel semanticModel = context.SemanticModel;

		if (!NullChecks.IsPlainNullCheck(semanticModel, operand, context.CancellationToken))
			return;

		ITypeSymbol operandType = semanticModel.GetTypeInfo(operand, context.CancellationToken).Type!;

		// A pointer takes the null constant pattern from C# 8, and so does a type parameter that C# 7 can refuse (CS8511).
		if (
			operandType.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer or TypeKind.TypeParameter
			&& !StanzaSyntax.SupportsLanguage(comparison.SyntaxTree, LanguageVersion.CSharp8)
		)
		{
			return;
		}

		// An operator the declarations do not show, such as an extension operator, still decides what `==` calls.
		ISymbol? called = semanticModel.GetSymbolInfo(comparison, context.CancellationToken).Symbol;

		if (!NullChecks.IsNullTestingOperator(called, operandType) || ExpressionTrees.IsInside(semanticModel, comparison, context.CancellationToken))
			return;

		context.ReportDiagnostic(Diagnostic.Create(Descriptor, comparison.GetLocation()));
	}
}
