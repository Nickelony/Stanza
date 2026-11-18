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

	private static readonly object s_gate = new();

	private static SafeFileHandle? s_job;
	private static bool s_created;

	/// <summary>Puts the process, and the processes it starts from now on, in the job, if Windows lets it.</summary>
	/// <remarks>
	/// A child that starts a process before this call leaves that process outside the job.
	/// </remarks>
	public static void Adopt(Process process)
	{
		if (!OperatingSystem.IsWindows())
			return;

		try
		{
			if (GetJob() is { } job)
				_ = AssignProcessToJobObject(job, process.SafeHandle);
		}
		catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
		{
			// A process that has already exited needs no job, and one that cannot join keeps the run as it was.
		}
	}

	[SupportedOSPlatform("windows")]
	private static SafeFileHandle? GetJob()
	{
		lock (s_gate)
		{
			if (s_created)
				return s_job;

			s_created = true;

			// Every child joins one job that ends its processes when the tool's handle closes, as it does when the tool is killed.
			// A normal exit releases the job first, so a build server a child started for later builds is not ended with the tool.
			s_job = CreateJob();

			if (s_job is not null)
				AppDomain.CurrentDomain.ProcessExit += static (_, _) => SetKillOnClose(s_job, kill: false);

			return s_job;
		}
	}

	[SupportedOSPlatform("windows")]
	private static SafeFileHandle? CreateJob()
	{
		SafeFileHandle job = CreateJobObject(attributes: IntPtr.Zero, name: null);

		if (job.IsInvalid || !SetKillOnClose(job, kill: true))
		{
			job.Dispose();
			return null;
		}

		return job;
	}

	[SupportedOSPlatform("windows")]
	private static bool SetKillOnClose(SafeFileHandle job, bool kill)
	{
		ExtendedLimit limit = default;
		limit.Basic.LimitFlags = kill ? KillOnJobClose : 0;

		return SetInformationJobObject(
			job,
			informationClass: ExtendedLimitInformation,
			information: ref limit,
			length: Marshal.SizeOf<ExtendedLimit>()
		);
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern SafeFileHandle CreateJobObject(IntPtr attributes, string? name);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool SetInformationJobObject(SafeFileHandle job, int informationClass, ref ExtendedLimit information, int length);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool AssignProcessToJobObject(SafeFileHandle job, SafeProcessHandle process);

	// The native call reads each field of the layouts below, so they keep the native order and sizes.
	[StructLayout(LayoutKind.Sequential)]
	private struct BasicLimit
	{
		public long PerProcessUserTimeLimit;
		public long PerJobUserTimeLimit;

		public uint LimitFlags;

		public UIntPtr MinimumWorkingSetSize;
		public UIntPtr MaximumWorkingSetSize;

		public uint ActiveProcessLimit;

		public UIntPtr Affinity;

		public uint PriorityClass;
		public uint SchedulingClass;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct ExtendedLimit
	{
		public BasicLimit Basic;

		public ulong ReadOperationCount;
		public ulong WriteOperationCount;
		public ulong OtherOperationCount;
		public ulong ReadTransferCount;
		public ulong WriteTransferCount;
		public ulong OtherTransferCount;

		public UIntPtr ProcessMemoryLimit;
		public UIntPtr JobMemoryLimit;
		public UIntPtr PeakProcessMemoryUsed;
		public UIntPtr PeakJobMemoryUsed;
	}
}
