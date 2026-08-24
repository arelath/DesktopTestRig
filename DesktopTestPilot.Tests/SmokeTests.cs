namespace DesktopTestPilot.Tests;

using DesktopTestPilot.Contracts;
using NUnit.Framework;

[TestFixture]
public sealed class SmokeTests
{
	[Test]
	public void ProductConstantsUseDesktopTestPilotNames()
	{
		Assert.That(ProtocolConstants.ProductName, Is.EqualTo("DesktopTestPilot"));
		Assert.That(ProtocolConstants.PipePrefix, Is.EqualTo("DesktopTestPilot"));
		Assert.That(ProtocolConstants.ProtocolVersion, Is.EqualTo("1"));
	}
}
