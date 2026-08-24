namespace DesktopTestRig.Tests;

using DesktopTestRig.Contracts;
using NUnit.Framework;

[TestFixture]
public sealed class SmokeTests
{
	[Test]
	public void ProductConstantsUseDesktopTestRigNames()
	{
		Assert.That(ProtocolConstants.ProductName, Is.EqualTo("DesktopTestRig"));
		Assert.That(ProtocolConstants.PipePrefix, Is.EqualTo("DesktopTestRig"));
		Assert.That(ProtocolConstants.ProtocolVersion, Is.EqualTo("1"));
	}
}
