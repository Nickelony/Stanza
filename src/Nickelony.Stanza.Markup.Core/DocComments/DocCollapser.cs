using System.Text;

namespace Nickelony.Stanza.Markup.Core.DocComments;

/// <summary>
/// Collapses whitespace and line breaks to single spaces, except spaces and tabs inside quoted attribute values.
/// </summary>
internal sealed class DocCollapser
{
	private readonly StringBuilder _builder = new();

	private bool _insideTag;
	private bool _pendingSpace;

	private char _quote;
	private char _lastSignificant;

	public static string Collapse(string text, int start, int end)
	{
		DocCollapser collapser = new();
		collapser.Append(text, start, end);
		return collapser.ToString();
	}

	public static string Collapse(string text) => Collapse(text, 0, text.Length);

	public void Append(string text) => Append(text, 0, text.Length);

	public void Append(string text, int start, int end)
	{
		for (int offset = start; offset < end; offset++)
		{
			char character = text[offset];

			if (character is '\r' or '\n')
			{
				_pendingSpace = _builder.Length > 0;
				continue;
			}

			if (MarkupWhitespace.IsXmlWhitespace(character))
			{
				if (_quote != '\0')
				{
					_builder.Append(character);
					continue;
				}

				_pendingSpace = _builder.Length > 0;
				continue;
			}

			// A tag broken before its closing bracket, as CSharpier breaks a long start tag, closes with no space before the bracket.
			if (_pendingSpace)
			{
				if (!(_insideTag && _quote == '\0' && character == '>'))
					_builder.Append(' ');

				_pendingSpace = false;
			}

			if (_quote != '\0')
			{
				if (character == _quote)
					_quote = '\0';
			}
			else if (_insideTag && character is '"' or '\'' && _lastSignificant == '=')
			{
				_quote = character;
			}
			else if (character == '<')
			{
				_insideTag = true;
			}
			else if (character == '>')
			{
				_insideTag = false;
			}

			_lastSignificant = character;
			_builder.Append(character);
		}
	}

	public override string ToString() => _builder.ToString();
}
