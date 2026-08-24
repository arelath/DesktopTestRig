namespace DesktopTestPilot.Tests;

using System.Reflection;
using DesktopTestPilot.AppDriverPayload;
using NUnit.Framework;

[TestFixture]
public sealed class PayloadAssemblyBoundaryTests
{
	[Test]
	public void PayloadPreservesWireIdentityWithoutClientImplementation()
	{
		var assembly = typeof(AppDriverPayload).Assembly;

		Assert.That(assembly.GetName().Name, Is.EqualTo("DesktopTestPilot"));
		Assert.That(assembly.GetType("DesktopTestPilot.AppDriver"), Is.Null);
		Assert.That(assembly.GetType("DesktopTestPilot.AppConnection"), Is.Null);
		Assert.That(assembly.GetType("DesktopTestPilot.DefaultAppDriverBackend"), Is.Null);
		Assert.That(assembly.GetType("DesktopTestPilot.Assert.Assertable"), Is.Null);
		Assert.That(
			assembly.GetType("DesktopTestPilot.AppDriverPayload.AppDriverPayload")?.GetMethod("Start", BindingFlags.Public | BindingFlags.Static),
			Is.Not.Null);
	}
}
