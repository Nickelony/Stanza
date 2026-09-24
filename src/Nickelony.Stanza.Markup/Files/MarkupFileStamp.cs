using System;

namespace Nickelony.Stanza.Markup.Files;

/// <summary>What a file looked like when it was read; a write refuses a file that differs from it.</summary>
internal readonly record struct MarkupFileStamp(long Length, DateTime LastWriteUtc);
