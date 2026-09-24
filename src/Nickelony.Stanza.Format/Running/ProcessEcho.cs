using System.IO;

namespace Nickelony.Stanza.Format.Running;

/// <summary>The writers a child's output stream and error stream are copied to.</summary>
internal sealed record ProcessEcho(TextWriter Output, TextWriter Error);
