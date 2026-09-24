using System;
using System.Collections.Immutable;
using System.Linq;

namespace Nickelony.Stanza.Analyzers.CodeFixes;

/// <summary>Holds the rows of one provider and finds the row of a diagnostic id.</summary>
internal sealed class FixTable
{
	private readonly ImmutableDictionary<string, FixRow> _rowsById;

	public FixTable(ImmutableArray<FixRow> rows)
	{
		_rowsById = rows.ToImmutableDictionary(static row => row.DiagnosticId, StringComparer.Ordinal);
		Ids = [.. rows.Select(static row => row.DiagnosticId)];
	}

	public ImmutableArray<string> Ids { get; }

	/// <summary>
	/// Returns <see langword="false"/> for an ID with no row, as a fix-all can carry another provider's diagnostics.
	/// </summary>
	public bool TryGet(string diagnosticId, out FixRow row) => _rowsById.TryGetValue(diagnosticId, out row);
}
