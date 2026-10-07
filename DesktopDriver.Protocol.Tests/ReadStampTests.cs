namespace StockSharp.DesktopDriver.Tests.Protocol;

using System;
using System.Threading;
using System.Threading.Tasks;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// What a paged read says about when it was read.
/// </summary>
/// <remarks>
/// A page is one answer out of a control that keeps moving, and the caller's next call - the next page,
/// or a wait for the control to move on - has to be able to say which state this one came from. Without a
/// stamp there is nothing to say it about: the second page could come from a table that was re-sorted in
/// between, and nothing in either answer would show it.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class ReadStampTests : BaseTestClass
{
	private static Task RunAsync(Func<Task> body)
		=> AssemblyInitializer.Session.Dispatch(async () =>
		{
			await body();
			return true;
		}, CancellationToken.None);

	[TestMethod]
	[Timeout(60000)]
	public Task APagedReadSaysWhenItWasRead() => RunAsync(async () =>
	{
		await using var hosted = await HostedApplication.StartAsync(CancellationToken.None);

		var page = await hosted.Service.ReadTreeItemsAsync(
			UiTarget.FromId(hosted.TreeId),
			new TreeItemsQuery(null, [], new UiPageRequest(10, null), null),
			CancellationToken.None);

		IsNotNull(page.Stamp, "A page with no stamp cannot be continued or waited past.");
		IsNotNull(page.Stamp.Revisions);
		AreEqual(hosted.Session.InstanceId, page.Stamp.InstanceId);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task TwoPagesOfTheSameUnchangedControlAgreeOnTheRevision() => RunAsync(async () =>
	{
		// The point of the stamp: a caller reading page after page can tell that what it is paging through
		// has not moved underneath it.
		await using var hosted = await HostedApplication.StartAsync(CancellationToken.None);

		var query = new TreeItemsQuery(null, [], new UiPageRequest(10, null), null);
		var target = UiTarget.FromId(hosted.TreeId);

		var first = await hosted.Service.ReadTreeItemsAsync(target, query, CancellationToken.None);
		var second = await hosted.Service.ReadTreeItemsAsync(target, query, CancellationToken.None);

		AreEqual(first.Stamp.Revisions, second.Stamp.Revisions);
		AreNotEqual(first.Stamp.SnapshotId, second.Stamp.SnapshotId);
	});
}
