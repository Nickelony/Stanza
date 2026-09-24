using System.Threading;
using Nickelony.Stanza.Markup.Configuration;
using Nickelony.Stanza.Markup.Notices;
using Nickelony.Stanza.Markup.Paths;

namespace Nickelony.Stanza.Markup.Collection;

internal sealed class MarkupCollectionContext(
	MarkupPaths paths,
	MarkupProfile? forced,
	MarkupConfiguration configuration,
	MarkupExcludeSet excludes,
	MarkupNotices notices,
	CancellationToken cancellationToken = default
)
{
	public MarkupPaths Paths { get; } = paths;
	public MarkupProfile? Forced { get; } = forced;
	public MarkupConfiguration Configuration { get; } = configuration;
	public MarkupExcludeSet Excludes { get; } = excludes;
	public MarkupNotices Notices { get; } = notices;
	public CancellationToken CancellationToken { get; } = cancellationToken;
}
