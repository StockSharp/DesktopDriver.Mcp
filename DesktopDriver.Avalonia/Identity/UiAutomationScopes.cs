namespace StockSharp.DesktopDriver.Avalonia;

using System;
using System.Collections.Generic;
using System.Threading;

using global::Avalonia.Controls;

/// <summary>
/// Rules that say which controls are roots of a scope, for the ones that do not declare it themselves.
/// </summary>
/// <remarks>
/// A panel, a dialog or a document is the thing a caller means when it says where a control is, and its
/// contents keep their addresses when it is moved to another window. Most such roots can declare it on
/// themselves, but a whole family of them - every panel of a product, say - is better answered once by
/// whoever knows the family than repeated on each one.
/// <para>
/// A rule is registered by the module that knows the family and removed when that module goes, so a build
/// with no such module in it behaves as though the family did not exist.
/// </para>
/// </remarks>
public static class UiAutomationScopes
{
	private static readonly List<Func<Control, string>> _rules = [];
	private static readonly Lock _sync = new();

	/// <summary>
	/// Adds a rule.
	/// </summary>
	/// <param name="rule">Given a control, the scope it is the root of, or <see langword="null"/>.</param>
	/// <returns>What removes the rule again.</returns>
	public static IDisposable Add(Func<Control, string> rule)
	{
		ArgumentNullException.ThrowIfNull(rule);

		using (_sync.EnterScope())
			_rules.Add(rule);

		return new Removal(rule);
	}

	/// <summary>
	/// The scope a control is the root of, according to the rules.
	/// </summary>
	/// <param name="control">The control.</param>
	/// <returns>The scope, or <see langword="null"/> when no rule claims it.</returns>
	public static string Of(Control control)
	{
		if (control is null)
			return null;

		Func<Control, string>[] rules;

		using (_sync.EnterScope())
		{
			if (_rules.Count == 0)
				return null;

			rules = [.. _rules];
		}

		foreach (var rule in rules)
		{
			if (rule(control) is { Length: > 0 } scope)
				return scope;
		}

		return null;
	}

	private sealed class Removal(Func<Control, string> rule) : IDisposable
	{
		public void Dispose()
		{
			using (_sync.EnterScope())
				_rules.Remove(rule);
		}
	}
}
