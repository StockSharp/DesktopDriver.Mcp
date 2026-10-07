namespace StockSharp.DesktopDriver.Serialization;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

using StockSharp.DesktopDriver.States;

/// <summary>
/// The state shapes this protocol knows about.
/// </summary>
/// <remarks>
/// The shared shapes are known from the start; a product layer adds its own - a diagram, an order book -
/// by registering them here before anything is serialised. Registration is explicit so that an unknown
/// name on the wire is a version error, not an instruction to build whichever CLR type it names.
/// </remarks>
public static class UiStateRegistry
{
	private static readonly Lock _sync = new();
	private static ImmutableDictionary<string, Type> _byKind = ImmutableDictionary<string, Type>.Empty;

	static UiStateRegistry()
	{
		Register(UiStateKinds.Basic, typeof(BasicState));
		Register(UiStateKinds.Grid, typeof(GridState));
		Register(UiStateKinds.Chart, typeof(ChartState));
		Register(UiStateKinds.Dock, typeof(DockState));
		Register(UiStateKinds.OrderBook, typeof(OrderBookState));
		Register(UiStateKinds.PropertyEditor, typeof(PropertyEditorState));
		Register(UiStateKinds.Tree, typeof(TreeState));
		Register(UiStateKinds.List, typeof(ListState));
		Register(UiStateKinds.Document, typeof(DocumentState));
		Register(UiStateKinds.Diagram, typeof(DiagramState));
		Register(UiStateKinds.Panel, typeof(PanelState));
	}

	/// <summary>
	/// The registered shapes, by wire name.
	/// </summary>
	public static ImmutableDictionary<string, Type> Registered => _byKind;

	/// <summary>
	/// Registers one state shape.
	/// </summary>
	/// <param name="kind">Its wire name.</param>
	/// <param name="type">Its type, derived from <see cref="UiState"/>.</param>
	/// <remarks>
	/// Registering the same pair twice is allowed and does nothing: two modules may both depend on the
	/// layer that owns a shape. Registering the same name for a different type is a mistake worth
	/// refusing, because whichever won would silently change what the protocol means.
	/// </remarks>
	public static void Register(string kind, Type type)
	{
		ArgumentException.ThrowIfNullOrEmpty(kind);
		ArgumentNullException.ThrowIfNull(type);

		if (!type.IsSubclassOf(typeof(UiState)))
			throw new ArgumentException($"{type.Name} is not a state shape.", nameof(type));

		using (_sync.EnterScope())
		{
			if (_byKind.TryGetValue(kind, out var existing))
			{
				if (existing == type)
					return;

				throw new InvalidOperationException(
					$"State shape '{kind}' is already registered as {existing.Name}.");
			}

			_byKind = _byKind.Add(kind, type);
		}
	}

	/// <summary>
	/// Looks up a registered shape.
	/// </summary>
	/// <param name="kind">Its wire name.</param>
	/// <returns>The type, or <see langword="null"/> when nothing is registered under that name.</returns>
	public static Type Resolve(string kind)
		=> kind is not null && _byKind.TryGetValue(kind, out var type) ? type : null;
}
