using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.CodeFixes;

/// <summary>Creates the changes that fix a diagnostic, or none when the fix is not offered.</summary>
internal delegate Task<ImmutableArray<TextChange>> FixChanges(FixDocument document, Diagnostic diagnostic, CancellationToken cancellationToken);

/// <summary>Ties a diagnostic ID, also its action's equivalence key, to the action's title and its fix.</summary>
internal readonly record struct FixRow(string DiagnosticId, string Title, FixChanges Create)
{
	public static FixRow For(DiagnosticDescriptor descriptor, string title, FixChanges create) => new(descriptor.Id, title, create);
}
