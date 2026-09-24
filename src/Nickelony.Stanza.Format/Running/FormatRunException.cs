using System;

namespace Nickelony.Stanza.Format.Running;

/// <summary>A failure that ends a run, such as a path that names no solution, reported by its message alone.</summary>
internal sealed class FormatRunException(string message, Exception? innerException = null) : Exception(message, innerException);
