namespace StockSharp.DesktopDriver.Mcp;

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ModelContextProtocol.Server;

/// <summary>
/// Tools that find, start and let go of the applications this server can drive.
/// </summary>
/// <remarks>
/// Every other tool names an application by the <c>instance</c> these hand back, so this is where a
/// session begins.
/// </remarks>
[McpServerToolType]
public static class ApplicationTools
{
	[McpServerTool(Name = "ui_list_applications", Title = "List applications", ReadOnly = true)]
	[Description(
		"Lists the applications this machine can drive and the ones already running. Start here: every " +
		"other tool names an application by the instance reported in runningAs. An application that is " +
		"not built cannot be started; it has to be built with the driver in it, which is usually a separate build.")]
	public static UiCatalogueEntryInfo[] ListApplications(UiApplications applications)
	{
		ArgumentNullException.ThrowIfNull(applications);

		var running = applications.Running();

		return
		[
			.. applications.Catalogue().Applications.Select(entry => new UiCatalogueEntryInfo(
				entry.AppId,
				entry.Description,
				File.Exists(entry.Executable),
				[.. running.Where(item => item.AppId == entry.AppId).Select(item => item.Instance)])),
		];
	}

	[McpServerTool(Name = "ui_start_application", Title = "Start an application")]
	[Description(
		"Starts one of the applications from ui_list_applications and waits until its window is drawn " +
		"and it can be driven. Answers with the instance to pass to every other tool. Starting the same " +
		"application twice gives two instances, and the second is named with a #2.")]
	public static Task<UiInstanceInfo> StartApplicationAsync(
		UiApplications applications,
		[Description("Which application, from ui_list_applications.")] string appId,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(applications);

		return applications.StartAsync(appId, cancellationToken);
	}

	[McpServerTool(Name = "ui_attach_application", Title = "Attach to a running application")]
	[Description(
		"Connects to an application somebody else started with --ui-automation. Needs the endpoint file " +
		"it wrote. The local channel is restricted to the current OS user. An " +
		"application attached to this way is never closed by ui_release_application.")]
	public static Task<UiInstanceInfo> AttachApplicationAsync(
		UiApplications applications,
		[Description("The endpoint file the application wrote.")] string endpointFile,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(applications);

		return applications.AttachAsync(endpointFile, cancellationToken);
	}

	[McpServerTool(Name = "ui_release_application", Title = "Let go of an application", Destructive = true)]
	[Description(
		"Lets go of an application. One this server started is closed; one it only attached to is left " +
		"running, because closing it would close a window its owner is working in.")]
	public static Task<string> ReleaseApplicationAsync(
		UiApplications applications,
		[Description("Which instance, from ui_list_applications.")] string instance)
	{
		ArgumentNullException.ThrowIfNull(applications);

		return applications.ReleaseAsync(instance);
	}

	[McpServerTool(Name = "ui_session", Title = "What answered", ReadOnly = true)]
	[Description(
		"What an instance is and what it can do: the product and its version, the fixture it was started " +
		"with, whether the outside world has been replaced, how input is sent and the default read budget.")]
	public static Task<string> SessionAsync(
		UiApplications applications,
		[Description("Which instance.")] string instance,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(applications);

		return applications.AskAsync(instance, async client
			=> UiAnswers.Json(await client.GetSessionAsync(cancellationToken)));
	}
}
