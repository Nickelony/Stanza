using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Tests.Shared;

/// <summary>A directory that cannot be listed until it is disposed.</summary>
internal sealed class DeniedDirectory : IDisposable
{
	private readonly Action _restore;

	private DeniedDirectory(Action restore) => _restore = restore;

	public void Dispose() => _restore();

	/// <summary>Denies the listing, or skips the test where a denial is not enforced.</summary>
	public static DeniedDirectory DenyOrSkip(string path)
	{
		Action? restore = null;
		string? failure = null;

		try
		{
			restore = DenyListing(path);
		}
		catch (Exception exception)
			when (exception
					is UnauthorizedAccessException
						or IOException
						or PlatformNotSupportedException
			)
		{
			failure = exception.Message;
		}

		// MSTEST0058 expects the assertion outside the catch block.
		if (restore is null)
			Assert.Inconclusive($"This machine does not let the test deny a directory: {failure}");

		// A user that bypasses permissions, such as root, still reads the directory.
		if (!IsListingDenied(path))
		{
			restore();
			Assert.Inconclusive("This machine reads a directory whose access is denied.");
		}

		return new DeniedDirectory(restore);
	}

	private static Action DenyListing(string path) =>
		OperatingSystem.IsWindows() ? DenyOnWindows(path) : DenyOnUnix(path, UnixFileMode.None);

	private static bool IsListingDenied(string path)
	{
		try
		{
			_ = Directory.EnumerateFileSystemEntries(path).FirstOrDefault();
			return false;
		}
		catch (UnauthorizedAccessException)
		{
			return true;
		}
	}

	[SupportedOSPlatform("windows")]
	private static Action DenyOnWindows(string path)
	{
		var directory = new DirectoryInfo(path);
		DirectorySecurity security = directory.GetAccessControl();

		var denial = new FileSystemAccessRule(
			new SecurityIdentifier(WellKnownSidType.WorldSid, null),
			FileSystemRights.ListDirectory,
			AccessControlType.Deny
		);

		security.AddAccessRule(denial);
		directory.SetAccessControl(security);

		// A set writes only the rules its object changed, so the same object takes the denial off again.
		return () =>
		{
			_ = security.RemoveAccessRule(denial);
			directory.SetAccessControl(security);
		};
	}

	[UnsupportedOSPlatform("windows")]
	private static Action DenyOnUnix(string path, UnixFileMode denied)
	{
		UnixFileMode original = File.GetUnixFileMode(path);

		File.SetUnixFileMode(path, denied);

		return () => File.SetUnixFileMode(path, original);
	}
}
