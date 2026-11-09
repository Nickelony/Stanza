namespace Nickelony.Stanza.Format.Planning;

/// <summary>A source a project compiles, and its view: the framework, symbols and analyzers it is read with.</summary>
internal sealed record CompiledFile(string Path, string Project, string View);
