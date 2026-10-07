namespace StockSharp.DesktopDriver.Maui;

using System;
using System.Collections.Generic;
using System.Threading;

using Microsoft.Maui.Controls;

/// <summary>
/// Rules that name the scope an element opens without the element declaring one itself: a module that
/// knows a family of controls says which of them are worth addressing against rather than against the
/// window.
/// </summary>
public static class UiAutomationScopes
{
	private static readonly List<Func<Element, string>> _rules = [];
	private static readonly Lock _sync = new();

	/// <summary>
	/// Adds a rule.
	/// </summary>
	/// <param name="rule">Answers the scope an element opens, or <see langword="null"/> when it opens none.</param>
	/// <returns>Takes the rule away again.</returns>
	public static IDisposable Add(Func<Element, string> rule)
	{
		ArgumentNullException.ThrowIfNull(rule);

		using (_sync.EnterScope())
			_rules.Add(rule);

		return new Removal(rule);
	}

	/// <summary>
	/// The scope an element opens by any of the rules.
	/// </summary>
	/// <param name="element">The element.</param>
	/// <returns>The scope, or <see langword="null"/> when no rule names one.</returns>
	public static string Of(Element element)
	{
		if (element is null)
			return null;

		Func<Element, string>[] rules;

		using (_sync.EnterScope())
		{
			if (_rules.Count == 0)
				return null;

			rules = [.. _rules];
		}

		foreach (var rule in rules)
		{
			if (rule(element) is { Length: > 0 } scope)
				return scope;
		}

		return null;
	}

	private sealed class Removal(Func<Element, string> rule) : IDisposable
	{
		public void Dispose()
		{
			using (_sync.EnterScope())
				_rules.Remove(rule);
		}
	}
}
