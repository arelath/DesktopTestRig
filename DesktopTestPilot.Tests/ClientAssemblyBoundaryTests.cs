namespace DesktopTestPilot.Tests;

using System.Linq;
using NUnit.Framework;

[TestFixture]
public sealed class ClientAssemblyBoundaryTests
{
	[Test]
	public void ClientAssemblyDoesNotContainPayloadImplementation()
	{
		var assembly = typeof(AppDriver).Assembly;

		Assert.That(assembly.GetType("DesktopTestPilot.AppDriverPayload.AppDriverPayload"), Is.Null);
		Assert.That(assembly.GetType("DesktopTestPilot.AppDriverPayload.AppDriverCommandDispatcher"), Is.Null);
		Assert.That(assembly.GetType("DesktopTestPilot.Interop.NamedPipeServer"), Is.Null);
		Assert.That(assembly.GetReferencedAssemblies().Select(static reference => reference.Name), Does.Not.Contain("0Harmony"));
	}
}
