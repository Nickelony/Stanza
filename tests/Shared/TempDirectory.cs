using System;
using System.Diagnostics;
using System.IO;

namespace Nickelony.Stanza.Tests.Shared;

internal sealed class TempDirectory : IDisposable
{
	// The .editorconfig's root = true stops the EditorConfig lookup from leaving the fixture.
	// A git test goes without it, so the repository holds only the files the test writes.
	public TempDirectory(bool editorConfig = true)
	{
		Root = Path.Combine(Path.GetTempPath(), $"stanza-tests-{Guid.NewGuid():N}");
		_ = Directory.CreateDirectory(Root);

		if (editorConfig)
			_ = Write(".editorconfig", "root = true\n");
	}

	public string Root { get; }

	public string Write(string relativePath, string content)
	{
		string fullPath = GetFullPath(relativePath);

		_ = Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
		File.WriteAllText(fullPath, content);

		return fullPath;
	}

	public string Read(string relativePath) => File.ReadAllText(GetFullPath(relativePath));

	// Windows may require Developer Mode or elevation, so callers handle a failed link creation.
	public static bool TryCreateLink(string linkPath, string targetPath, bool directory)
	{
		try
		{
			FileSystemInfo link = directory
				? Directory.CreateSymbolicLink(linkPath, targetPath)
				: File.CreateSymbolicLink(linkPath, targetPath);

			return (link.Attributes & FileAttributes.ReparsePoint) != 0;
		}
		catch (Exception exception)
			when (exception
					is UnauthorizedAccessException
						or IOException
						or PlatformNotSupportedException
			)
		{
			return false;
		}
	}

	public static bool TryCreateDirectoryLink(string linkPath, string targetPath)
	{
		return TryCreateLink(linkPath, targetPath, directory: true)
			|| TryCreateJunction(linkPath, targetPath);
	}

	private static bool TryCreateJunction(string linkPath, string targetPath)
	{
		return OperatingSystem.IsWindows()
			&& TryRun("cmd.exe", "/c", "mklink", "/J", linkPath, targetPath)
			&& (File.GetAttributes(linkPath) & FileAttributes.ReparsePoint) != 0;
	}

	// A hard link needs no elevation, so a test of link behavior can run where a symbolic link cannot.
	public static bool TryCreateHardLink(string linkPath, string targetPath)
	{
		bool created = OperatingSystem.IsWindows()
			? TryRun("cmd.exe", "/c", "mklink", "/H", linkPath, targetPath)
			: TryRun("ln", targetPath, linkPath);

		return created && File.Exists(linkPath);
	}

	private static bool TryRun(string fileName, params string[] arguments)
	{
		var info = new ProcessStartInfo(fileName)
		{
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};

		foreach (string argument in arguments)
			info.ArgumentList.Add(argument);

		string description = $"{fileName} {string.Join(' ', arguments)}";

		return ChildProcess.Run(info, description).ExitCode == 0;
	}

	public void Dispose()
	{
		if (!Directory.Exists(Root))
			return;

		// Git writes its objects read-only, and Windows deletes no read-only file.
		string git = Path.Combine(Root, ".git");

		if (Directory.Exists(git))
		{
			foreach (string file in Directory.GetFiles(git, "*", SearchOption.AllDirectories))
				File.SetAttributes(file, FileAttributes.Normal);
		}

		try
		{
			Directory.Delete(Root, recursive: true);
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
		{
			// A recursive delete that held a link can leave the empty root behind,
			// so the root is removed again without recursion.
			RemoveEmptyRoot(exception);
		}
	}

	private void RemoveEmptyRoot(Exception reason)
	{
		try
		{
			Directory.Delete(Root);
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
		{
			Console.Error.WriteLine(
				$"stanza-tests: could not remove the temporary directory '{Root}': "
					+ $"{reason.GetType().Name}: {reason.Message} "
					+ $"(retry: {exception.GetType().Name}: {exception.Message})"
			);
		}
	}

	private string GetFullPath(string relativePath) =>
		Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
}
