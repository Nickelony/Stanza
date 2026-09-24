namespace Nickelony.Stanza.Markup.Collection;

/// <summary>Why a file the collection reached gives no input, a link aside.</summary>
internal enum MarkupSkipReason
{
	/// <summary>An <c>.editorconfig</c> turns the file off.</summary>
	Disabled,

	/// <summary>No profile or <c>stanza_markup_profile</c> entry claims the file.</summary>
	Unclaimed,
}
