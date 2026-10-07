namespace StockSharp.DesktopDriver.Mcp;

using System;
using System.IO;
using System.Reflection;

/// <summary>
/// What this server was set up with.
/// </summary>
/// <remarks>
/// All of it comes from the environment, written by whoever installed the server, and none of it from
/// the agent. Which applications may be started is exactly the kind of decision that has to be made
/// before the agent is in the room.
/// </remarks>
public sealed class UiSettings
{
	/// <summary>
	/// The variable naming the catalogue of applications this server may start.
	/// </summary>
	public const string CatalogueVariable = "STOCKSHARP_UI_CATALOGUE";

	/// <summary>
	/// The variable naming where pictures and other artifacts are written.
	/// </summary>
	public const string ArtifactsVariable = "STOCKSHARP_UI_ARTIFACTS";

	/// <summary>
	/// The name looked for beside the server when the variable is not set.
	/// </summary>
	public const string CatalogueFileName = "applications.json";

	/// <summary>
	/// Initializes a new instance of the <see cref="UiSettings"/> class.
	/// </summary>
	public UiSettings()
	{
		var home = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

		CataloguePath = Environment.GetEnvironmentVariable(CatalogueVariable)
			?? Path.Combine(home, CatalogueFileName);

		// Beside the server rather than in the user's profile: a picture belongs with the thing that made
		// it, where it can be found, cleared out and left out of backups as one.
		ArtifactDirectory = Environment.GetEnvironmentVariable(ArtifactsVariable)
			?? Path.Combine(home, "artifacts");
	}

	/// <summary>
	/// Where the catalogue of applications is.
	/// </summary>
	public string CataloguePath { get; }

	/// <summary>
	/// Where pictures and other artifacts are written.
	/// </summary>
	public string ArtifactDirectory { get; }

	/// <summary>
	/// How long an application is given to come up and open its endpoint.
	/// </summary>
	public TimeSpan StartupPatience { get; } = TimeSpan.FromSeconds(90);

	/// <summary>
	/// How long the connection itself is given.
	/// </summary>
	public TimeSpan ConnectPatience { get; } = TimeSpan.FromSeconds(15);
}
