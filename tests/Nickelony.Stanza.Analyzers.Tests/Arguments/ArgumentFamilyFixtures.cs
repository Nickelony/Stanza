using System.Collections.Generic;
using System.Globalization;
using Microsoft.CodeAnalysis;

namespace Nickelony.Stanza.Analyzers.Tests.Arguments;

internal static class ArgumentFamilyFixtures
{
	internal static IReadOnlyDictionary<string, ReportDiagnostic> BothRules { get; } =
		TestRuleSets.Only(
			DiagnosticDescriptors.NameArgumentsOfCall,
			DiagnosticDescriptors.RemoveRedundantArgumentName
		);

	internal static Dictionary<string, string> Config(
		int? minArguments = null,
		string? matchingName = null,
		string? nameCase = null,
		string? literals = null
	)
	{
		var config = new Dictionary<string, string>();

		if (minArguments is not null)
		{
			config["stanza_named_argument_min_arguments"] = minArguments.Value.ToString(
				CultureInfo.InvariantCulture
			);
		}

		if (matchingName is not null)
			config["stanza_named_argument_match"] = matchingName;

		if (nameCase is not null)
			config["stanza_named_argument_case"] = nameCase;

		if (literals is not null)
			config["stanza_named_argument_literals"] = literals;

		return config;
	}
}
