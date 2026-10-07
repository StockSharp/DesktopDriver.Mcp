namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.States;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// Stand-ins for the parts of a real application the runtime talks to.
/// </summary>
/// <remarks>
/// The runtime is meant to work without Avalonia and without any application, which is what lets these
/// tests be ordinary unit tests. They test the runtime; whether a real grid answers correctly is a
/// different question, asked of the adapters against the real control.
/// </remarks>
internal class TestControl
{
	public string Name { get; init; } = "control";
}

internal sealed class DerivedControl : TestControl;

internal interface IFirstThing;

internal interface ISecondThing;

internal interface IBothThings : IFirstThing, ISecondThing;

internal sealed class BothThings : IBothThings;

/// <summary>
/// Runs the read where it stands: a unit test has no dispatcher and needs none.
/// </summary>
internal sealed class ImmediateExecutor : IUiExecutor
{
	public bool CheckAccess() => true;

	public Task<T> InvokeAsync<T>(Func<T> read, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		return Task.FromResult(read());
	}
}

internal sealed class StubPresentationReader : IUiPresentationReader
{
	public UiPresentation Read(UiSubject subject, UiCaptureContext context)
		=> new(
			UiContentStatuses.Created,
			UiField<string>.Known("window:test"),
			UiField<bool>.Known(true),
			UiField<bool>.Known(true),
			UiField<bool>.Known(true),
			UiField<bool>.Known(true),
			UiField<bool>.Known(false),
			UiField<UiRect>.Known(new UiRect(0, 0, 10, 10)),
			UiField<double>.Known(1));
}

internal class StubAdapter(string kind, Type targetType) : IUiSnapshotAdapter
{
	public string Kind { get; } = kind;

	public Type TargetType { get; } = targetType;

	public bool Claims { get; set; } = true;

	public string Text { get; set; } = "stub";

	public int Reads { get; private set; }

	public ImmutableArray<string> Capabilities { get; set; } = ["input.click"];

	public bool CanHandle(UiSubject subject) => Claims && TargetType.IsInstanceOfType(subject.Instance);

	public ImmutableArray<string> GetCapabilities(UiSubject subject) => Capabilities;

	public UiStateCapture CaptureState(UiSubject subject, UiCaptureContext context)
	{
		Reads++;

		return new UiStateCapture(
			new BasicState(
				UiField<string>.Known(Text),
				UiField<UiValue>.Known(new UiStringValue(Text)),
				UiField<bool>.Known(false),
				UiField<bool?>.Known(null),
				UiField<bool>.Known(false),
				UiField<bool>.Known(false),
				UiField<UiNodeId>.Known(null),
				UiField<UiNodeId>.Known(null),
				ImmutableArray<string>.Empty),
			UiReadyStatuses.Ready,
			UiCompleteness.Complete,
			ImmutableArray<string>.Empty);
	}
}

internal sealed class StubContainerAdapter(string kind, Type targetType)
	: StubAdapter(kind, targetType), IUiContainerAdapter
{
	public Func<UiSubject, ImmutableArray<UiChildLink>> Children { get; set; } =
		_ => ImmutableArray<UiChildLink>.Empty;

	public bool ReportTruncated { get; set; }

	public UiDataPage<UiChildLink> ReadChildren(
		UiSubject subject,
		UiPageRequest query,
		UiCaptureContext context)
	{
		var all = Children(subject);
		var page = all.Length > query.Limit ? [.. all[..query.Limit]] : all;

		return new UiDataPage<UiChildLink>(
			context.Node,
			null,
			page,
			UiField<long>.Known(all.Length),
			ReportTruncated || page.Length < all.Length,
			null,
			null);
	}
}

internal sealed class StubRootSource(params UiSubject[] roots) : IUiRootSource
{
	public ImmutableArray<UiSubject> GetRoots(UiReadBudget budget) => [.. roots];

	public ImmutableArray<UiSurfaceInfo> GetSurfaces() => ImmutableArray<UiSurfaceInfo>.Empty;
}
