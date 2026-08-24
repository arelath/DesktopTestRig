namespace DesktopTestRig.Tests;

using System.IO;
using NUnit.Framework;

[TestFixture]
public sealed class NativeInjectorProjectTests
{
	[Test]
	public void NativeProjectDeclaresExpectedOutputsAndExport()
	{
		var root = FindRepositoryRoot();
		var project = File.ReadAllText(Path.Combine(root, "DesktopTestRig.GenericInjector", "DesktopTestRig.GenericInjector.vcxproj"));
		var executor = File.ReadAllText(Path.Combine(root, "DesktopTestRig.GenericInjector", "Executor.cpp"));

		Assert.That(project, Does.Contain("DesktopTestRig.GenericInjector.$(ArchitectureName)"));
		Assert.That(project, Does.Contain("version.rc"));
		Assert.That(executor, Does.Contain("ExecuteInDefaultAppDomain"));
		Assert.That(executor, Does.Contain("netframework"));
		Assert.That(executor, Does.Contain("netcoreapp"));
		Assert.That(executor, Does.Contain("dotnet"));
	}

	private static string FindRepositoryRoot()
	{
		var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
			directory = directory.Parent;

		return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
	}
}
