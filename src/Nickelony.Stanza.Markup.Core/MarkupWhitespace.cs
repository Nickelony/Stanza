namespace Nickelony.Stanza.Markup.Core;

internal static class MarkupWhitespace
{
	/// <summary>
	/// Narrower than .NET's on purpose: a character such as U+00A0, U+0085 or U+2028 is character data, not layout.
	/// </summary>
	public static bool IsXmlWhitespace(char character) => character is ' ' or '\t' or '\r' or '\n';

	public static bool IsBlank(string text)
	{
		foreach (char character in text)
		{
			if (!IsXmlWhitespace(character))
				return false;
		}

		return true;
	}
}
