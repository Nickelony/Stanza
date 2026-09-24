using System.Collections.Immutable;
using Nickelony.Stanza.Markup.Core.Formatting;

namespace Nickelony.Stanza.Markup.Cli;

/// <summary>What a run did with one file, with the edits of a file it changed.</summary>
internal sealed record MarkupFileResult(MarkupFileOutcome Outcome, string DisplayPath, ImmutableArray<MarkupEdit> Edits);
