using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Nickelony.Stanza.Shared;

/// <summary>Reads the arguments as both tools do, and hands each one but help and version to the tool.</summary>
internal abstract class ToolArgumentReader
{
	/// <summary>Whether <c>-h</c> or <c>--help</c> was read.</summary>
	public bool Help { get; private set; }

	/// <summary>Whether <c>--version</c> was read.</summary>
	public bool Version { get; private set; }

	/// <summary>The options that take a value.</summary>
	protected abstract IReadOnlyCollection<string> ValueOptions { get; }

	/// <summary>The value options a line may repeat; every other one is refused a second time.</summary>
	protected abstract IReadOnlyCollection<string> RepeatableOptions { get; }

	/// <summary>Reads each argument in turn, and stops at the first error the reading or the tool raises.</summary>
	/// <returns>Whether every argument was read; otherwise, <paramref name="error"/> holds the first error.</returns>
	public bool TryReadAll(string[] args, [NotNullWhen(false)] out string? error)
	{
		// A value option that RepeatableOptions does not name is an error when given twice.
		HashSet<string> given = [];
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
				if (!TryAcceptPositional(argument, out error))
					return false;

				continue;
			}

			if (!TryReadOption(args, ref index, argument, given, out error))
				return false;
		}

		error = null;
		return true;
	}

	/// <summary>Whether the tool takes the argument, and otherwise the error it raises.</summary>
	protected abstract bool TryAcceptPositional(string argument, [NotNullWhen(false)] out string? error);

	/// <summary>Whether the tool takes the value of the option, and otherwise the error it raises.</summary>
	protected abstract bool TryAcceptValue(string option, string value, [NotNullWhen(false)] out string? error);

	/// <summary>Whether the tool knows the flag, which it then sets.</summary>
	private bool TryAcceptFlag(string option)
	{
		switch (option)
		{
			case "-h" or "--help":
				Help = true;
				return true;

			case "--version":
				Version = true;
				return true;

			default:
				return TryAcceptToolFlag(option);
		}
	}

	/// <summary>Whether the tool knows a flag other than help and version, which it then sets.</summary>
	protected abstract bool TryAcceptToolFlag(string option);

	/// <summary>The error for a value option the tool has no case for.</summary>
	protected static ArgumentOutOfRangeException BuildUnreadValueOption(string option) =>
		new(nameof(option), option, "The value option has no case here.");

	private bool TryReadOption(string[] args, ref int index, string argument, HashSet<string> given, [NotNullWhen(false)] out string? error)
	{
		// "--profile=xml" equals "--profile xml" for an option that takes a value.
		// A flag written this way keeps its text and stays unknown.
		string option = argument;
		string? value = null;
		int equals = argument.IndexOf('=');

		if (equals > 0 && ValueOptions.Contains(argument[..equals]))
		{
			option = argument[..equals];
			value = argument[(equals + 1)..];
		}

		if (!ValueOptions.Contains(option))
		{
			bool known = TryAcceptFlag(option);
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

		// A repeatable option may be given any number of times, any other once.
		if (!given.Add(option) && !RepeatableOptions.Contains(option))
		{
			error = $"the {option} option was given more than once.";
			return false;
		}

		return TryAcceptValue(option, value, out error);
	}
}
