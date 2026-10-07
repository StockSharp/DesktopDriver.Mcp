namespace StockSharp.DesktopDriver.Host;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

/// <summary>
/// Several module registrations held as one.
/// </summary>
/// <param name="registrations">The registrations, in the order they were made.</param>
/// <remarks>
/// An application registers a set of modules and later takes the set down again. Undoing them one by one
/// is how one gets left behind, still holding subscriptions to a window nobody is looking at.
/// </remarks>
public sealed class UiCompositeRegistration(params IDisposable[] registrations) : IDisposable
{
	private readonly ImmutableArray<IDisposable> _registrations = [.. registrations ?? []];

	/// <summary>
	/// Undoes every registration, the last made first.
	/// </summary>
	/// <exception cref="AggregateException">One or more registrations could not be undone; the rest were.</exception>
	public void Dispose()
	{
		List<Exception> failures = null;

		for (var index = _registrations.Length - 1; index >= 0; index--)
		{
			try
			{
				_registrations[index].Dispose();
			}
			catch (Exception error)
			{
				// One module that cannot unregister must not leave the others registered.
				(failures ??= []).Add(error);
			}
		}

		if (failures is not null)
			throw new AggregateException("Not every module could be unregistered.", failures);
	}
}
