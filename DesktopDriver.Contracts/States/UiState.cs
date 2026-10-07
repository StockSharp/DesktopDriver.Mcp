namespace StockSharp.DesktopDriver.States;

using System.Text.Json.Serialization;

/// <summary>
/// What a control holds, in the shape that control's family is read in.
/// </summary>
/// <remarks>
/// Every shape is registered by name. An unknown name is answered with a version error rather than
/// deserialized into whatever CLR type the name happens to resemble: the protocol is a contract, not an
/// invitation to construct types from a string on the wire.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
public abstract record UiState
{
	/// <summary>
	/// The registered wire name of this shape.
	/// </summary>
	public abstract string Kind { get; }
}
