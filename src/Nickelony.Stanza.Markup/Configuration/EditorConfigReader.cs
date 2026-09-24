using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Nickelony.Stanza.Markup.Core.Options;
using Nickelony.Stanza.Markup.Notices;
using Nickelony.Stanza.Markup.Paths;
using Nickelony.Stanza.Shared;

namespace Nickelony.Stanza.Markup.Configuration;

/// <summary>Reads the <c>.editorconfig</c> files above a path into a chain of sections.</summary>
internal sealed class EditorConfigReader(MarkupPaths paths, MarkupNotices notices)
{
	/// <summary>The bytes read from one file; a longer file is cut back to its last whole line within them.</summary>
	/// <remarks>
	/// A file is read into memory whole, and a hostile line can be as long as the file.
	/// </remarks>
	internal const int MaxBytes = 1 << 20;

	/// <summary>The sections read from one file; each costs a matcher the first time a path meets it.</summary>
	internal const int MaxSections = 500;

	/// <summary>The problems one file reports, first in line order; a last warning counts the rest.</summary>
	internal const int MaxProblems = 100;

	/// <summary>The characters of a line, key, value or glob that a warning quotes.</summary>
	private const int MaxQuotedLength = 200;

	private const string FileName = ".editorconfig";

	/// <summary>The parsed file at each <c>.editorconfig</c> path, so a file many chains share is read once.</summary>
	/// <remarks>
	/// Measured over 20,000 markup files and 10,000 others, a run without it takes twice as long.
	/// </remarks>
	private readonly Dictionary<string, ParsedFile?> _files = new(PathComparison.Comparer);

	/// <summary>The problems kept for the file being read, the first in line order.</summary>
	/// <remarks>
	/// A refused glob arrives after the lines, so it can displace a problem of a later line.
	/// </remarks>
	private readonly List<Problem> _problems = [];

	/// <summary>The warnings that say the file's settings were dropped, which the cap never hides.</summary>
	private readonly List<Problem> _dropped = [];

	/// <summary>The problems the file being read has had, whether or not they are kept.</summary>
	private int _problemCount;

	/// <summary>The index of the kept problem reported last once the list is full, or -1 while it is not.</summary>
	private int _last = -1;

	private void AddProblem(string path, int? line, string message)
	{
		Problem problem = new(Path: path, Line: line, Message: message, Sequence: ++_problemCount);

		if (_problems.Count < MaxProblems)
		{
			_problems.Add(problem);
			return;
		}

		if (_last < 0)
			_last = FindLastProblem();

		if (!problem.Precedes(_problems[_last]))
			return;

		_problems[_last] = problem;
		_last = FindLastProblem();
	}

	private int FindLastProblem()
	{
		int last = 0;

		for (int index = 1; index < _problems.Count; index++)
		{
			if (_problems[last].Precedes(_problems[index]))
				last = index;
		}

		return last;
	}

	/// <summary>Adds a warning that says settings were dropped, which comes first and is not capped.</summary>
	private void AddDropped(string path, int? line, string message) =>
		_dropped.Add(new Problem(Path: path, Line: line, Message: message, Sequence: 0));

	/// <summary>Reports what the file dropped, then its problems by line, then a count of those left out.</summary>
	private void ReportProblems()
	{
		foreach (Problem dropped in _dropped)
			notices.AddWarning(dropped.Message, paths.GetDisplayPath(dropped.Path), dropped.Line);

		List<Problem> ordered = [.. _problems.OrderBy(static problem => problem.Line ?? int.MaxValue).ThenBy(static problem => problem.Sequence)];

		foreach (Problem problem in ordered)
			notices.AddWarning(problem.Message, paths.GetDisplayPath(problem.Path), problem.Line);

		int left = _problemCount - ordered.Count;

		if (left > 0)
		{
			string displayPath = paths.GetDisplayPath(ordered[0].Path);
			string count = left == 1 ? "1 more problem, which is" : $"{left} more problems, which are";

			notices.AddWarning($"'{displayPath}' has {count} not reported.", displayPath);
		}

		_dropped.Clear();
		_problems.Clear();

		_problemCount = 0;
		_last = -1;
	}

