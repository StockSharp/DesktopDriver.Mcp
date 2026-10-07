namespace StockSharp.DesktopDriver.Cli;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

/// <summary>
/// The command line, read once.
/// </summary>
/// <remarks>
/// Options are <c>--name value</c> or <c>--name=value</c>, and a name given twice is a mistake rather
/// than a last-one-wins: a script that set the same option twice meant one of them, and guessing which
/// is how the wrong window gets clicked.
/// </remarks>
public sealed class UiArguments
{
	private readonly ImmutableDictionary<string, string> _options;
	private readonly ImmutableArray<string> _positional;
	private readonly HashSet<string> _used = new(StringComparer.Ordinal);

	private UiArguments(ImmutableDictionary<string, string> options, ImmutableArray<string> positional)
	{
		_options = options;
		_positional = positional;
	}

	/// <summary>
	/// The words that are not options, in the order they were given.
	/// </summary>
	public ImmutableArray<string> Positional => _positional;

	/// <summary>
	/// Reads a command line.
	/// </summary>
	/// <param name="args">What the program was started with.</param>
	/// <returns>The arguments.</returns>
	public static UiArguments Parse(string[] args)
	{
		var options = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
		var positional = ImmutableArray.CreateBuilder<string>();

		for (var index = 0; index < (args?.Length ?? 0); index++)
		{
			var argument = args[index];

			if (!argument.StartsWith("--", StringComparison.Ordinal))
			{
				positional.Add(argument);
				continue;
			}

			var separator = argument.IndexOf('=');
			string name;
			string value;

			if (separator >= 0)
			{
				name = argument[2..separator];
				value = argument[(separator + 1)..];
			}
			else
			{
				name = argument[2..];
				value = index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal)
					? args[++index]
					: string.Empty;
			}

			if (name.Length == 0)
				throw new UiUsageException($"'{argument}' is not an option.");

			if (options.ContainsKey(name))
				throw new UiUsageException($"--{name} was given more than once.");

			options.Add(name, value);
		}

		return new UiArguments(options.ToImmutable(), positional.ToImmutable());
	}

	/// <summary>
	/// The value of an option that has to be there.
	/// </summary>
	/// <param name="name">Its name.</param>
	/// <returns>Its value.</returns>
	public string Required(string name)
		=> Optional(name) ?? throw new UiUsageException($"--{name} is required.");

	/// <summary>
	/// The value of an option, when it was given.
	/// </summary>
	/// <param name="name">Its name.</param>
	/// <returns>Its value, or <see langword="null"/>.</returns>
	public string Optional(string name)
	{
		_used.Add(name);

		return _options.TryGetValue(name, out var value) && value.Length > 0 ? value : null;
	}

	/// <summary>
	/// Whether an option was given at all.
	/// </summary>
	/// <param name="name">Its name.</param>
	/// <returns><see langword="true"/> when it was.</returns>
	public bool Flag(string name)
	{
		_used.Add(name);

		return _options.ContainsKey(name);
	}

	/// <summary>
	/// A whole number option.
	/// </summary>
	/// <param name="name">Its name.</param>
	/// <param name="fallback">What it is when it was not given.</param>
	/// <returns>Its value.</returns>
	public int Number(string name, int fallback)
	{
		var text = Optional(name);

		if (text is null)
			return fallback;

		return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
			? value
			: throw new UiUsageException($"--{name} takes a whole number, not '{text}'.");
	}

	/// <summary>
	/// A whole number option that may simply not be there.
	/// </summary>
	/// <param name="name">Its name.</param>
	/// <returns>Its value, or <see langword="null"/> when it was not given.</returns>
	public long? Index(string name)
	{
		var text = Optional(name);

		if (text is null)
			return null;

		return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
			? value
			: throw new UiUsageException($"--{name} takes a whole number, not '{text}'.");
	}

	/// <summary>
	/// One of a fixed set of names.
	/// </summary>
	/// <typeparam name="T">The set.</typeparam>
	/// <param name="name">The option's name.</param>
	/// <param name="fallback">What it is when it was not given.</param>
	/// <returns>Its value.</returns>
	public T Choice<T>(string name, T fallback)
		where T : struct, Enum
	{
		var text = Optional(name);

		if (text is null)
			return fallback;

		return Enum.TryParse<T>(text, ignoreCase: true, out var value) && Enum.IsDefined(value)
			? value
			: throw new UiUsageException(
				$"--{name} takes one of {string.Join(", ", Enum.GetNames<T>()).ToLowerInvariant()}, not '{text}'.");
	}

	/// <summary>
	/// A moment in time, as ISO 8601.
	/// </summary>
	/// <param name="name">Its name.</param>
	/// <returns>Its value, or <see langword="null"/> when it was not given.</returns>
	public DateTime? Moment(string name)
	{
		var text = Optional(name);

		if (text is null)
			return null;

		return DateTime.TryParse(
			text,
			CultureInfo.InvariantCulture,
			DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
			out var value)
			? value
			: throw new UiUsageException($"--{name} takes a time such as 2026-01-02T10:00:00Z, not '{text}'.");
	}

	/// <summary>
	/// A comma-separated list.
	/// </summary>
	/// <param name="name">Its name.</param>
	/// <returns>Its items, or empty when it was not given.</returns>
	public ImmutableArray<string> List(string name)
	{
		var text = Optional(name);

		return text is null
			? []
			: [.. text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
	}

	/// <summary>
	/// Refuses anything the command did not ask for.
	/// </summary>
	/// <remarks>
	/// A misspelt option is a script that is not doing what its author thinks. Ignoring it would run the
	/// command anyway, with a default nobody chose.
	/// </remarks>
	public void RefuseUnknown()
	{
		var unknown = _options.Keys.Where(name => !_used.Contains(name)).OrderBy(name => name).ToArray();

		if (unknown.Length > 0)
			throw new UiUsageException($"This command has no option --{string.Join(", --", unknown)}.");
	}
}

/// <summary>
/// The command line is wrong.
/// </summary>
/// <param name="message">What is wrong with it.</param>
public sealed class UiUsageException(string message) : Exception(message);
