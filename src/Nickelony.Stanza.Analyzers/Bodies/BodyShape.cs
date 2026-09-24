namespace Nickelony.Stanza.Analyzers.Bodies;

internal enum BodyShape
{
	Statement,
	Value,
	Accessor,
}

internal static class BodyShapes
{
	/// <summary>Whether the body hands a value back: a property or indexer getter, or an accessor.</summary>
	public static bool IsValueShaped(this BodyShape shape) => shape is BodyShape.Value or BodyShape.Accessor;
}
