using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace Nickelony.Stanza.Tests.Shared;

/// <summary>Runs a command on Windows with no console, its output and error on pipes.</summary>
// Process cannot ask for DETACHED_PROCESS, so only CreateProcess gives a child no console at all.
[SupportedOSPlatform("windows")]
internal static class DetachedProcess
{
	private const uint DetachedFlag = 0x00000008;

	private const int UseStandardHandlesFlag = 0x00000100;
	private const int InheritFlag = 0x1;

	private const uint WaitMilliseconds = 30_000;

	public static RunResult Run(string commandLine, string workingDirectory)
	{
		var attributes = new SecurityAttributes
		{
			Length = Marshal.SizeOf<SecurityAttributes>(),
			InheritHandle = true,
		};

		CreatePipeOrThrow(
			out SafeFileHandle outputRead,
			out SafeFileHandle outputWrite,
			ref attributes
		);

		CreatePipeOrThrow(
			out SafeFileHandle errorRead,
			out SafeFileHandle errorWrite,
			ref attributes
		);

		// The read ends stay with this process, so the child inherits only the ends it writes to.
		_ = SetHandleInformation(outputRead, mask: InheritFlag, flags: 0);
		_ = SetHandleInformation(errorRead, mask: InheritFlag, flags: 0);

		var startup = new StartupInfo
		{
			Size = Marshal.SizeOf<StartupInfo>(),
			Flags = UseStandardHandlesFlag,
			StandardOutput = outputWrite.DangerousGetHandle(),
			StandardError = errorWrite.DangerousGetHandle(),
		};

		bool started = CreateProcess(
			applicationName: null,
			commandLine,
			processAttributes: IntPtr.Zero,
			threadAttributes: IntPtr.Zero,
			inheritHandles: true,
			creationFlags: DetachedFlag,
			environment: IntPtr.Zero,
			currentDirectory: workingDirectory,
			startupInfo: ref startup,
			processInformation: out ProcessInformation process
		);

		int failure = Marshal.GetLastWin32Error();

		outputWrite.Dispose();
		errorWrite.Dispose();

		if (!started)
			throw new Win32Exception(failure);

		using var processHandle = new SafeFileHandle(process.Process, ownsHandle: true);
		using var threadHandle = new SafeFileHandle(process.Thread, ownsHandle: true);

		using var output = new StreamReader(
			new FileStream(outputRead, FileAccess.Read),
			Encoding.UTF8
		);

		using var error = new StreamReader(
			new FileStream(errorRead, FileAccess.Read),
			Encoding.UTF8
		);

		// Both pipes drain while the process runs, because a full pipe blocks it.
		Task<string> errorText = Task.Run(error.ReadToEnd);
		string outputText = output.ReadToEnd();

		if (WaitForSingleObject(process.Process, WaitMilliseconds) != 0)
			throw new TimeoutException($"No exit within {WaitMilliseconds} ms: {commandLine}");

		_ = GetExitCodeProcess(process.Process, out uint exitCode);

		return new RunResult((int)exitCode, outputText, errorText.GetAwaiter().GetResult());
	}

	private static void CreatePipeOrThrow(
		out SafeFileHandle read,
		out SafeFileHandle write,
		ref SecurityAttributes attributes
	)
	{
		if (!CreatePipe(out read, out write, ref attributes, size: 0))
			throw new Win32Exception(Marshal.GetLastWin32Error());
	}

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool CreatePipe(
		out SafeFileHandle read,
		out SafeFileHandle write,
		ref SecurityAttributes attributes,
		int size
	);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool SetHandleInformation(SafeFileHandle handle, int mask, int flags);

	[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	private static extern bool CreateProcess(
		string? applicationName,
		string commandLine,
		IntPtr processAttributes,
		IntPtr threadAttributes,
		bool inheritHandles,
		uint creationFlags,
		IntPtr environment,
		string? currentDirectory,
		ref StartupInfo startupInfo,
		out ProcessInformation processInformation
	);

	[DllImport("kernel32.dll")]
	private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

	[DllImport("kernel32.dll")]
	private static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);

	// The native call reads each field of the three layouts below, so they keep the native order.
	[StructLayout(LayoutKind.Sequential)]
	private struct SecurityAttributes
	{
		public int Length;

		public IntPtr SecurityDescriptor;

		public bool InheritHandle;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct StartupInfo
	{
		public int Size;

		public string? Reserved;
		public string? Desktop;
		public string? Title;

		public int X;
		public int Y;
		public int XSize;
		public int YSize;
		public int XCountChars;
		public int YCountChars;
		public int FillAttribute;
		public int Flags;

		public short ShowWindow;
		public short Reserved2Size;

		public IntPtr Reserved2;
		public IntPtr StandardInput;
		public IntPtr StandardOutput;
		public IntPtr StandardError;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct ProcessInformation
	{
		public IntPtr Process;
		public IntPtr Thread;

		public int ProcessId;
		public int ThreadId;
	}
}
