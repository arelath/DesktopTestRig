namespace DesktopTestRig.Cli.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DesktopTestRig.Contracts;
using DesktopTestRig.Interop;
using NUnit.Framework;

[TestFixture]
public sealed class AgentInterfaceTests
{
	[TestCase("find", "--scan-limit")]
	[TestCase("tree", "--view")]
	[TestCase("config set", "<value>")]
	[TestCase("pipe status", "--pid")]
	public void HelpDescribesSelectedCommandWithoutLoadingConfig(string path, string expected)
	{
		var store = new CliDefaultsStore(CliTestHost.CreateTempConfigPath());
		Directory.CreateDirectory(Path.GetDirectoryName(store.ConfigPath)!);
		File.WriteAllText(store.ConfigPath, "invalid json");
		var session = new FakeAppSessionService();
		var result = CliTestHost.Run(path.Split(' ').Concat(["--help"]).ToArray(), CliTestHost.CreateServices(defaultsStore: store, appSessionService: session));
		Assert.That(result.ExitCode, Is.Zero, result.Stdout);
		Assert.That(result.Stdout, Does.Contain(expected).And.Contain("Examples:").And.Contain("Built-in defaults"));
		Assert.That(session.LastTarget, Is.Null);
	}

	[TestCase(null)]
	[TestCase("find")]
	[TestCase("pipe status")]
	public void SchemaDescribesParserAndResponsesWithoutAttaching(string? command)
	{
		var session = new FakeAppSessionService();
		var args = command is null ? new[] { "schema" } : new[] { "schema", "--command", command };
		var result = CliTestHost.Run(args, CliTestHost.CreateServices(appSessionService: session));
		Assert.That(result.ExitCode, Is.Zero, result.Stdout);
		using var json = JsonDocument.Parse(result.Stdout);
		var data = json.RootElement.GetProperty("data");
		Assert.That(data.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(1));
		var commands = data.GetProperty("commands").EnumerateArray().ToArray();
		Assert.That(commands.Length, command is null ? Is.GreaterThan(30) : Is.EqualTo(1));
		if (command == "find")
		{
			var scan = commands[0].GetProperty("options").EnumerateArray().Single(o => o.GetProperty("name").GetString() == "--scan-limit");
			Assert.That(scan.GetProperty("defaultValue").GetInt32(), Is.EqualTo(5000));
			Assert.That(result.Stdout, Does.Contain("searchComplete").And.Contain("resultsTruncated"));
		}
		Assert.That(session.LastTarget, Is.Null);
	}

	[TestCase("missing")]
	[TestCase("find --nonsense")]
	public void SchemaRejectsUnknownPaths(string path)
	{
		var result = CliTestHost.Run(["schema", "--command", path]);
		Assert.That(result.ExitCode, Is.EqualTo(1));
		Assert.That(result.Stdout, Does.Contain("invalid-arguments"));
	}

	[TestCase("find", "--limit", "0")]
	[TestCase("find", "--scan-limit", "-1")]
	[TestCase("tree", "--limit", "0")]
	[TestCase("tree", "--max-depth", "-2")]
	[TestCase("tree", "--view", "invalid")]
	public void InvalidLimitsAndViewsFailBeforeAttach(string command, string option, string value)
	{
		var session = new FakeAppSessionService();
		var result = CliTestHost.Run([command, "--pid", "1234", option, value], CliTestHost.CreateServices(targetResolver: new FakeTargetResolver(), appSessionService: session));
		Assert.That(result.ExitCode, Is.EqualTo(1), result.Stdout);
		Assert.That(session.LastTarget, Is.Null);
	}

	[Test]
	public void CaptureAndReturnLimitsAreIndependentAndBothTruncationsAreReported()
	{
		var session = new FakeAppSessionService();
		session.Session.Snapshot.IsTruncated = true;
		session.Session.Snapshot.TruncationReason = "max-node-count";
		var result = CliTestHost.Run(["find", "--pid", "1234", "--limit", "1", "--scan-limit", "20000"],
			CliTestHost.CreateServices(targetResolver: new FakeTargetResolver(), appSessionService: session));
		using var json = JsonDocument.Parse(result.Stdout);
		var data = json.RootElement.GetProperty("data");
		Assert.That(data.GetProperty("searchComplete").GetBoolean(), Is.False);
		Assert.That(data.GetProperty("resultsTruncated").GetBoolean(), Is.True);
		Assert.That(data.GetProperty("totalMatchCount").GetInt32(), Is.EqualTo(2));
		Assert.That(data.GetProperty("matchCount").GetInt32(), Is.EqualTo(1));
		Assert.That(data.GetProperty("scannedNodeCount").GetInt32(), Is.EqualTo(2));
		Assert.That(session.Session.Commands.OfType<GetVisualTreeCommandRequest>().Single().MaxNodeCount, Is.EqualTo(20000));
	}

	[TestCase(false, 0)]
	[TestCase(true, 11)]
	public void IncompleteSearchNeverEstablishesAbsence(bool required, int exitCode)
	{
		var session = new FakeAppSessionService();
		session.Session.Snapshot.IsTruncated = true;
		var args = new List<string> { "find", "--pid", "1234", "--name", "Missing" };
		if (required) args.Add("--require-match");
		var result = CliTestHost.Run(args.ToArray(), CliTestHost.CreateServices(targetResolver: new FakeTargetResolver(), appSessionService: session));
		Assert.That(result.ExitCode, Is.EqualTo(exitCode), result.Stdout);
		Assert.That(result.Stdout, Does.Contain("\"searchComplete\":false").And.Contain("--scan-limit"));
		Assert.That(result.Stdout, Does.Not.Contain("\"code\":\"no-match\""));
	}

