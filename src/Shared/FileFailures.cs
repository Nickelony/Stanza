using System;
using System.IO;

namespace Nickelony.Stanza.Shared;

/// <summary>Names the exceptions a file operation raises for a file or path it cannot use.</summary>
/// <remarks>
/// The first set is the base, two add one exception each, and the last joins them: pick the narrowest that fits.
/// </remarks>
internal static class FileFailures
{
	/// <summary>The file cannot be read, listed or written: an I/O failure, or access the platform refuses.</summary>
	public static bool IsFileFailure(Exception exception) => exception is IOException or UnauthorizedAccessException;

	/// <summary>A file failure, or a path the platform refuses, as one with a character it never allows.</summary>
	public static bool IsFileOrPathFailure(Exception exception) => IsFileFailure(exception) || exception is ArgumentException;

	/// <summary>A file failure, or a name or an operation the platform does not support.</summary>
	public static bool IsFileOrUnsupportedFailure(Exception exception) => IsFileFailure(exception) || exception is NotSupportedException;

	/// <summary>Any failure of reading or writing a file: a refused path or an unsupported one too.</summary>
	public static bool IsFileOperationFailure(Exception exception) => IsFileOrPathFailure(exception) || exception is NotSupportedException;
}
