namespace StockSharp.DesktopDriver.Host;

using System;
using System.Threading;
using System.Threading.Tasks;

using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Runtime;

/// <summary>
/// What an interface toolkit brings to a driven application.
/// </summary>
/// <param name="Executor">Runs reads and input on the toolkit's interface thread.</param>
/// <param name="Roots">The windows and popups the application has open.</param>
/// <param name="PresentationReader">Reads where a control is and whether it is shown.</param>
/// <param name="InputTargets">Finds where on screen a part of a control is.</param>
/// <param name="CreateInputDriver">Creates the backend that sends input; asked only for a run that may be driven.</param>
/// <param name="Screenshots">Takes pictures of controls and windows.</param>
/// <param name="Version">The toolkit's version, as the session reports it.</param>
public sealed record UiToolkitParts(
	IUiExecutor Executor,
	IUiRootSource Roots,
	IUiPresentationReader PresentationReader,
	IUiInputTargetResolver InputTargets,
	Func<IUiInputDriver> CreateInputDriver,
	Func<UiScreenshotRequest, CancellationToken, Task<UiScreenshotInfo>> Screenshots,
	string Version);
