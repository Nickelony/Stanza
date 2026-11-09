using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Format.Tests;

/// <summary>Keeps the builds of a tool run from leaving a node or a server behind.</summary>
[TestClass]
public static class ChildBuildEnvironment
{
	// A node or a VBCSCompiler that outlives its test holds the temp output the test deletes.
	// The tool starts dotnet format and msbuild itself, so they inherit these from this process.
	[AssemblyInitialize]
	public static void DisableBuildServers(TestContext _)
	{
		foreach ((string name, string value) in ChildBuild.ServerSwitches)
			Environment.SetEnvironmentVariable(name, value);
	}
}
