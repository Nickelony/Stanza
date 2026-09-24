using System;
using System.Collections.Generic;
using Nickelony.Stanza.Markup.Core.DocComments;

namespace Nickelony.Stanza.Markup.Core.Xml;

internal sealed class MarkupElement(string name, int startLineIndex) : IMarkupItem, IMarkupBodyNode
{
	private readonly List<IMarkupChild> _children = [];

	private bool _sealed;

	public string Name { get; } = name;
	public bool IsPropertyElement { get; } = name.IndexOf('.', name.LastIndexOf(':') + 1) >= 0;

	public int StartLineIndex { get; } = startLineIndex;

	public int EndLineIndex
	{
		get;
		set => field = EnsureUnsealed(value);
	} = startLineIndex;

	/// <summary>
	/// The text a tag is collapsed from only when read, which takes a check of Tomb-Editor from 3.1 s to 2.6 s.
	/// </summary>
	public MarkupSourceText? SourceText
	{
		get;
		set => field = EnsureUnsealed(value);
	}

	/// <summary>The open tag with its whitespace collapsed.</summary>
	public string OpenTag => field ??= SourceText is null ? string.Empty : DocCollapser.Collapse(SourceText.Text, OpenTagStart, OpenTagEnd);

	/// <summary>The end tag collapsed, or <see langword="null"/> for an element with none.</summary>
	public string? CloseTag =>
		CloseTagStart < 0 || SourceText is null ? null : field ??= DocCollapser.Collapse(SourceText.Text, CloseTagStart, CloseTagEnd);

	public int OpenTagStart
	{
		get;
		set => field = EnsureUnsealed(value);
	}

	public bool IsEmpty
	{
		get;
		set => field = EnsureUnsealed(value);
	}

	public int OpenTagEnd
	{
		get;
		set => field = EnsureUnsealed(value);
	}

	public int CloseTagStart
	{
		get;
		set => field = EnsureUnsealed(value);
	} = -1;

	public int CloseTagEnd
	{
		get;
		set => field = EnsureUnsealed(value);
	}

	/// <summary>The text and the elements in document order; empty until sealed.</summary>
	public IReadOnlyList<IMarkupBodyNode> Body { get; private set; } = [];

	/// <summary>The elements, comments and instructions in document order; empty until sealed.</summary>
	public IReadOnlyList<IMarkupItem> Items { get; private set; } = [];

	public bool HasCharacterData
	{
		get;
		set => field = EnsureUnsealed(value);
	}

	public bool IsSpacePreserved
	{
		get;
		set => field = EnsureUnsealed(value);
	}

	public IReadOnlyList<MarkupElement> ChildElements { get; private set; } = [];

	public bool HasElementChildren => ChildElements.Count > 0;

	public void Add(IMarkupChild child) => _children.Add(EnsureUnsealed(child));

	public void Seal()
	{
		_sealed = true;

		// An element without a child keeps the shared empty lists.
		if (_children.Count == 0)
			return;

		List<IMarkupBodyNode> body = new(_children.Count);
		List<IMarkupItem> items = new(_children.Count);
		List<MarkupElement> childElements = [];

		foreach (IMarkupChild child in _children)
		{
			if (child is IMarkupBodyNode node)
				body.Add(node);

			if (child is IMarkupItem item)
				items.Add(item);

			if (child is MarkupElement element)
				childElements.Add(element);
		}

		// Read-only lists, so a cast of what a walk is handed cannot change a tree that other walks have cached.
		Body = [.. body];
		Items = [.. items];
		ChildElements = [.. childElements];

		// The three lists hold every child, so the builder list only costs memory from here on.
		_children.Clear();
		_children.TrimExcess();
	}

	/// <summary>
	/// The parser builds an element and then seals it, so a later write would change what a finished walk read.
	/// </summary>
	private T EnsureUnsealed<T>(T value) => _sealed ? throw new InvalidOperationException($"'{Name}' was sealed before this change.") : value;
}
