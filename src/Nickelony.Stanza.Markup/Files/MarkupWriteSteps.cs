using System;

namespace Nickelony.Stanza.Markup.Files;

/// <summary>The steps of a write that read or change the file system, so a test can fail or race each.</summary>
internal sealed record MarkupWriteSteps(Action<string, string> Swap, Func<string, MarkupFileStamp, bool> HasChanged);
