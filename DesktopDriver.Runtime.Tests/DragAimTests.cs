namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// A drag is aimed at both of its ends before anything is pressed.
/// </summary>
/// <remarks>
/// A drag whose far end turned out not to be there would already have taken hold of whatever it was pressed on, and
/// letting go of it somewhere else is an action of its own - an order moved to a price nobody asked for.
/// </remarks>
[TestClass]
public class DragAimTests : BaseTestClass
{
	private static readonly UiNodeId _id = new("window:test", "chart");

	[TestMethod]
	public async Task ADragIsAimedAtBothOfItsEnds()
	{
		var driver = new RecordingDriver();
		var dispatcher = Create(new PriceResolver(), driver);

		var receipt = await dispatcher.ExecuteAsync(
			Request(new UiChartPricePart("Area0", 10m), new UiDragAction(new UiChartPricePart("Area0", 40m))),
			CancellationToken);

		AreEqual(UiActionStatuses.Dispatched, receipt.Status);
		AreEqual(new UiPoint(5, 10), driver.Received.PointInSurfaceDip);
		AreEqual(new UiPoint(5, 40), driver.Received.DestinationInSurfaceDip);
	}

	[TestMethod]
	public async Task ADragWhoseFarEndCannotBeAimedAtIsNotSent()
	{
		var driver = new RecordingDriver();
		var dispatcher = Create(new PriceResolver(), driver);

		await ThrowsAsync<UiAutomationException>(() => dispatcher.ExecuteAsync(
			Request(new UiChartPricePart("Area0", 10m), new UiDragAction(new UiChartPricePart("Area0", PriceResolver.Missing))),
			CancellationToken));

		AreEqual(0, driver.Sent, "A drag was pressed although where it was to be let go is not there.");
	}

	[TestMethod]
	public async Task OnlyADragIsGivenAPlaceToLetGo()
	{
		var driver = new RecordingDriver();
		var dispatcher = Create(new PriceResolver(), driver);

		await dispatcher.ExecuteAsync(
			Request(new UiChartPricePart("Area0", 10m), new UiClickAction(UiPointerButtons.Left, 1, UiKeyModifiers.Control)),
			CancellationToken);

		IsNull(driver.Received.DestinationInSurfaceDip);
	}

	private static UiInputRequest Request(UiTargetPart part, UiInputAction action)
		=> new(Guid.NewGuid(), UiTarget.FromId(_id), part, action, null, TimeSpan.FromSeconds(5));

	private static UiInputDispatcher Create(PriceResolver resolver, RecordingDriver driver)
	{
		var nodes = new UiNodeRegistry(Guid.NewGuid());
		var revisions = new UiRevisionTracker();

		nodes.Register(new UiSubject(_id, new TestControl()), "chart", true);
		resolver.Nodes = nodes;
		resolver.Revisions = revisions;

		return new UiInputDispatcher(
			nodes,
			new UiAdapterRegistry(),
			revisions,
			resolver,
			driver,
			new ImmediateExecutor(),
			new UiActionJournal());
	}

	// Puts a price as high up as its value, and finds nothing at the one price the cases call missing.
	private sealed class PriceResolver : IUiInputTargetResolver
	{
		public const decimal Missing = 999m;

		public UiNodeRegistry Nodes { get; set; }

		public UiRevisionTracker Revisions { get; set; }

		public UiResolvedInputTarget Resolve(UiSubject subject, UiTargetPart part, UiInputAction action, UiCaptureContext context)
		{
			var price = ((UiChartPricePart)part).Price;

			if (price == Missing)
				throw UiErrors.NotFound($"Nothing is drawn at {price}.");

			return new(Nodes.GetReference(subject.Id), "window:test", new UiRect(0, 0, 100, 100), new UiPoint(5, (double)price), Revisions.Read(subject.Id));
		}
	}

	private sealed class RecordingDriver : IUiInputDriver
	{
		public UiResolvedInputTarget Received { get; private set; }

		public int Sent { get; private set; }

		public string BackendId => "test";

		public ImmutableArray<string> Capabilities { get; } = ["input.click", "input.drag"];

		public UiError Refusal => null;

		public Task DrawnAsync(UiTarget target, CancellationToken cancellationToken) => Task.CompletedTask;

		public Task<UiActionReceipt> ExecuteAsync(UiInputRequest request, UiResolvedInputTarget resolvedTarget, CancellationToken cancellationToken)
		{
			Sent++;
			Received = resolvedTarget;

			return Task.FromResult(new UiActionReceipt(
				request.ActionId,
				UiActionStatuses.Dispatched,
				BackendId,
				UiField<UiRevisions>.Unavailable(UiUnavailableReasons.Unknown, null),
				UiField<UiRevisions>.Unavailable(UiUnavailableReasons.Unknown, null),
				true,
				null));
		}
	}
}
