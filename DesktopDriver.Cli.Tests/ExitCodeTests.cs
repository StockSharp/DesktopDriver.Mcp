namespace StockSharp.DesktopDriver.Tests.Cli;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Cli;
using StockSharp.DesktopDriver.Diagnostics;

/// <summary>
/// What a script reads off this program when it is done.
/// </summary>
/// <remarks>
/// The numbers are the contract. A refusal and an application that is not running must never share one,
/// because the first is a script's own mistake and the second is worth trying again.
/// </remarks>
[TestClass]
public class ExitCodeTests : BaseTestClass
{
	[TestMethod]
	public void EveryCodeTheProtocolHasIsAccountedFor()
	{
		foreach (var code in Codes())
		{
			var exit = UiExitCodes.For(code);

			IsTrue(
				exit is UiExitCodes.Failed or UiExitCodes.Unreachable or UiExitCodes.Refused,
				$"{code} came out as {exit}.");
		}
	}

	[TestMethod]
	public void NotBeingThereIsNotTheSameAsSayingNo()
	{
		AreEqual(UiExitCodes.Unreachable, UiExitCodes.For(UiErrorCodes.ApplicationExited));
		AreEqual(UiExitCodes.Refused, UiExitCodes.For(UiErrorCodes.Unauthorized));
		AreNotEqual(UiExitCodes.For(UiErrorCodes.ApplicationExited), UiExitCodes.For(UiErrorCodes.Unauthorized));
	}

	[TestMethod]
	public void WrongNodeAndWrongInputAreBothRefusals()
	{
		AreEqual(UiExitCodes.Refused, UiExitCodes.For(UiErrorCodes.NotFound));
		AreEqual(UiExitCodes.Refused, UiExitCodes.For(UiErrorCodes.NotInteractable));
		AreEqual(UiExitCodes.Refused, UiExitCodes.For(UiErrorCodes.UnsupportedCapability));
	}

	[TestMethod]
	public void NotKnowingIsNotARefusal()
	{
		// Nothing about these says whether asking again would help, so they must not read as "give up".
		AreEqual(UiExitCodes.Failed, UiExitCodes.For(UiErrorCodes.Timeout));
		AreEqual(UiExitCodes.Failed, UiExitCodes.For(UiErrorCodes.OutcomeUnknown));
		AreEqual(UiExitCodes.Failed, UiExitCodes.For(UiErrorCodes.Cancelled));
	}

	[TestMethod]
	public void SomethingUnheardOfIsAnOrdinaryFailure()
	{
		AreEqual(UiExitCodes.Failed, UiExitCodes.For("somethingNobodyHasWrittenYet"));
	}

	[TestMethod]
	public void TheCodesThemselvesAreDistinct()
	{
		int[] codes =
		[
			UiExitCodes.Ok, UiExitCodes.Failed, UiExitCodes.Usage,
			UiExitCodes.Unreachable, UiExitCodes.Refused,
		];

		AreEqual(codes.Length, codes.Distinct().Count());
	}

	private static IEnumerable<string> Codes()
		=> typeof(UiErrorCodes)
			.GetFields(BindingFlags.Public | BindingFlags.Static)
			.Where(field => field.IsLiteral && field.FieldType == typeof(string))
			.Select(field => (string)field.GetRawConstantValue());
}
