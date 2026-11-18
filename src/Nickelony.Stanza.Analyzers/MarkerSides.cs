using System;

namespace Nickelony.Stanza.Analyzers;

/// <summary>The sides of a directive marker that need a blank line.</summary>
[Flags]
internal enum MarkerSides
{
	None = 0,
	Above = 1,
	Below = 2,
	Both = Above | Below,
}
