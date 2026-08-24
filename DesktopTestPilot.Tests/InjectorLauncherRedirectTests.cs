namespace DesktopTestPilot.Tests;

using System.IO;
using DesktopTestPilot.InjectorLauncher;
using NUnit.Framework;

[TestFixture]
public sealed class InjectorLauncherRedirectTests
{
	[Test]
	public void RedirectPathUsesTargetArchitectureExecutableName()
	{
		var path = ArchitectureRedirect.GetLauncherPath(@"C:\tools\DesktopTestPilot.InjectorLauncher.x86.exe", "x64");

		Assert.That(path, Is.EqualTo(@"C:\tools\DesktopTestPilot.InjectorLauncher.x64.exe"));
	}

	[Test]
	public void RedirectPathUsesSiblingArchitectureResourceFolder()
	{
		var path = ArchitectureRedirect.GetLauncherPath(
			@"C:\tools\DesktopTestPilotResources\x64\DesktopTestPilot.InjectorLauncher.x64.exe",
			"x86");

		Assert.That(path, Is.EqualTo(@"C:\tools\DesktopTestPilotResources\x86\DesktopTestPilot.InjectorLauncher.x86.exe"));
	}

	[Test]
	public void RedirectCommandPreservesOriginalArguments()
	{
		var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
		var x86Root = Path.Combine(root, "DesktopTestPilotResources", "x86");
		var x64Root = Path.Combine(root, "DesktopTestPilotResources", "x64");
		Directory.CreateDirectory(x86Root);
		Directory.CreateDirectory(x64Root);
		try
		{
			var currentExe = Path.Combine(x86Root, "DesktopTestPilot.InjectorLauncher.x86.exe");
			var targetExe = Path.Combine(x64Root, "DesktopTestPilot.InjectorLauncher.x64.exe");
			File.WriteAllText(targetExe, string.Empty);

			var startInfo = ArchitectureRedirect.CreateStartInfo(currentExe, "x86", "x64", new[] { "--assembly", @"C:\Program Files\DesktopTestPilot.dll" });

			Assert.That(startInfo, Is.Not.Null);
			Assert.That(startInfo!.FileName, Is.EqualTo(targetExe));
			Assert.That(startInfo.Arguments, Is.EqualTo("--assembly \"C:\\Program Files\\DesktopTestPilot.dll\""));
			Assert.That(startInfo.CreateNoWindow, Is.True);
			Assert.That(startInfo.UseShellExecute, Is.False);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Test]
	public void MissingRedirectExecutableReturnsControlledFailure()
	{
		Assert.That(
			() => ArchitectureRedirect.CreateStartInfo(@"C:\tools\DesktopTestPilot.InjectorLauncher.x86.exe", "x86", "x64", System.Array.Empty<string>()),
			Throws.TypeOf<InjectorLauncherException>().With.Property(nameof(InjectorLauncherException.ExitCode)).EqualTo(InjectorExitCode.MissingArchitectureLauncher));
	}

	[Test]
	public void RedirectRunPassesThroughExitCode()
	{
		var startInfo = new System.Diagnostics.ProcessStartInfo("DesktopTestPilot.InjectorLauncher.x64.exe");

		var exitCode = ArchitectureRedirect.Run(startInfo, _ => new FakeRedirectedProcess(37));

		Assert.That(exitCode, Is.EqualTo(37));
	}

	private sealed class FakeRedirectedProcess : IRedirectedProcess
	{
		public FakeRedirectedProcess(int exitCode)
		{
			ExitCode = exitCode;
		}

		public int ExitCode { get; }

		public void WaitForExit()
		{
		}

		public void Dispose()
		{
		}
	}
}
