namespace StockSharp.DesktopDriver.Adapters;

using System;

/// <summary>
/// A set of adapters that belong together and are registered together.
/// </summary>
/// <remarks>
/// One module per library of controls. An application composes the modules it needs, and the lease it
/// gets back removes exactly that module's registrations - not another module's identical ones.
/// </remarks>
public interface IUiAutomationModule
{
	/// <summary>
	/// The module's identity.
	/// </summary>
	string ModuleId { get; }

	/// <summary>
	/// Registers the module's adapters.
	/// </summary>
	/// <param name="registry">Where to register them.</param>
	/// <returns>A lease that removes them when it is disposed.</returns>
	IDisposable Register(IUiAdapterRegistry registry);
}
