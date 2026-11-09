using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Analyzers.Tests.Harness;

/// <summary>An analyzer crash, which a run that tolerates an unsettled result reports.</summary>
internal sealed class AnalyzerCrashException(string message) : AssertFailedException(message);
