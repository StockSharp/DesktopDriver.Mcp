namespace StockSharp.DesktopDriver.Queries;

using System.Collections.Immutable;

using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// Which properties of an editor to read.
/// </summary>
/// <param name="Paths">The properties wanted, or empty for all of them.</param>
/// <param name="Page">Which page.</param>
/// <param name="Guard">What the caller believes it is reading.</param>
/// <remarks>
/// What comes back is what the editor is showing: its own properties, after its search filter, and the
/// properties of the ones the user has opened. A property nobody has opened has no properties yet - the
/// editor builds them when it is opened - and opening one is something a person does, not something a
/// read does.
/// </remarks>
public sealed record PropertyEditorItemsQuery(
	ImmutableArray<string> Paths,
	UiPageRequest Page,
	UiReadGuard Guard);
