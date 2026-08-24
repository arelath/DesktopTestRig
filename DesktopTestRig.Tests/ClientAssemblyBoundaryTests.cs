namespace DesktopTestRig.Tests;

using System.Linq;
using NUnit.Framework;

[TestFixture]
public sealed class ClientAssemblyBoundaryTests
{
	[Test]
	public void ClientAssemblyDoesNotContainPayloadImplementation()
	{
		var assembly = typeof(AppDriver).Assembly;

		Assert.That(assembly.GetType("DesktopTestRig.AppDriverPayload.AppDriverPayload"), Is.Null);
		Assert.That(assembly.GetType("DesktopTestRig.AppDriverPayload.AppDriverCommandDispatcher"), Is.Null);
		Assert.That(assembly.GetType("DesktopTestRig.Interop.NamedPipeServer"), Is.Null);
		Assert.That(assembly.GetReferencedAssemblies().Select(static reference => reference.Name), Does.Not.Contain("0Harmony"));
	}
}
