using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Nickelony.Stanza.Shared;

/// <summary>Reads the arguments the way both tools read them, and hands each one to the tool.</summary>
internal static class ToolArguments
{
	/// <summary>Reads each argument in turn, and stops at the first error the reading or the tool raises.</summary>
	/// <returns>Whether every argument was read; otherwise, <paramref name="error"/> holds the first error.</returns>
	public static bool TryRead(
		string[] args,
		IReadOnlyCollection<string> valueOptions,
		IToolArgumentHandler handler,
		[NotNullWhen(false)] out string? error
	)
	{
		bool optionsEnded = false;
		int index = 0;

		while (index < args.Length)
		{
			string argument = args[index];
			index++;

			if (!optionsEnded && argument == "--")
			{
				optionsEnded = true;
				continue;
			}

			// A bare "-" is refused: resolved against the working directory, it names a file called "-" there.
			if (!optionsEnded && argument == "-")
			{
				error = "unknown option '-'.";
				return false;
			}

			if (optionsEnded || argument.Length < 2 || argument[0] != '-')
			{
				if (!handler.TryAcceptPositional(argument, out error))
					return false;

				continue;
			}

			if (!TryReadOption(args, ref index, argument, valueOptions, handler, out error))
				return false;
		}

		error = null;
		return true;
	}

	private static bool TryReadOption(
		string[] args,
		ref int index,
		string argument,
		IReadOnlyCollection<string> valueOptions,
		IToolArgumentHandler handler,
		[NotNullWhen(false)] out string? error
	)
	{
		// "--profile=xml" equals "--profile xml" for an option that takes a value.
		// A flag written this way keeps its text and stays unknown.
		string option = argument;
		string? value = null;
		int equals = argument.IndexOf('=');

		if (equals > 0 && valueOptions.Contains(argument[..equals]))
		{
			option = argument[..equals];
			value = argument[(equals + 1)..];
		}

		if (!valueOptions.Contains(option))
		{
			bool known = handler.TryAcceptFlag(option);
			error = known ? null : $"unknown option '{argument}'.";

			return known;
		}

		// An option is never taken as a value, so "--profile --strict" is a missing value.
		if (value is null && index < args.Length && !args[index].StartsWith('-'))
		{
			value = args[index];
			index++;
		}

		if (string.IsNullOrEmpty(value))
		{
			error = $"the {option} option needs a value.";
			return false;
		}

		return handler.TryAcceptValue(option, value, out error);
	}
}
