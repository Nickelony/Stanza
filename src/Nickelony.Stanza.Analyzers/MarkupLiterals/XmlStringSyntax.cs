using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Nickelony.Stanza.Analyzers.MarkupLiterals;

/// <summary>Reads the <c>[StringSyntax("Xml")]</c> on the field, property or parameter a literal goes to.</summary>
/// <remarks>
/// The attribute is matched by its name, since the analyzer targets a framework that does not declare it.
/// </remarks>
internal static class XmlStringSyntax
{
	private const string AttributeName = "StringSyntaxAttribute";
	private const string AttributeNamespace = "System.Diagnostics.CodeAnalysis";
	private const string Xml = "xml";

	public static bool Covers(LiteralExpressionSyntax literal, SemanticModel semanticModel, CancellationToken cancellationToken)
	{
		if (semanticModel.GetOperation(literal, cancellationToken) is not { } operation)
			return false;

		return FindTargets(operation).Any(target => CarriesXml(target, cancellationToken));
	}

	/// <summary>Finds the fields, properties and parameters the literal initializes or is passed to.</summary>
	private static ImmutableArray<ISymbol> FindTargets(IOperation operation)
	{
		IOperation value = operation;

		// An implicit conversion, such as to object, stands between the literal and its target.
		while (value.Parent is IConversionOperation { IsImplicit: true } conversion)
			value = conversion;

		// A params argument reaches its parameter through the array or collection the compiler builds for it.
		if (value.Parent is IArrayInitializerOperation { Parent: IArrayCreationOperation { IsImplicit: true } creation })
			value = creation;
		else if (value.Parent is ICollectionExpressionOperation { IsImplicit: true, Parent: IArgumentOperation } collection)
			value = collection;

		return value.Parent switch
		{
			IArgumentOperation { Parameter: { } parameter } => [parameter],
			IFieldInitializerOperation initializer => [.. initializer.InitializedFields],
			IPropertyInitializerOperation initializer => [.. initializer.InitializedProperties],
			ISimpleAssignmentOperation assignment when assignment.Value == value => FindAssignedMembers(assignment.Target),
			_ => [],
		};
	}

	private static ImmutableArray<ISymbol> FindAssignedMembers(IOperation target)
	{
		return target switch
		{
			IFieldReferenceOperation field => [field.Field],
			IPropertyReferenceOperation property => [property.Property],
			_ => [],
		};
	}

	/// <summary>Whether the target, or the record member declared with it, carries the attribute for XML.</summary>
	private static bool CarriesXml(ISymbol target, CancellationToken cancellationToken)
	{
		if (target.GetAttributes().Any(IsXmlSyntax))
			return true;

		return FindRecordTwin(target, cancellationToken) is { } twin && twin.GetAttributes().Any(IsXmlSyntax);
	}

	/// <summary>Finds the property a positional record parameter declares, or that property's parameter.</summary>
	/// <remarks>
	/// An attribute on such a parameter marks the parameter alone, and one with <c>property:</c> the property alone.
	/// </remarks>
	private static ISymbol? FindRecordTwin(ISymbol target, CancellationToken cancellationToken)
	{
		ImmutableArray<SyntaxReference> references = target.DeclaringSyntaxReferences;

		if (references.Length != 1 || references[0].GetSyntax(cancellationToken) is not ParameterSyntax)
			return null;

		SyntaxReference reference = references[0];

		ISymbol[] candidates = target switch
		{
			IPropertySymbol property => [.. property.ContainingType.InstanceConstructors.SelectMany(constructor => constructor.Parameters)],
			IParameterSymbol parameter when parameter.ContainingType is { } type => [.. type.GetMembers(parameter.Name).OfType<IPropertySymbol>()],
			_ => [],
		};

		return candidates.FirstOrDefault(candidate =>
			!SymbolEqualityComparer.Default.Equals(candidate, target)
			&& candidate.DeclaringSyntaxReferences.Any(other => other.SyntaxTree == reference.SyntaxTree && other.Span == reference.Span)
		);
	}

	/// <summary>Whether the attribute is <c>StringSyntax</c> and names XML as its syntax, in any case.</summary>
	private static bool IsXmlSyntax(AttributeData attribute)
	{
		return attribute.AttributeClass is { Name: AttributeName } type
			&& type.ContainingNamespace.ToDisplayString() == AttributeNamespace
			&& attribute.ConstructorArguments.Length > 0
			&& attribute.ConstructorArguments[0].Value is string syntax
			&& string.Equals(syntax, Xml, StringComparison.OrdinalIgnoreCase);
	}
}
