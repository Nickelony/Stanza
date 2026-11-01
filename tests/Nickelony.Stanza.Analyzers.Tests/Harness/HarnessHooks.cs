using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Analyzers.Tests.Harness;

/// <summary>Starts and ends the record and the time budget of every test.</summary>
[TestClass]
public static class HarnessHooks
{
	[GlobalTestInitialize]
	public static void RecordTheRunningTest(TestContext context)
	{
		HarnessRecord.Begin(GetTest(context));
		HarnessBudget.Begin(context.CancellationToken);
	}

	[GlobalTestCleanup]
	public static void EndTheRunningTest(TestContext context)
	{
		HarnessBudget.End();
		HarnessRecord.End(GetTest(context), context.CurrentTestOutcome);
	}

	private static RecordedTest GetTest(TestContext context)
	{
		string className = context.FullyQualifiedTestClassName ?? string.Empty;

		return new RecordedTest(
			Class: className[(className.LastIndexOf('.') + 1)..],
			Method: context.TestName ?? string.Empty
		);
	}
}