	[Test]
	public void CompleteSearchCanCapReturnedMatchesWithoutLosingCoverage()
	{
		var result = new FindSnapshotService().Find(new FakeAppSessionService().Session.Snapshot, new FindSnapshotOptions { Limit = 1 });
		Assert.That(result.SearchComplete, Is.True);
		Assert.That(result.ResultsTruncated, Is.True);
		Assert.That(result.TotalMatchCount, Is.EqualTo(2));
	}

	[Test]
	public void SemanticIsDefaultAndRawCanBeConfiguredOrExplicitlySelected()
	{
		var store = new CliDefaultsStore(CliTestHost.CreateTempConfigPath());
		var services = CliTestHost.CreateServices(defaultsStore: store, targetResolver: new FakeTargetResolver());
		Assert.That(CliTestHost.Run(["tree", "--pid", "1234"], services).Stdout, Does.Contain("\"view\":\"semantic\""));
		store.Set("commands.tree.view", "raw");
		Assert.That(CliTestHost.Run(["tree", "--pid", "1234"], services).Stdout, Does.Contain("\"typeName\":").Or.Contain("\"totalNodeCount\":"));
		Assert.That(CliTestHost.Run(["tree", "--pid", "1234", "--view", "semantic"], services).Stdout, Does.Contain("\"view\":\"semantic\""));
		Assert.That(() => store.Set("commands.tree.view", "invalid"), Throws.TypeOf<AutomationException>());
	}

	[Test]
	public void SemanticProjectionReparentsControlsAndPropagatesAncestorStateWithoutMutatingInput()
	{
		var snapshot = SemanticSnapshot();
		var result = new SemanticTreeService().Shape(snapshot, new TreeSnapshotOptions { IncludeHidden = true });
		Assert.That(result.Nodes.Select(n => n.TargetId), Is.EqualTo(new[] { "window", "panel", "button" }));
		var button = result.Nodes.Single(n => n.TargetId == "button");
		Assert.That(button.ParentId, Is.EqualTo("panel"));
		Assert.That(button.Depth, Is.EqualTo(2));
		Assert.That(button.Properties["enabled"], Is.EqualTo(false));
		Assert.That(button.Properties["visible"], Is.EqualTo(false));
		Assert.That(result.OmittedNodeCount, Is.EqualTo(2));
		Assert.That(snapshot.Nodes.Single(n => n.TargetId == "button").ParentId, Is.EqualTo("border"));
		Assert.That(snapshot.Nodes.Single(n => n.TargetId == "button").Properties["IsEnabled"], Is.EqualTo(true));
		var visible = new SemanticTreeService().Shape(snapshot, new TreeSnapshotOptions());
		Assert.That(visible.Nodes.Any(n => n.TargetId == "button"), Is.False);
	}

	[Test]
	public void SemanticRootExpansionPreservesAncestorStateAndDepthTruncation()
	{
		var snapshot = SemanticSnapshot();
		snapshot.IsTruncated = true;
		snapshot.TruncationReason = "max-node-count";
		var result = new SemanticTreeService().Shape(snapshot, new TreeSnapshotOptions { IncludeHidden = true, RootTargetId = "panel", MaxDepth = 0, Shape = TreeShape.Nested });
		Assert.That(result.Truncated, Is.True);
		Assert.That(result.Roots.Single().TargetId, Is.EqualTo("panel"));
		Assert.That(result.Roots.Single().Children, Is.Null);
		Assert.That(result.Nodes, Is.Empty);
	}

	[Test]
	public void UnknownStatesAreNotInventedAndNullValuesRemainNull()
	{
		var snapshot = VisualTreeSnapshot.Create(1, [new VisualTreeNodeDto { TargetId = "root", IsRoot = true, TypeName = "Slider", Properties = new() { ["Value"] = null } }]);
		var result = new SemanticTreeService().Shape(snapshot, new TreeSnapshotOptions());
		Assert.That(result.Nodes.Single().Properties.ContainsKey("enabled"), Is.False);
		Assert.That(result.Nodes.Single().Properties["value"], Is.Null);
	}

	private static VisualTreeSnapshot SemanticSnapshot()
	{
		VisualTreeNodeDto Node(string id, string type, string? parent, string[] children, Dictionary<string, object?>? props = null) => new()
		{
			TargetId = id, TypeName = type, ParentId = parent, IsRoot = parent is null, ChildIds = children.ToList(),
			Properties = props ?? new() { ["IsVisible"] = true, ["IsEnabled"] = true },
		};
		return VisualTreeSnapshot.Create(1,
		[
			Node("window", "Window", null, ["panel"]),
			Node("panel", "Grid", "window", ["border"], new() { ["Name"] = "CatalogPanel", ["IsVisible"] = true, ["IsEnabled"] = true }),
			Node("border", "Border", "panel", ["button"], new() { ["IsVisible"] = false, ["IsEnabled"] = false }),
			Node("button", "Button", "border", ["text"], new() { ["Content"] = "Refresh", ["IsVisible"] = true, ["IsEnabled"] = true }),
			Node("text", "TextBlock", "button", [], new() { ["Text"] = "Refresh", ["IsVisible"] = true, ["IsEnabled"] = true }),
		]);
	}
}
