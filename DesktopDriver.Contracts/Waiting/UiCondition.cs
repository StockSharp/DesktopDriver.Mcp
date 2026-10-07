namespace StockSharp.DesktopDriver.Waiting;

using System.Collections.Immutable;
using System.Text.Json.Serialization;

using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// What is being waited for.
/// </summary>
/// <remarks>
/// A small fixed set, deliberately. Waiting is the one place where a test would be tempted to send code
/// to the application, and a protocol that accepted code would have to run it.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(UiExistsCondition), UiExistsCondition.KindName)]
[JsonDerivedType(typeof(UiFieldCondition), UiFieldCondition.KindName)]
[JsonDerivedType(typeof(UiRevisionAfterCondition), UiRevisionAfterCondition.KindName)]
[JsonDerivedType(typeof(UiAllCondition), UiAllCondition.KindName)]
[JsonDerivedType(typeof(UiAnyCondition), UiAnyCondition.KindName)]
public abstract record UiCondition
{
	/// <summary>
	/// The registered wire name of this condition shape.
	/// </summary>
	public abstract string Kind { get; }
}

/// <summary>The node exists, or does not.</summary>
/// <param name="Expected">Which of the two is being waited for.</param>
public sealed record UiExistsCondition(bool Expected) : UiCondition
{
	/// <summary>The wire name.</summary>
	public const string KindName = "exists";

	/// <inheritdoc />
	public override string Kind => KindName;
}

/// <summary>A field of the node's schema compares as expected.</summary>
/// <param name="Path">The registered schema path, such as <c>state.sorts[0].direction</c>.</param>
/// <param name="Comparison">How to compare.</param>
/// <param name="Expected">What to compare with.</param>
public sealed record UiFieldCondition(
	string Path,
	UiComparisons Comparison,
	UiValue Expected) : UiCondition
{
	/// <summary>The wire name.</summary>
	public const string KindName = "field";

	/// <inheritdoc />
	public override string Kind => KindName;
}

/// <summary>The node has moved on from a revision the caller saw.</summary>
/// <param name="RevisionKind">Which revision.</param>
/// <param name="Baseline">The revision the caller saw.</param>
/// <remarks>
/// Only comparable inside one epoch: a source that was replaced has not moved on, it has been replaced,
/// and the caller needs a new baseline rather than an answer.
/// </remarks>
public sealed record UiRevisionAfterCondition(
	UiRevisionKinds RevisionKind,
	UiRevision Baseline) : UiCondition
{
	/// <summary>The wire name.</summary>
	public const string KindName = "revisionAfter";

	/// <inheritdoc />
	public override string Kind => KindName;
}

/// <summary>Every condition holds.</summary>
/// <param name="Conditions">The conditions.</param>
public sealed record UiAllCondition(ImmutableArray<UiCondition> Conditions) : UiCondition
{
	/// <summary>The wire name.</summary>
	public const string KindName = "all";

	/// <inheritdoc />
	public override string Kind => KindName;
}

/// <summary>At least one condition holds.</summary>
/// <param name="Conditions">The conditions.</param>
public sealed record UiAnyCondition(ImmutableArray<UiCondition> Conditions) : UiCondition
{
	/// <summary>The wire name.</summary>
	public const string KindName = "any";

	/// <inheritdoc />
	public override string Kind => KindName;
}
