using System;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>The statements one model has been asked about, with the answer for each.</summary>
/// <remarks>
/// A model belongs to one compilation, so a change of references starts a new cache.
/// </remarks>
internal sealed class StatementReads<T>(SemanticModel semanticModel, StatementReads<T>.Binder bind)
	where T : struct
{
	/// <summary>Reads the answer for a statement, or declines it when it is no candidate and binds no symbol.</summary>
	public delegate bool Binder(SemanticModel semanticModel, SyntaxNode statement, CancellationToken cancellationToken, out T answer);

	private readonly ConcurrentDictionary<SyntaxNode, T> _known = new();
	private int _bound;

	/// <summary>How many statements the model has bound; a test seam.</summary>
	public int Bound => Volatile.Read(ref _bound);

	/// <summary>Reads the answer for a statement, or <c>default</c> for one the binder declines.</summary>
	public T Read(SyntaxNode statement, CancellationToken cancellationToken)
	{
		if (_known.TryGetValue(statement, out T known))
			return known;

		if (!bind(semanticModel, statement, cancellationToken, out T answer))
			return default;

		_ = Interlocked.Increment(ref _bound);
		_known[statement] = answer;

		return answer;
	}

	/// <summary>Gets a reader of the answers, for a caller that passes one on.</summary>
	public Func<SyntaxNode, T> GetReader(CancellationToken cancellationToken) => statement => Read(statement, cancellationToken);
}
