using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class DiagnosticIdTests
{
	// The codes only the tool reports, so no descriptor of the analyzer backs them.
	private static readonly string[] s_toolOnlyIds = ["ZA0100", "ZA0106"];

	[TestMethod]
	public void TheSharedMarkupDeclaresNoIdOfItsOwn()
	{
		HashSet<string> descriptors = [.. TestRuleSets.Declared.Select(rule => rule.Descriptor.Id)];

		List<string> orphans =
		[
			.. MarkupIdConstants()
				.Where(id => !descriptors.Contains(id) && !s_toolOnlyIds.Contains(id)),
		];

		Assert.IsEmpty(
			orphans,

			"The IDs below are declared by the shared markup diagnostics but name no rule of the analyzer and no code "
				+ "of the tool, so the products would report under different texts:"
				+ Environment.NewLine
				+ string.Join(Environment.NewLine, orphans)
		);
	}

	private static IEnumerable<string> MarkupIdConstants()
	{
		return typeof(MarkupDiagnostics)
			.GetFields(BindingFlags.Public | BindingFlags.Static)
			.Where(field =>
				field.FieldType == typeof(string)
				&& field.Name.EndsWith("Id", StringComparison.Ordinal)
			)
			.Select(field => (string)field.GetValue(null)!);
	}
}
