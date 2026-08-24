namespace DesktopTestRig.Tests;

using System.Reflection;
using DesktopTestRig.AppDriverPayload;
using NUnit.Framework;

[TestFixture]
public sealed class PayloadAssemblyBoundaryTests
{
	[Test]
	public void PayloadPreservesWireIdentityWithoutClientImplementation()
	{
		var assembly = typeof(AppDriverPayload).Assembly;

		Assert.That(assembly.GetName().Name, Is.EqualTo("DesktopTestRig"));
		Assert.That(assembly.GetType("DesktopTestRig.AppDriver"), Is.Null);
		Assert.That(assembly.GetType("DesktopTestRig.AppConnection"), Is.Null);
		Assert.That(assembly.GetType("DesktopTestRig.DefaultAppDriverBackend"), Is.Null);
		Assert.That(assembly.GetType("DesktopTestRig.Assert.Assertable"), Is.Null);
		Assert.That(
			assembly.GetType("DesktopTestRig.AppDriverPayload.AppDriverPayload")?.GetMethod("Start", BindingFlags.Public | BindingFlags.Static),
			Is.Not.Null);
	}
}
