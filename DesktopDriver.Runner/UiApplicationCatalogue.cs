namespace StockSharp.DesktopDriver.Runner;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// The applications a caller may start, and where they are.
/// </summary>
/// <remarks>
/// Written by whoever set the machine up, never by the caller. Naming an application from a list is what
/// keeps automation from being a way to run anything: a path that arrived with the request would make it
/// exactly that.
/// </remarks>
public sealed class UiApplicationCatalogue
{
	private static readonly JsonSerializerOptions _json = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
	};

	private sealed record Document(
		[property: JsonPropertyName("applications")] ImmutableArray<UiApplicationEntry> Applications);

	private readonly ImmutableDictionary<string, UiApplicationEntry> _byAppId;

	private UiApplicationCatalogue(ImmutableDictionary<string, UiApplicationEntry> byAppId, string path)
	{
		_byAppId = byAppId;
		Path = path;
	}

	/// <summary>
	/// Where this catalogue was read from.
	/// </summary>
	public string Path { get; }

	/// <summary>
	/// What it holds, in the order it lists them.
	/// </summary>
	public ImmutableArray<UiApplicationEntry> Applications => [.. _byAppId.Values.OrderBy(entry => entry.AppId, StringComparer.Ordinal)];

	/// <summary>
	/// Reads a catalogue.
	/// </summary>
	/// <param name="path">Where it is.</param>
	/// <returns>The catalogue.</returns>
	public static UiApplicationCatalogue Open(string path)
	{
		ArgumentException.ThrowIfNullOrEmpty(path);

		var full = System.IO.Path.GetFullPath(path);

		if (!File.Exists(full))
			throw new FileNotFoundException($"There is no catalogue of applications at {full}.", full);

		var document = JsonSerializer.Deserialize<Document>(File.ReadAllText(full), _json)
			?? throw new InvalidOperationException($"{full} is empty.");

		var entries = ImmutableDictionary.CreateBuilder<string, UiApplicationEntry>(StringComparer.Ordinal);

		foreach (var entry in document.Applications.IsDefault ? [] : document.Applications)
		{
			if (string.IsNullOrEmpty(entry.AppId) || string.IsNullOrEmpty(entry.Executable))
				throw new InvalidOperationException($"{full} lists an application with no identifier or no executable.");

			if (entries.ContainsKey(entry.AppId))
				throw new InvalidOperationException($"{full} lists {entry.AppId} twice.");

			// Resolved against the catalogue rather than against whatever the working directory happens to
			// be, so the same file works from anywhere.
			var executable = System.IO.Path.IsPathRooted(entry.Executable)
				? entry.Executable
				: System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(full), entry.Executable));

			entries.Add(entry.AppId, entry with { Executable = executable });
		}

		return new UiApplicationCatalogue(entries.ToImmutable(), full);
	}

	/// <summary>
	/// Finds an application by name.
	/// </summary>
	/// <param name="appId">Its identifier.</param>
	/// <returns>The entry.</returns>
	public UiApplicationEntry Find(string appId)
	{
		ArgumentException.ThrowIfNullOrEmpty(appId);

		if (_byAppId.TryGetValue(appId, out var entry))
			return entry;

		var known = _byAppId.Count == 0
			? "it lists none"
			: $"it lists {string.Join(", ", Applications.Select(item => item.AppId))}";

		throw new KeyNotFoundException($"The catalogue has no application called {appId}; {known}.");
	}
}
