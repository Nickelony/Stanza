using System.Collections.Generic;

namespace Nickelony.Stanza.Markup.Configuration;

internal sealed record EditorConfigSection(string Directory, string Glob, IReadOnlyDictionary<string, string> Values, int? HeaderLine = null);
