namespace DesktopTestPilot.Tests;

using System;
using System.IO;
using DesktopTestPilot.AppDriverPayload;
using DesktopTestPilot.Contracts;
using NUnit.Framework;

[TestFixture]
public sealed class ReliabilityPackagingTests
{
	[Test]
	public void PipeStatusExposesRuntimeCounters()
	{
		var session = new ReusablePipeSession("test-pipe", _ => { });
		var status = session.CreateStatusResponse();

		Assert.That(status.Counters["commandsHandled"], Is.EqualTo(0));
		Assert.That(status.Counters["activeSubscriptions"], Is.EqualTo(0));
	}

	[Test]
	public void StartupLogTailHandlesMissingFile()
	{
		var found = PayloadLog.TryReadTailForPipe("missing-pipe", 123456, out var tail);

		Assert.That(found, Is.False);
		Assert.That(tail, Is.Empty);
	}

	[Test]
	public void PublishLayoutConfigurationIncludesPayloadAndResourceFolders()
	{
		var root = FindRepositoryRoot();
		var project = File.ReadAllText(Path.Combine(root, "DesktopTestPilot.Cli", "DesktopTestPilot.Cli.csproj"));
		var payloadLayoutTargets = File.ReadAllText(Path.Combine(root, "Shared", "DesktopTestPilotPayloadLayout.targets"));
		var frameworkProps = File.ReadAllText(Path.Combine(root, "Shared", "DesktopTestPilot.Frameworks.props"));

		Assert.That(project, Does.Contain("DesktopTestPilotPayloadLayout.targets"));
		Assert.That(payloadLayoutTargets, Does.Contain("$(ArtifactsStagingRoot)payloads\\**\\*.*"));
		Assert.That(payloadLayoutTargets, Does.Contain("$(ArtifactsStagingRoot)DesktopTestPilotResources"));
		Assert.That(frameworkProps, Does.Contain("Family=\"netframework\""));
		Assert.That(frameworkProps, Does.Contain("Family=\"netcoreapp\""));
		Assert.That(frameworkProps, Does.Contain("Family=\"dotnet\""));
	}

	[Test]
	public void CiWorkflowDeclaresFastAndPublishLanes()
	{
		var workflow = File.ReadAllText(Path.Combine(FindRepositoryRoot(), ".github", "workflows", "ci.yml"));

		Assert.That(workflow, Does.Contain("TestFast"));
		Assert.That(workflow, Does.Contain("PublishCli"));
		Assert.That(workflow, Does.Contain("Pack"));
	}

	[Test]
	public void PerformanceBudgetDocumentExists()
	{
		var path = Path.Combine(FindRepositoryRoot(), "Docs", "PerformanceBudgets.md");

		Assert.That(File.Exists(path), Is.True);
	}

	private static string FindRepositoryRoot()
	{
		var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
		while (directory is not null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "DesktopTestPilot.sln")))
				return directory.FullName;

			directory = directory.Parent;
		}

		throw new DirectoryNotFoundException("Could not locate the DesktopTestPilot repository root.");
	}
}
