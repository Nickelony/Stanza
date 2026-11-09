using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Expressions;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class UseEqualityOperatorForNullCheckAnalyzer() : StanzaRuleAnalyzer(StanzaRules.UseEqualityOperatorForNullCheck)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		var check = (IsPatternExpressionSyntax)context.Node;

		if (NullCheckEdits.FindNullTest(check.Pattern) is null || NullCheckEdits.CreateEqualityCheck(check).IsEmpty)
			return;

		var options = StanzaOptions.From(context.Options, check.SyntaxTree);

		if (!StanzaRuleState.IsTheOwningHalf(scope: context, options, rule: Rule, position: check.SpanStart))
			return;

		SemanticModel semanticModel = context.SemanticModel;
		ExpressionSyntax operand = check.Expression;

		if (!NullChecks.IsPlainNullCheck(semanticModel, operand, context.CancellationToken))
			return;

		// The comparison does not exist yet, so it is bound where the pattern stands to learn what `==` would call there.
		BinaryExpressionSyntax comparison = SyntaxFactory.BinaryExpression(
			SyntaxKind.EqualsExpression,
			SyntaxFactory.ParenthesizedExpression(operand.WithoutTrivia()),
			SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)
		);

		ISymbol? called = semanticModel.GetSpeculativeSymbolInfo(check.SpanStart, comparison, SpeculativeBindingOption.BindAsExpression).Symbol;

		if (!NullChecks.IsNullTestingOperator(called, semanticModel.GetTypeInfo(operand, context.CancellationToken).Type!))
			return;

		context.ReportDiagnostic(Diagnostic.Create(Descriptor, check.GetLocation()));
	}
}
