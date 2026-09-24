using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Nickelony.Stanza.Format.Running;

/// <summary>The local tools of the repository's manifest that a run uses, and the CSharpier version it pins.</summary>
/// <remarks>
/// <see cref="IsRoot"/> is whether <c>dotnet</c> stops reading manifests at this one.
/// </remarks>
internal sealed record ToolManifest(string? CSharpierVersion, bool HasMarkup, bool IsRoot = false)
{
	public bool HasCSharpier => CSharpierVersion is not null;

	/// <summary>A CSharpier this old lacks the <c>format</c> and <c>check</c> commands a run calls.</summary>
	/// <seealso href="https://github.com/Nickelony/Stanza/wiki/Format-command-reference#layout"/>
	public bool PinsOldCSharpier => CSharpierVersion?.StartsWith("0.", StringComparison.Ordinal) == true;

	/// <summary>Reads the manifests of the directory and its parents, each tool from the nearest listing.</summary>
	/// <remarks>
	/// The walk ends at a manifest whose <c>isRoot</c> is <see langword="true"/>, as <c>dotnet</c> reads them.
	/// </remarks>
	public static ToolManifest Read(string directory)
	{
		string? csharpierVersion = null;
		bool hasMarkup = false;

		for (DirectoryInfo? current = new(directory); current is not null; current = current.Parent)
		{
			bool isRoot = false;

			foreach (string path in FindManifests(current.FullName))
			{
				ToolManifest manifest = ReadFile(path);

				csharpierVersion ??= manifest.CSharpierVersion;
				hasMarkup |= manifest.HasMarkup;
				isRoot |= manifest.IsRoot;
			}

			if (isRoot)
				break;
		}

		return new ToolManifest(CSharpierVersion: csharpierVersion, HasMarkup: hasMarkup);
	}

	public static ToolManifest FromJson(string json)
	{
		JsonDocumentOptions options = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

		using var document = JsonDocument.Parse(json, options);

		if (document.RootElement.ValueKind != JsonValueKind.Object)
			throw new JsonException("The manifest is not a JSON object.");

		string? csharpierVersion = null;
		bool hasMarkup = false;

		if (document.RootElement.TryGetProperty("tools", out JsonElement listed) && listed.ValueKind == JsonValueKind.Object)
		{
			foreach (JsonProperty tool in listed.EnumerateObject())
			{
				if (tool.Name.Equals("csharpier", StringComparison.OrdinalIgnoreCase))
					csharpierVersion = ReadVersion(tool.Value);
				else if (tool.Name.Equals("nickelony.stanza.markup", StringComparison.OrdinalIgnoreCase))
					hasMarkup = true;
			}
		}

		bool isRoot = document.RootElement.TryGetProperty("isRoot", out JsonElement root) && root.ValueKind == JsonValueKind.True;

		return new ToolManifest(CSharpierVersion: csharpierVersion, HasMarkup: hasMarkup, IsRoot: isRoot);
	}

	/// <summary>Finds the manifests a directory holds, the one in <c>.config</c> first, as dotnet reads them.</summary>
	private static string[] FindManifests(string directory)
	{
		string[] manifests = [Path.Combine(directory, ".config", "dotnet-tools.json"), Path.Combine(directory, "dotnet-tools.json")];
		return [.. manifests.Where(File.Exists)];
	}

	private static ToolManifest ReadFile(string path)
	{
		try
		{
			return FromJson(File.ReadAllText(path));
		}
		catch (JsonException exception)
		{
			throw new FormatRunException($"the tool manifest '{path}' is not valid JSON: {exception.Message}", exception);
		}
	}

	/// <summary>A tool listed without a version string pins an empty one, so the manifest still lists it.</summary>
	private static string ReadVersion(JsonElement tool)
	{
		if (tool.ValueKind != JsonValueKind.Object || !tool.TryGetProperty("version", out JsonElement version))
			return string.Empty;

		return version.ValueKind == JsonValueKind.String ? version.GetString()! : string.Empty;
	}
}
