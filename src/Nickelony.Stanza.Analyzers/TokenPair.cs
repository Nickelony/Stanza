using Microsoft.CodeAnalysis;

namespace Nickelony.Stanza.Analyzers;

/// <summary>Two tokens in text order, the one before a place and the one after it.</summary>
internal readonly record struct TokenPair(SyntaxToken Previous, SyntaxToken Next);
