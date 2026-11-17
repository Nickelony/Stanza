namespace Nickelony.Stanza.Analyzers.Tests.Harness;

/// <summary>What a test read of a run's result, and whether the fixes changed its source.</summary>
/// <remarks>
/// The record keeps this in place of the result, so no run's diagnostics or texts outlive it.
/// </remarks>
internal sealed class ResultReads
{
	private bool _tracked;

	/// <summary>Whether the test read the result after the harness handed it over.</summary>
	public bool WasRead { get; private set; }

	/// <summary>Whether the test read the fixed text after the harness handed it over.</summary>
	public bool FixedTextWasRead { get; private set; }

	/// <summary>Whether the fixes changed the source the run began with.</summary>
	public bool FixedTheSource { get; private set; }

	public void Begin(bool fixedTheSource)
	{
		_tracked = true;
		FixedTheSource = fixedTheSource;
	}

	public void NoteDiagnosticsRead() => WasRead |= _tracked;

	public void NoteFixedTextRead()
	{
		WasRead |= _tracked;
		FixedTextWasRead |= _tracked;
	}
}
