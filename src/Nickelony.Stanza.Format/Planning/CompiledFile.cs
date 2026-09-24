namespace Nickelony.Stanza.Format.Planning;

/// <summary>A source a project compiles, and its view: the framework and symbols the compiler reads it with.</summary>
internal sealed record CompiledFile(string Path, string Project, string View);
