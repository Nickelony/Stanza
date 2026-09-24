using System;
using System.Runtime.CompilerServices;

namespace Nickelony.Stanza.Analyzers;

/// <summary>Caches one value per key object, and drops it when the key is collected.</summary>
internal sealed class WeakCache<TKey, TValue>
	where TKey : class
{
	private readonly ConditionalWeakTable<TKey, Entry> _table = new();

	/// <summary>Passes the state to the computation, so a caller's delegate need not close over it.</summary>
	/// <returns>The first value stored, since two threads can compute one key twice outside any lock.</returns>
	public TValue GetOrCompute<TState>(TKey key, TState state, Func<TKey, TState, TValue> compute)
	{
		if (_table.TryGetValue(key, out Entry? cached))
			return cached.Value;

		return Store(key, compute(key, state));
	}

	public TValue GetOrCompute(TKey key, Func<TKey, TValue> compute) => GetOrCompute(key, compute, static (key, compute) => compute(key));

	/// <summary>Keeps the first value stored for a key, the one every caller reads.</summary>
	private TValue Store(TKey key, TValue value)
	{
		Entry entry = new(value);
		return _table.GetValue(key, _ => entry).Value;
	}

	private sealed class Entry(TValue value)
	{
		public TValue Value { get; } = value;
	}
}
