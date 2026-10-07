namespace StockSharp.DesktopDriver.Testing;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Threading;

using StockSharp.DesktopDriver.Runner;

/// <summary>
/// One registered way of starting a product for a test.
/// </summary>
/// <param name="RecipeId">What a request names this recipe by.</param>
/// <param name="AppId">The product it starts.</param>
/// <param name="ExecutableKey">The build-time key naming the executable's path.</param>
/// <param name="FixtureId">The fixture the product shows when started this way.</param>
/// <param name="UsesTestProfile">Whether it is started on a settings directory made for the run.</param>
/// <remarks>
/// A request names a recipe; it never carries a command. The difference is the whole point: a caller
/// that could pass an executable and its arguments could start anything on the machine, and a protocol
/// reachable by an agent must not be able to do that.
/// </remarks>
public sealed record UiLaunchRecipe(
	string RecipeId,
	string AppId,
	string ExecutableKey,
	string FixtureId,
	bool UsesTestProfile);

/// <summary>
/// The recipes a test assembly allows.
/// </summary>
/// <remarks>
/// Filled by the test assembly that owns the products, because it is the one that knows where they were
/// built. Nothing outside adds to it, and a request for a recipe that is not here is refused rather than
/// guessed at.
/// </remarks>
public static class UiLaunchRecipes
{
	private static readonly Dictionary<string, UiLaunchRecipe> _byId = new(StringComparer.Ordinal);
	private static readonly Dictionary<string, Assembly> _owners = new(StringComparer.Ordinal);
	private static readonly Lock _sync = new();

	/// <summary>
	/// Every registered recipe.
	/// </summary>
	public static ImmutableArray<UiLaunchRecipe> All
	{
		get
		{
			using (_sync.EnterScope())
				return [.. _byId.Values];
		}
	}

	/// <summary>
	/// Registers one way of starting a product.
	/// </summary>
	/// <param name="recipe">The recipe.</param>
	/// <param name="owner">The assembly that knows where the executable was built.</param>
	public static void Register(UiLaunchRecipe recipe, Assembly owner)
	{
		ArgumentNullException.ThrowIfNull(recipe);
		ArgumentNullException.ThrowIfNull(owner);
		ArgumentException.ThrowIfNullOrEmpty(recipe.RecipeId);

		using (_sync.EnterScope())
		{
			_byId[recipe.RecipeId] = recipe;
			_owners[recipe.RecipeId] = owner;
		}
	}

	/// <summary>
	/// Finds a recipe.
	/// </summary>
	/// <param name="recipeId">What the request named.</param>
	/// <param name="recipe">The recipe.</param>
	/// <param name="owner">The assembly that knows where its executable is.</param>
	/// <returns><see langword="true"/> when there is one.</returns>
	public static bool TryGet(string recipeId, out UiLaunchRecipe recipe, out Assembly owner)
	{
		recipe = null;
		owner = null;

		if (string.IsNullOrEmpty(recipeId))
			return false;

		using (_sync.EnterScope())
			return _byId.TryGetValue(recipeId, out recipe) && _owners.TryGetValue(recipeId, out owner);
	}

	/// <summary>
	/// Where the executable of a recipe was built.
	/// </summary>
	/// <param name="recipe">The recipe.</param>
	/// <param name="owner">The assembly that names it.</param>
	/// <returns>The path.</returns>
	public static string ExecutableOf(UiLaunchRecipe recipe, Assembly owner)
	{
		ArgumentNullException.ThrowIfNull(recipe);

		return DrivenApplication.Executable(owner, recipe.ExecutableKey);
	}
}
