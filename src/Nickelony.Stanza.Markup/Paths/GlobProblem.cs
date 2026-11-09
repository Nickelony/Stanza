namespace Nickelony.Stanza.Markup.Paths;

/// <summary>Why a glob cannot select files: the platform cannot resolve it, or the dialect refuses it.</summary>
internal readonly record struct GlobProblem(bool IsRefusal, string Detail)
{
	/// <summary>Writes the problem after the name of the glob, which each kind spells its own way.</summary>
	public string Describe(string unresolvedName, string refusedName)
	{
		return IsRefusal
			? $"{refusedName} is refused ({Detail}); it matches nothing."
			: $"{unresolvedName} could not be resolved to a path: {Detail}";
	}
}
