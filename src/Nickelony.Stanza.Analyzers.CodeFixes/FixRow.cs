using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.CodeFixes;

/// <summary>Creates the changes that fix a diagnostic, or none when the fix is not offered.</summary>
internal delegate Task<ImmutableArray<TextChange>> FixChanges(FixDocument document, Diagnostic diagnostic, CancellationToken cancellationToken);

/// <summary>Ties a diagnostic ID, also its action's equivalence key, to the action's title and its fix.</summary>
/// <remarks>
/// A row whose single fix writes more than its share of a Fix All names that fix apart.
/// </remarks>
internal readonly record struct FixRow(string DiagnosticId, string Title, FixChanges Create, FixChanges? CreateSingle = null)
{
	public static FixRow For(DiagnosticDescriptor descriptor, string title, FixChanges create, FixChanges? createSingle = null) =>
		new(DiagnosticId: descriptor.Id, Title: title, Create: create, CreateSingle: createSingle);
}
