namespace StockSharp.DesktopDriver.Mcp;

using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Runner;

/// <summary>
/// Tools that take a picture of what is on screen.
/// </summary>
/// <remarks>
/// A picture is written to a file and its details are answered; the image itself only travels when it
/// is asked for. A megabyte of image on every screenshot is a cost the caller should be choosing to pay.
/// <para>
/// A picture is for a person to look at, not for a test to measure. What a control holds is read with
/// the reading tools, which say so in words that do not change with a theme or a font.
/// </para>
/// </remarks>
[McpServerToolType]
public static class PictureTools
{
	[McpServerTool(Name = "ui_screenshot", Title = "Take a picture", ReadOnly = true)]
	[Description(
		"Takes a picture of a node and writes it to a file, answering with the file's path and the " +
		"picture's details. Use ui_screenshot_image when the picture itself is needed rather than the " +
		"file. To check what a control holds, read it instead — a picture cannot be compared reliably.")]
	public static Task<string> ScreenshotAsync(
		UiApplications applications,
		UiSettings settings,
		[Description("Which instance.")] string instance,
		[Description("Which node, as scope/identifier.")] string node,
		[Description("controlRender, windowCapture or screenCapture; controlRender by default.")] string captureKind = null,
		[Description("Refuse rather than take a picture wider than this, in pixels.")] int maxPixelWidth = 0,
		[Description("Refuse rather than take a picture taller than this, in pixels.")] int maxPixelHeight = 0,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(applications);
		ArgumentNullException.ThrowIfNull(settings);

		var request = Request(node, captureKind, maxPixelWidth, maxPixelHeight);

		return applications.AskAsync(instance, async client =>
		{
			var info = await client.CaptureScreenshotAsync(request, cancellationToken);
			var path = Path.Combine(settings.ArtifactDirectory, $"{info.ArtifactId}.png");

			await UiArtifactFile.SaveAsync(client, info.ArtifactId, path, cancellationToken);

			return UiAnswers.Json(new UiPictureInfo(info, path));
		});
	}

	[McpServerTool(Name = "ui_screenshot_image", Title = "Take a picture and show it", ReadOnly = true)]
	[Description(
		"Takes a picture of a node and answers with the image itself, to be looked at. The whole image " +
		"travels back, so aim it at the control in question rather than at a whole window. A size limit " +
		"refuses an oversized picture rather than shrinking it: a shrunk picture cannot be measured.")]
	public static Task<ImageContentBlock> ScreenshotImageAsync(
		UiApplications applications,
		[Description("Which instance.")] string instance,
		[Description("Which node, as scope/identifier.")] string node,
		[Description("controlRender, windowCapture or screenCapture; controlRender by default.")] string captureKind = null,
		[Description("Refuse rather than take a picture wider than this, in pixels.")] int maxPixelWidth = 0,
		[Description("Refuse rather than take a picture taller than this, in pixels.")] int maxPixelHeight = 0,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(applications);

		var request = Request(node, captureKind, maxPixelWidth, maxPixelHeight);

		return applications.AskAsync(instance, async client =>
		{
			var info = await client.CaptureScreenshotAsync(request, cancellationToken);
			var bytes = await UiArtifactFile.ReadAsync(client, info.ArtifactId, cancellationToken);

			return ImageContentBlock.FromBytes(bytes, info.MimeType);
		});
	}

	private static UiScreenshotRequest Request(
		string node,
		string captureKind,
		int maxPixelWidth,
		int maxPixelHeight)
		=> new(
			UiAnswers.Node(node),
			string.IsNullOrEmpty(captureKind) ? UiCaptureKinds.ControlRender : captureKind,
			false,
			maxPixelWidth > 0 ? maxPixelWidth : 0,
			maxPixelHeight > 0 ? maxPixelHeight : 0,
			null,
			[]);
}
