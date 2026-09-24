using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Nickelony.Stanza.Markup.Configuration;
using Nickelony.Stanza.Shared;

namespace Nickelony.Stanza.Markup.Cli;

/// <summary>The options and paths of a <c>stanza-markup</c> run, read from its arguments, and the usage text.</summary>
internal sealed class MarkupCommandLine
{
	public static readonly string Usage = $"""
		stanza-markup - keep the multi-line sibling elements of a markup document apart

		Usage:
		  stanza-markup <command> [options] <path>...

		Commands:
		  format   Add the blank lines that are missing, cap each gap of blank lines that is too long,
		           and write the files back.
		  check    Report the files that are not formatted. Exits with 1 when there are any.

		Paths:
		  A file, a directory that is scanned recursively, or a glob such as "src/**/*.xml".

		Options:
		  --profile <name>       Lay out the files a glob or a file argument names as this profile,
		                         whatever their extension.
		  --exclude <glob>       Skip the files the glob matches, judged by their path from the working
		                         directory. May be repeated.
		  --format <name>        How check reports: human (the default) prints one line per file, and
		                         msbuild prints one MSBuild diagnostic per finding.
		  --reprint              Write the body of every <member> with the documentation layout,
		                         whatever stanza_markup_doc_reprint says.
		  --strict               Treat each warning as an error.
		  -h, --help             Show this help.
		  --version              Show the tool version.
		  --                     Read every later argument as a path or a command.

		Profiles: {MarkupProfiles.Summary}

		Each file takes its keys from the .editorconfig files that apply to it.

		Options in full: https://github.com/Nickelony/Stanza/wiki/Markup-command-reference
		Keys:            https://github.com/Nickelony/Stanza/wiki/Markup-configuration
		""";

	public MarkupCommand Command { get; private init; }
	public MarkupReportFormat ReportFormat { get; private init; }

	public bool Help { get; private init; }
	public bool Version { get; private init; }
	public bool Strict { get; private init; }
	public bool Reprint { get; private init; }

	public string? Profile { get; private init; }

	public ImmutableArray<string> Paths { get; private init; } = [];
	public ImmutableArray<string> Excludes { get; private init; } = [];

	/// <summary>Reads the arguments, and gives the error of the first one that cannot be read.</summary>
	public static bool TryParse(string[] args, [NotNullWhen(true)] out MarkupCommandLine? commandLine, [NotNullWhen(false)] out string? error)
	{
		ArgumentReader reader = new();

		if (!reader.TryReadAll(args, out error))
		{
			commandLine = null;
			return false;
		}

		return reader.TryBuild(out commandLine, out error);
	}

	private static bool TryParseFormat(string value, out MarkupReportFormat format)
	{
		if (string.Equals(value, "human", StringComparison.OrdinalIgnoreCase))
		{
			format = MarkupReportFormat.Human;
			return true;
		}

		if (string.Equals(value, "msbuild", StringComparison.OrdinalIgnoreCase))
		{
			format = MarkupReportFormat.MsBuild;
			return true;
		}

		format = MarkupReportFormat.Human;
		return false;
	}

	private static bool TryParseCommand(string argument, out MarkupCommand command)
	{
		if (string.Equals(argument, "format", StringComparison.OrdinalIgnoreCase))
		{
			command = MarkupCommand.Format;
			return true;
		}

		if (string.Equals(argument, "check", StringComparison.OrdinalIgnoreCase))
		{
			command = MarkupCommand.Check;
			return true;
		}

		command = MarkupCommand.Format;
		return false;
	}

	/// <summary>Holds what the arguments read so far set, until the last one is read.</summary>
	private sealed class ArgumentReader : IToolArgumentHandler
	{
		private static readonly string[] s_valueOptions = ["--profile", "--exclude", "--format"];

		private readonly ImmutableArray<string>.Builder _paths = ImmutableArray.CreateBuilder<string>();
		private readonly ImmutableArray<string>.Builder _excludes = ImmutableArray.CreateBuilder<string>();

		private MarkupCommand? _command;
		private MarkupReportFormat _format = MarkupReportFormat.Human;

		private string? _profile;

		private bool _help;
		private bool _version;
		private bool _strict;
		private bool _reprint;
		private bool _formatGiven;

		public bool TryReadAll(string[] args, [NotNullWhen(false)] out string? error) =>
			ToolArguments.TryRead(args, valueOptions: s_valueOptions, handler: this, out error);

		public bool TryBuild([NotNullWhen(true)] out MarkupCommandLine? commandLine, [NotNullWhen(false)] out string? error)
		{
			error = _help || _version ? null : FindCommandError();

			if (error is not null)
			{
				commandLine = null;
				return false;
			}

			commandLine = new MarkupCommandLine
			{
				// Reachable only with --help or --version, where no command is required.
				Command = _command ?? MarkupCommand.Format,
				Help = _help,
				Version = _version,
				Strict = _strict,
				ReportFormat = _format,
				Reprint = _reprint,
				Profile = _profile,
				Paths = _paths.ToImmutable(),
				Excludes = _excludes.ToImmutable(),
			};

			return true;
		}

		/// <summary>
		/// The first command word is the command; any other positional, a later command word too, is a path.
		/// </summary>
		public bool TryAcceptPositional(string argument, [NotNullWhen(false)] out string? error)
		{
			if (_command is null && TryParseCommand(argument, out MarkupCommand parsed))
				_command = parsed;
			else
				_paths.Add(argument);

			error = null;
			return true;
		}

		public bool TryAcceptFlag(string option)
		{
			switch (option)
			{
				case "-h" or "--help":
					_help = true;
					return true;

				case "--version":
					_version = true;
					return true;

				case "--strict":
					_strict = true;
					return true;

				case "--reprint":
					_reprint = true;
					return true;

				default:
					return false;
			}
		}

		public bool TryAcceptValue(string option, string value, [NotNullWhen(false)] out string? error)
		{
			error = null;

			switch (option)
			{
				case "--format":
					if (_formatGiven)
					{
						error = BuildRepeatError(option);
						return false;
					}

					_formatGiven = true;

					if (TryParseFormat(value, out _format))
						return true;

					error = $"unknown report format '{value}'. Known formats: human, msbuild.";
					return false;

				case "--profile":
					if (_profile is not null)
					{
						error = BuildRepeatError(option);
						return false;
					}

					_profile = value;
					return true;

				case "--exclude":
					_excludes.Add(value);
					return true;

				default:
					throw new ArgumentOutOfRangeException(nameof(option), option, "The value option has no case here.");
			}
		}

		private static string BuildRepeatError(string option) => $"the {option} option was given more than once.";

		/// <summary>
		/// Weighs the command and the options it cannot carry before the paths,
		/// so <c>format --format msbuild</c> reports the option, not the missing path.
		/// </summary>
		private string? FindCommandError()
		{
			// Without a command every positional was read as a path, and the first is named, as it is most often a mistyped command.
			if (_command is null)
			{
				string given = _paths.Count > 0 ? $", and '{_paths[0]}' is not one" : string.Empty;
				return $"a command is required, either 'format' or 'check'{given}.";
			}

			if (_format == MarkupReportFormat.MsBuild && _command != MarkupCommand.Check)
				return "the --format msbuild option only applies to 'check'.";

			return _paths.Count == 0 ? "at least one path is required." : null;
		}
	}
}
