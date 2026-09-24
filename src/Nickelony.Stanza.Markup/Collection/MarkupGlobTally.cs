using Nickelony.Stanza.Markup.Notices;

namespace Nickelony.Stanza.Markup.Collection;

/// <summary>Counts what one glob matched, so a glob that inspects nothing is reported with the reason.</summary>
internal sealed class MarkupGlobTally
{
	public int Matched { get; set; }
	public int Excluded { get; set; }
	public int Unclaimed { get; set; }
	public int Disabled { get; set; }

	/// <summary>Reports a glob that selected nothing; one that matched no file at all is an error.</summary>
	public void Report(string argument, int collected, MarkupNotices notices)
	{
		// A glob that matches no file fails the run, as a path that does not exist does.
		if (Matched == 0)
		{
			notices.AddError($"'{argument}' does not match any file.");
		}
		else if (collected == 0 && Excluded == Matched)
		{
			notices.AddWarning($"'{argument}' matched {Matched} file(s), but all of them are excluded, so nothing was inspected.");
		}
		else if (collected == 0 && (Unclaimed > 0 || Disabled > 0))
		{
			// One clause per reason a match was left out.
			// A link match is reported on its own and counted in neither clause.
			int considered = Matched - Excluded;

			string disabledClause = $"{Disabled} of them {(Disabled == 1 ? "is disabled in its" : "are disabled in their")} .editorconfig";
			string reason = Unclaimed > 0 ? "none of them is claimed by a profile or by their .editorconfig" : disabledClause;

			if (Unclaimed > 0 && Disabled > 0)
				reason = $"{Unclaimed} of them {(Unclaimed == 1 ? "is" : "are")} not claimed by a profile, and {disabledClause}";

			notices.AddWarning($"'{argument}' matched {considered} file(s), but {reason}, so nothing was inspected.");
		}
	}
}
