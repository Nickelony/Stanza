using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace Nickelony.Stanza.Format.Running;

/// <summary>Ends the children of the tool, and what they started, when the tool is killed, on Windows.</summary>
/// <remarks>
/// Linux and macOS give a child no such tie, so a tool killed there leaves its children running.
/// </remarks>
internal static class ChildJob
{
	private const int ExtendedLimitInformation = 9;
	private const uint KillOnJobClose = 0x00002000;

	/// <summary>The offset of the limit flags in the native record, after the two 64-bit time limits.</summary>
	private const int LimitFlagsOffset = 16;

	/// <summary>The size of the native record, which grows with the width of a pointer.</summary>
	private static readonly int s_limitInformationSize = Environment.Is64BitProcess ? 144 : 112;

	private static readonly object s_gate = new();

	private static SafeFileHandle? s_job;
	private static bool s_found;

	/// <summary>Puts the process, and the processes it starts from now on, in the job, if Windows lets it.</summary>
	/// <remarks>
	/// A child that starts a process before this call leaves that process outside the job.
	/// </remarks>
	public static void Join(Process process)
	{
		if (!OperatingSystem.IsWindows())
			return;

		try
		{
			if (FindJob() is { } job)
				_ = TryJoinJob(job, process.SafeHandle);
		}
		catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
		{
			// A process that has already exited needs no job, and one that cannot join keeps the run as it was.
		}
	}

	[SupportedOSPlatform("windows")]
	private static SafeFileHandle? FindJob()
	{
		lock (s_gate)
		{
			if (s_found)
				return s_job;

			s_found = true;

			// Every child joins one job that ends its processes when the tool's handle closes, as it does when the tool is killed.
			// A normal exit releases the job first, so a build server a child started for later builds is not ended with the tool.
			s_job = BuildJob();

			if (s_job is not null)
				AppDomain.CurrentDomain.ProcessExit += static (_, _) => TrySetKillOnClose(s_job, kill: false);

			return s_job;
		}
	}

	[SupportedOSPlatform("windows")]
	private static SafeFileHandle? BuildJob()
	{
		SafeFileHandle job = BuildJobObject(attributes: IntPtr.Zero, name: null);

		if (job.IsInvalid || !TrySetKillOnClose(job, kill: true))
		{
			job.Dispose();
			return null;
		}

		return job;
	}

	[SupportedOSPlatform("windows")]
	private static bool TrySetKillOnClose(SafeFileHandle job, bool kill)
	{
		byte[] information = new byte[s_limitInformationSize];

		_ = BitConverter.TryWriteBytes(information.AsSpan(LimitFlagsOffset), kill ? KillOnJobClose : 0);

		return TrySetJobInformation(job, informationClass: ExtendedLimitInformation, information, length: information.Length);
	}

	[DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern SafeFileHandle BuildJobObject(IntPtr attributes, string? name);

	[DllImport("kernel32.dll", EntryPoint = "SetInformationJobObject", SetLastError = true)]
	private static extern bool TrySetJobInformation(SafeFileHandle job, int informationClass, byte[] information, int length);

	[DllImport("kernel32.dll", EntryPoint = "AssignProcessToJobObject", SetLastError = true)]
	private static extern bool TryJoinJob(SafeFileHandle job, SafeProcessHandle process);
}
