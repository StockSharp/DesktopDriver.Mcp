namespace StockSharp.DesktopDriver.Mcp;

using System;
using System.Collections.Immutable;

using ModelContextProtocol;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Serialization;

/// <summary>
/// What the tools of this server hand back, and how they read what they were given.
/// </summary>
/// <remarks>
/// An answer is the protocol's own JSON, written by the protocol's own serializer. Building a second
/// shape here would be a second opinion about the protocol: the two would agree until the day one of
/// them changed, and the agent would be the last to find out.
/// </remarks>
internal static class UiAnswers
{
	public static string Json<T>(T value) => UiJson.Write(value);

	public static UiTarget Node(string node)
		=> UiTarget.FromId(NodeId(node));

	public static UiNodeId NodeId(string node)
	{
		if (string.IsNullOrEmpty(node))
			return null;

		var separator = node.IndexOf('/');

		if (separator <= 0 || separator == node.Length - 1)
		{
			throw new McpException(
				$"'{node}' is not an address. An address is a scope and an identifier written as " +
				"scope/identifier, the way ui_find and ui_windows report them.");
		}

		return new UiNodeId(node[..separator], node[(separator + 1)..]);
	}

	public static UiPageRequest Page(int limit, string cursor)
		=> new(limit > 0 ? limit : UiPageRequest.Default.Limit, string.IsNullOrEmpty(cursor) ? null : cursor);

	public static ImmutableArray<string> List(string commaSeparated)
		=> string.IsNullOrEmpty(commaSeparated)
			? []
			: [.. commaSeparated.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

	public static Guid Identifier(string text, string what)
		=> Guid.TryParse(text, out var value)
			? value
			: throw new McpException($"'{text}' is not {what}; those are identifiers such as {Guid.Empty:D}.");
}