	/// <summary>Cuts text a warning quotes to a fixed length, so a hostile file cannot flood the report.</summary>
	private static string ToQuoted(string text)
	{
		if (text.Length <= MaxQuotedLength)
			return text;

		// A cut between the halves of a surrogate pair would leave a character no encoding can write.
		int length = char.IsHighSurrogate(text[MaxQuotedLength - 1]) ? MaxQuotedLength - 1 : MaxQuotedLength;

		return $"{text[..length]}...";
	}

	private void CheckKey(string path, int line, string key, string value)
	{
		// `unset` removes what an outer file set rather than naming a value to judge,
		// and a key the check finds no problem with is not reported.
		if (EditorConfigChain.IsUnset(value) || MarkupKeyCheck.FindProblem(key, value) is not { } problem)
			return;

		AddProblem(path, line, $"'{paths.GetDisplayPath(path)}' sets {ToQuoted(key)} to '{ToQuoted(value)}': {problem}.");
	}

	public EditorConfigChain GetChain(string filePath)
	{
		List<EditorConfigSection> sections = [];
		string? current = Path.GetDirectoryName(Path.GetFullPath(filePath));

		// Searching up from the file, each file's sections go before those already collected,
		// so the nearer file wins.
		while (!string.IsNullOrEmpty(current))
		{
			string path = Path.Combine(current, FileName);
			ParsedFile? file = FindFile(path, current);

			if (file is not null)
			{
				sections.InsertRange(0, file.Sections);

				if (file.IsRoot)
					break;
			}

			current = Path.GetDirectoryName(current);
		}

		return new EditorConfigChain(sections);
	}

	private ParsedFile? FindFile(string path, string directory)
	{
		if (_files.TryGetValue(path, out ParsedFile? cached))
			return cached;

		// A file that states no length holds no setting, and a FIFO states none, so opening one would block until a writer does.
		ParsedFile? file = HoldsText(new FileInfo(path)) ? ReadFile(path, directory) : null;

		_files.Add(path, file);

		return file;
	}

	/// <summary>A file deleted between the two checks holds no setting either.</summary>
	private static bool HoldsText(FileInfo info)
	{
		try
		{
			return info.Exists && info.Length > 0;
		}
		catch (IOException)
		{
			return false;
		}
	}

	private ParsedFile? ReadFile(string path, string directory)
	{
		try
		{
			ParsedFile file = ReadSections(path, directory);

			ValidateGlobs(path, file);

			return file;
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
		{
			AddDropped(path, line: null, $"'{paths.GetDisplayPath(path)}' could not be read ({exception.Message}); its settings were ignored.");
			return null;
		}
		finally
		{
			ReportProblems();
		}
	}

	private void ValidateGlobs(string path, ParsedFile file)
	{
		foreach (EditorConfigSection section in file.Sections)
		{
			if (!GlobMatcher.TryValidate(section.Glob, out string? refusal))
			{
				AddProblem(
					path,
					line: section.HeaderLine,
					$"'{paths.GetDisplayPath(path)}' has a section '[{ToQuoted(section.Glob)}]' whose pattern is refused ({ToQuoted(refusal)}); it matches nothing."
				);
			}

			// A backslash is the escape of the dialect, so `src\*.xml` names a file whose own name holds a star.
			int escaped = GlobMatcher.FindEscapedWildcard(section.Glob);

			if (escaped >= 0)
			{
				AddProblem(
					path,
					line: section.HeaderLine,
					$"'{paths.GetDisplayPath(path)}' has a section '[{ToQuoted(section.Glob)}]' where a backslash escapes '{section.Glob[escaped]}', so it names that character itself; write a slash to separate directories."
				);
			}
		}
	}

	private ParsedFile ReadSections(string path, string directory)
	{
		SectionBuilder builder = new(directory);
		using StringReader lines = new(ReadText(path));
		int lineNumber = 0;

		while (lines.ReadLine() is { } rawLine)
		{
			lineNumber++;
			string line = rawLine.Trim();

			// A whole line comment is skipped here; an inline one is cut from the header or the value below, as Roslyn does.
			if (line.Length == 0 || line[0] == '#' || line[0] == ';')
				continue;

			if (line[0] != '[')
				ReadPair(path, lineNumber, line, builder);
			else if (!TryReadHeader(path, lineNumber, line, builder))
				break;
		}

		return builder.Build();
	}

	/// <summary>Reads at most <see cref="MaxBytes"/> bytes, cut back to a whole line when the file is longer.</summary>
	private string ReadText(string path)
	{
		using FileStream stream = File.OpenRead(path);

		// One byte past the cap says whether the file goes on, and whether it ends the last line the cap holds.
		byte[] buffer = new byte[Math.Min(stream.Length, MaxBytes + 1L)];
		int length = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);

		// The reader drops a byte order mark, as reading the file line by line does.
		using StreamReader reader = new(new MemoryStream(buffer, 0, Math.Min(length, MaxBytes)), Encoding.UTF8);
		string text = reader.ReadToEnd();

		return length <= MaxBytes ? text : ReadWholeLines(path, text, next: buffer[MaxBytes], goesOn: stream.Length > length);
	}

