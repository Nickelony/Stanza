using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>A guard fails with a heading, a line for each offender and a closing note.</summary>
internal static class GuardAssert
{
	internal static void None(
		IReadOnlyCollection<string> offenders,
		string heading,
		string? closing = null
	)
	{
		string list = string.Join(Environment.NewLine, offenders);
		string note = closing is null ? string.Empty : Environment.NewLine + closing;

		Assert.IsEmpty(offenders, $"{heading}:{Environment.NewLine}{list}{note}");
	}
}
