namespace Nickelony.Stanza.Format.Running;

/// <summary>The code a child process exited with, and what it wrote to each stream.</summary>
internal sealed record ProcessResult(int ExitCode, string Output, string Error);