	/// <summary>Keeps the lines a cut file holds whole, and warns when it drops more than blank space.</summary>
	/// <remarks>
	/// A line is whole when a break ends it within the cap or is the next byte; a lone carriage return is a break.
	/// </remarks>
	private string ReadWholeLines(string path, string text, byte next, bool goesOn)
	{
		bool nextBreaksALine = next is (byte)'\r' or (byte)'\n';
		int end = nextBreaksALine ? text.Length : text.AsSpan().LastIndexOfAny('\r', '\n') + 1;

		// A line break or blank space after the last whole line is no setting, so dropping it needs no warning.
		bool nextIsBlank = next is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';
		bool dropsText = goesOn || !nextIsBlank || !text.AsSpan(end).IsWhiteSpace();

		if (!dropsText)
			return text[..end];

		string displayPath = paths.GetDisplayPath(path);
		int lines = CountLines(text.AsSpan(0, end));

		string message =
			lines == 0
				? $"'{displayPath}' has a first line longer than {MaxBytes >> 20} MiB; the whole file was ignored."
				: $"'{displayPath}' is larger than {MaxBytes >> 20} MiB; everything after its line {lines} was ignored.";

		AddDropped(path, line: null, message);

		return text[..end];
	}

	/// <summary>Counts the lines as <see cref="StringReader"/> does, so a last line without a break counts.</summary>
	private static int CountLines(ReadOnlySpan<char> text)
	{
		int lines = 0;

		for (int index = 0; index < text.Length; index++)
		{
			if (text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
				index++;

			if (text[index] is '\r' or '\n')
				lines++;
			else if (index == text.Length - 1)
				lines++;
		}

		return lines;
	}

	/// <summary>Starts the section a header names, or says that the file holds more sections than are read.</summary>
	private bool TryReadHeader(string path, int lineNumber, string line, SectionBuilder builder)
	{
		string header = MarkupOptionReader.StripComment(line, honorsEscapes: true);

		if (!IsSectionHeader(header))
		{
			// Not a header, so the keys below it do not join the section above.
			AddProblem(
				path,
				lineNumber,
				$"'{paths.GetDisplayPath(path)}' has the line '{ToQuoted(line)}', which is not a section header; it was ignored."
			);

			builder.StartSection(glob: null, headerLine: null);

			return true;
		}

		if (builder.SectionCount >= MaxSections)
		{
			AddDropped(
				path,
				lineNumber,
				$"'{paths.GetDisplayPath(path)}' has more than {MaxSections} sections; the lines from here on were ignored."
			);

			return false;
		}

		// The header check guarantees the last character is the closing bracket.
		builder.StartSection(glob: header[1..^1], headerLine: lineNumber);

		return true;
	}

	private void ReadPair(string path, int lineNumber, string line, SectionBuilder builder)
	{
		int separator = FindSeparator(line);

		if (separator <= 0)
		{
			// Reported rather than dropped in silence:
			// a mistyped key would look like one the tool never reads.
			AddProblem(
				path,
				lineNumber,
				$"'{paths.GetDisplayPath(path)}' has the line '{ToQuoted(line)}', which is not a key with a value; it was ignored."
			);

			return;
		}

		// A key under an unreadable header belongs to no section, not to the file as a whole.
		if (builder.InUnreadableSection)
			return;

		string key = line[..separator].Trim().ToLowerInvariant();
		string value = MarkupOptionReader.StripCommentAndSeverity(key, line[(separator + 1)..]);

		if (builder.Glob is null && key == "root")
		{
			if (MarkupKeyCheck.FromBooleanName(value) is null && !EditorConfigChain.IsUnset(value))
			{
				AddProblem(
					path,
					lineNumber,
					$"'{paths.GetDisplayPath(path)}' sets root to '{ToQuoted(value)}': neither 'true' nor 'false', so the search for a parent .editorconfig continues."
				);
			}

			builder.IsRoot = MarkupKeyCheck.FromBooleanName(value) is true;
			return;
		}

		builder.Add(key, value);
		CheckKey(path, line: lineNumber, key, value);
	}

	/// <summary>This reader accepts <c>=</c> and <c>:</c>, and the key ends at whichever comes first.</summary>
	private static int FindSeparator(string line)
	{
		int equals = line.IndexOf('=');
		int colon = line.IndexOf(':');

		if (equals < 0)
			return colon;

		return colon < 0 ? equals : Math.Min(equals, colon);
	}

	private static bool IsSectionHeader(string line) => line.Length >= 3 && line[0] == '[' && line[^1] == ']';

	private sealed record ParsedFile(bool IsRoot, List<EditorConfigSection> Sections);

	private readonly record struct Problem(string Path, int? Line, string Message, int Sequence)
	{
		/// <summary>Whether this one is reported first: by line, those with no line last, then by arrival.</summary>
		public bool Precedes(Problem other)
		{
			int line = Line ?? int.MaxValue;
			int otherLine = other.Line ?? int.MaxValue;

			return line != otherLine ? line < otherLine : Sequence < other.Sequence;
		}
	}

	/// <summary>Gathers the sections of one file as its lines are read.</summary>
	private sealed class SectionBuilder(string directory)
	{
		private readonly List<EditorConfigSection> _sections = [];
		private readonly Dictionary<string, string> _global = new(StringComparer.OrdinalIgnoreCase);

		private Dictionary<string, string> _pending = new(StringComparer.OrdinalIgnoreCase);
		private int? _headerLine;

		public string? Glob { get; private set; }

		/// <summary>The headers read so far that name a section, whether or not a key follows.</summary>
		public int SectionCount { get; private set; }

		public bool IsRoot { get; set; }

		/// <summary>Whether an unreadable header is in force, so its keys belong to no section.</summary>
		public bool InUnreadableSection { get; private set; }

		public void StartSection(string? glob, int? headerLine)
		{
			AddPendingSection();

			_pending = new(StringComparer.OrdinalIgnoreCase);
			_headerLine = headerLine;
			Glob = glob;

			if (glob is not null)
				SectionCount++;

			InUnreadableSection = glob is null;
		}

		public void Add(string key, string value)
		{
			if (Glob is null)
				_global[key] = value;
			else
				_pending[key] = value;
		}

		public ParsedFile Build()
		{
			AddPendingSection();

			// Keys written before the first section apply to every file, so they go first.
			// The format requires every pair but `root` to sit under a section,
			// so reading them at all is this tool's own leniency rather than the dialect's rule.
			if (_global.Count > 0)
				_sections.Insert(0, new(directory, "*", _global));

			return new(IsRoot, _sections);
		}

		private void AddPendingSection()
		{
			if (Glob is not null && _pending.Count > 0)
				_sections.Add(new(Directory: directory, Glob, Values: _pending, HeaderLine: _headerLine));
		}
	}
}
