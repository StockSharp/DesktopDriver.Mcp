namespace StockSharp.DesktopDriver.Protocol;

/// <summary>
/// How a runner starts an application to be driven, and what the application looks for when it starts.
/// </summary>
/// <remarks>
/// The application is started with <see cref="EnableSwitch"/> and <see cref="EndpointSwitch"/> on its command
/// line. Once its endpoint is listening it writes the endpoint to the file the switch named, and the
/// runner opens a session over a local pipe restricted to the current user.
/// </remarks>
public static class UiLaunchProtocol
{
	/// <summary>
	/// The switch that turns automation on.
	/// </summary>
	public const string EnableSwitch = "--ui-automation";

	/// <summary>
	/// The switch that says where to write the endpoint once it is open; the path follows the equals sign.
	/// </summary>
	public const string EndpointSwitch = "--ui-automation-endpoint=";
}
