using System.Reflection;

namespace Nickelony.Stanza.Tests.Shared;

/// <summary>Names a dynamic-data row, so the test projects name failing rows alike.</summary>
internal static class RowNames
{
	public static string GetRowName(MethodInfo method, object[] data) =>
		$"{method.Name}: {data[0]}";
}
