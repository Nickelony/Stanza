using System.Diagnostics.CodeAnalysis;

namespace Nickelony.Stanza.Shared;

/// <summary>What a tool does with each argument that <see cref="ToolArguments"/> reads.</summary>
internal interface IToolArgumentHandler
{
	/// <summary>Whether the tool takes the argument, and otherwise the error it raises.</summary>
	bool TryAcceptPositional(string argument, [NotNullWhen(false)] out string? error);

	/// <summary>Whether the tool knows the flag, which it then sets.</summary>
	bool TryAcceptFlag(string option);

	/// <summary>Whether the tool takes the value of the option, and otherwise the error it raises.</summary>
	bool TryAcceptValue(string option, string value, [NotNullWhen(false)] out string? error);
}
