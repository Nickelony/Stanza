using System;
using System.Globalization;
using System.Xml;

namespace Nickelony.Stanza.Markup.Core;

/// <summary>Why a document could not be laid out, and the 1-based line and column where the problem starts.</summary>
internal readonly record struct MarkupFailure(string Reason, int Line, int Column)
{
	public const int FirstLine = 1;
	public const int FirstColumn = 1;

	public static MarkupFailure From(XmlException exception) =>
		new(Describe(exception), Line: Math.Max(exception.LineNumber, FirstLine), Column: Math.Max(exception.LinePosition, FirstColumn));

	/// <summary>
	/// Cuts the localized line and position suffix, found by the exception's two numbers rather than by its words.
	/// </summary>
	public static string Describe(XmlException exception)
	{
		string message = exception.Message;

		if (exception.LineNumber <= 0)
			return message;

		int columnOffset = message.LastIndexOf(exception.LinePosition.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

		if (columnOffset <= 0)
			return message;

		int lineOffset = message.LastIndexOf(exception.LineNumber.ToString(CultureInfo.InvariantCulture), columnOffset - 1, StringComparison.Ordinal);

		if (lineOffset < 0)
			return message;

		// The word that names the line stands before its number.
		int end = lineOffset;

		while (end > 0 && message[end - 1] == ' ')
			end--;

		while (end > 0 && char.IsLetter(message[end - 1]))
			end--;

		while (end > 0 && message[end - 1] == ' ')
			end--;

		return message.Substring(0, end);
	}
}
