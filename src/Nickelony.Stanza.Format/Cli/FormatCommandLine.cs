using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Nickelony.Stanza.Format.Planning;
using Nickelony.Stanza.Format.Running;
using Nickelony.Stanza.Shared;

namespace Nickelony.Stanza.Format.Cli;

/// <summary>The options of a <c>stanza-format</c> run, read from its arguments, and the usage text.</summary>
internal sealed class FormatCommandLine
{
	public const string Usage = """
		stanza-format - run dotnet format, CSharpier and stanza-markup in the order the Stanza rules need

		Usage:
		  stanza-format [options] [<path>]

		Without --all, --check or --since, formats the files git reports as changed, through the projects that compile them.

		Path:
		  A solution file, or a directory that holds one solution. The default is the working directory.

		Options:
		  --all                  Format the whole solution, and lay out the files of its directory.
		  --check                Write nothing, and exit with 1 when a step would change a file.
		                         Checks the whole solution, or the files --since names.
		  --since <ref>          Take the files changed since the current branch left <ref>, such as
		                         origin/main, and the untracked ones. Scopes --check too. Not with --all.
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

		if (!reader.TryReadAll(args, out error) || !reader.TryValidate(out error))
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
	private sealed class ArgumentReader : ToolArgumentReader
	{
		private static readonly string[] s_valueOptions = ["--layout", "--since"];

		/// <summary>The value options a line may repeat, which is none of them.</summary>
		private static readonly string[] s_repeatableOptions = [];

		protected override IReadOnlyCollection<string> ValueOptions => s_valueOptions;
		protected override IReadOnlyCollection<string> RepeatableOptions => s_repeatableOptions;

		private bool _all;
		private bool _check;

		private string? _path;
		private string? _since;

		private FormatLayout? _layout;

		/// <summary>Refuses options that cannot be read together, whichever came first.</summary>
		public bool TryValidate([NotNullWhen(false)] out string? error)
		{
			error = _all && _since is not null ? "--since scopes a run to the changed files, so it cannot be combined with --all." : null;
			return error is null;
		}

		public FormatCommandLine Build()
		{
			return new FormatCommandLine
			{
				Options = new FormatOptions(All: _all, Check: _check, Path: _path ?? ".", Layout: _layout, Since: _since),
				Help = Help,
				Version = Version,
			};
		}

		protected override bool TryAcceptPositional(string argument, [NotNullWhen(false)] out string? error)
		{
			// An empty path would run on the working directory, as no path does, so it is refused rather than guessed at.
			if (argument.Length == 0)
			{
				error = "the path is empty.";
				return false;
			}

			if (_path is not null)
			{
				error = $"only one path can be formatted, and '{_path}' was named before '{argument}'.";
				return false;
			}

			_path = argument;
			error = null;

			return true;
		}

		protected override bool TryAcceptToolFlag(string option)
		{
			switch (option)
			{
				case "--all":
					_all = true;
					return true;

				case "--check":
					_check = true;
					return true;

				default:
					return false;
			}
		}

		protected override bool TryAcceptValue(string option, string value, [NotNullWhen(false)] out string? error)
		{
			if (option == "--since")
			{
				error = GitRepository.DescribeOptionLikeRef(value);

				if (error is not null)
					return false;

				_since = value;
				return true;
			}

			if (option != "--layout")
				throw BuildUnreadValueOption(option);

			bool known = TryParseLayout(value, out _layout);
			error = known ? null : $"unknown layout '{value}'. Known layouts: csharpier, whitespace, none.";

			return known;
		}
	}
}
