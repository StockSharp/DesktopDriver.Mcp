namespace StockSharp.DesktopDriver.Queries;

using StockSharp.DesktopDriver.Identity;

/// <summary>
/// A bounded walk of the meaning-level tree.
/// </summary>
/// <param name="Root">Where to start; absent means the application's surfaces.</param>
/// <param name="IncludeVisualInternals">Whether to include template parts, for diagnosis only.</param>
/// <param name="Options">How much to read of each node.</param>
/// <remarks>
/// The default tree is the one a person would draw: windows, panels, the controls in them. Template
/// borders and presenters are visual scaffolding and are only included when something is being diagnosed.
/// </remarks>
public sealed record UiTreeQuery(
	UiTarget Root,
	bool IncludeVisualInternals,
	UiCaptureOptions Options);
