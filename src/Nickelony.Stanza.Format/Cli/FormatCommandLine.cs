using System;
using System.Diagnostics.CodeAnalysis;
using Nickelony.Stanza.Format.Planning;
using Nickelony.Stanza.Shared;

namespace Nickelony.Stanza.Format.Cli;

/// <summary>The options of a <c>stanza-format</c> run, read from its arguments, and the usage text.</summary>
internal sealed class FormatCommandLine
{
	public const string Usage = """
		stanza-format - run dotnet format, CSharpier and stanza-markup in the order the Stanza rules need

		Usage:
		  stanza-format [options] [<path>]

		Without --all or --check, formats the files git reports as changed, through the projects that compile them.

		Path:
		  A solution file, or a directory that holds one solution. The default is the working directory.

		Options:
		  --all                  Format the whole solution, and lay out the files of its directory.
		  --check                Write nothing, and exit with 1 when a step would change a file.
		                         Checks the whole solution.
		  --layout <name>        What lays out the code between the rule passes: csharpier, whitespace
		                         (the whitespace pass of dotnet format) or none. The default is csharpier
		                         when the tool manifest lists it, and whitespace when it does not.
		  -h, --help             Show this help.
		  --version              Show the tool version.
		  --                     Read the next argument as the path.

		Options in full: https://github.com/Nickelony/Stanza/wiki/Format-command-reference
		""";

	public required FormatOptions Options { get; init; }

	public bool Help { get; private init; }
	public bool Version { get; private init; }

	/// <summary>Reads the arguments, and gives the error of the first one that cannot be read.</summary>
	public static bool TryParse(string[] args, [NotNullWhen(true)] out FormatCommandLine? commandLine, [NotNullWhen(false)] out string? error)
	{
		ArgumentReader reader = new();

		if (!reader.TryReadAll(args, out error))
		{
			commandLine = null;
			return false;
		}

		commandLine = reader.Build();
		return true;
	}

	private static bool TryParseLayout(string value, [NotNullWhen(true)] out FormatLayout? layout)
	{
		layout = value.ToLowerInvariant() switch
		{
			"csharpier" => FormatLayout.CSharpier,
			"whitespace" => FormatLayout.Whitespace,
			"none" => FormatLayout.None,
			_ => null,
		};

		return layout is not null;
	}

	/// <summary>Holds what the arguments read so far set, until the last one is read.</summary>
	private sealed class ArgumentReader : IToolArgumentHandler
	{
		private static readonly string[] s_valueOptions = ["--layout"];

		private bool _all;
		private bool _check;
		private bool _help;
		private bool _version;

		private string? _path;

		private FormatLayout? _layout;

		public bool TryReadAll(string[] args, [NotNullWhen(false)] out string? error) =>
			ToolArguments.TryRead(args, valueOptions: s_valueOptions, handler: this, out error);

		public FormatCommandLine Build()
		{
			return new FormatCommandLine
			{
				Options = new FormatOptions(All: _all, Check: _check, Path: _path ?? ".", Layout: _layout),
				Help = _help,
				Version = _version,
			};
		}

		public bool TryAcceptPositional(string argument, [NotNullWhen(false)] out string? error)
		{
			if (_path is not null)
			{
				error = $"only one path can be formatted, and '{_path}' was named before '{argument}'.";
				return false;
			}

			_path = argument;
			error = null;
			return true;
		}

		public bool TryAcceptFlag(string option)
		{
			switch (option)
			{
				case "--all":
					_all = true;
					return true;

				case "--check":
					_check = true;
					return true;

				case "-h"
				or "--help":
					_help = true;
					return true;

				case "--version":
					_version = true;
					return true;

				default:
					return false;
			}
		}

		public bool TryAcceptValue(string option, string value, [NotNullWhen(false)] out string? error)
		{
			if (option != "--layout")
				throw new ArgumentOutOfRangeException(nameof(option), option, "The value option has no case here.");

			bool known = TryParseLayout(value, out _layout);
			error = known ? null : $"unknown layout '{value}'. Known layouts: csharpier, whitespace, none.";

			return known;
		}
	}
}
