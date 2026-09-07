namespace DesktopTestRig.Cli.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using DesktopTestRig.Interop;
using DesktopTestRig.Utility.WpfUtility.Tree;
using NUnit.Framework;

[TestFixture]
public sealed class PropertyDiagnosticsTests
{
	[TestCase("tree", false)]
	[TestCase("tree", true)]
	[TestCase("find", false)]
	[TestCase("find", true)]
	[TestCase("node", false)]
	[TestCase("node", true)]
	[TestCase("props", false)]
	[TestCase("props", true)]
	public void CommandsSeparateValuesFromDiagnostics(string command, bool debug)
	{
		var session = new FakeAppSessionService();
		session.Session.Snapshot = Snapshot();
		var services = CliTestHost.CreateServices(targetResolver: new FakeTargetResolver(), appSessionService: session);
		var args = new List<string> { command, "--pid", "1234", "--props", "Name,Text,Header,Broken" };
		if (command is "node" or "props") args.AddRange(["--target", "root-0001"]);
		if (command == "find") args.AddRange(["--name", "Test", "--include-properties"]);
		if (debug) args.Add("--debug");

		var result = CliTestHost.Run(args.ToArray(), services);

		Assert.That(result.ExitCode, Is.Zero, result.Stdout);
		Assert.That(result.Stdout, Does.Contain("\"Text\":null"));
		Assert.That(result.Stdout, Does.Contain("\"propertyDiagnostics\":"));
		Assert.That(result.Stdout, Does.Contain("property-read-failed"));
		Assert.That(result.Stdout, Does.Contain("getter failed"));
		Assert.That(result.Stdout.Contains("missing-property", StringComparison.Ordinal), Is.EqualTo(debug));
		Assert.That(result.Stdout, Does.Not.Contain(typeof(PropertyExtractionError).FullName));
	}

	[Test]
	public void ProjectionDoesNotMutateRawSnapshotAndRespectsPropertySelection()
	{
		var snapshot = Snapshot();
		var service = new TreeSnapshotService();
		var result = service.Shape(snapshot, new TreeSnapshotOptions { Properties = ["Text", "Header"] });
		Assert.That(result.Nodes.Single().Properties.Keys, Is.EqualTo(new[] { "Text" }));
		Assert.That(result.Nodes.Single().PropertyDiagnostics, Is.Empty);
		var debug = service.Shape(snapshot, new TreeSnapshotOptions { IncludeMissingPropertyDiagnostics = true });
		Assert.That(debug.Nodes.Single().PropertyDiagnostics.Count, Is.EqualTo(2));
		Assert.That(snapshot.Nodes.Single().Properties["Header"], Is.TypeOf<PropertyExtractionError>());
		var suppressed = service.Shape(snapshot, new TreeSnapshotOptions { SuppressProperties = true, IncludeMissingPropertyDiagnostics = true });
		Assert.That(suppressed.Nodes.Single().PropertyDiagnostics, Is.Empty);
		Assert.That(suppressed.Nodes.Single().Properties, Is.Empty);
	}

	[Test]
	public void ErrorsAreNeitherSearchableTextNorSuggestedSelectors()
	{
		var snapshot = Snapshot();
		var finder = new FindSnapshotService();
		Assert.That(finder.Find(snapshot, new FindSnapshotOptions { PropertyContains = new("Header", "PropertyExtractionError") }).MatchCount, Is.Zero);
		Assert.That(finder.Find(snapshot, new FindSnapshotOptions { PropertyEquals = new("Header", typeof(PropertyExtractionError).FullName!) }).MatchCount, Is.Zero);
		Assert.That(finder.Find(snapshot, new FindSnapshotOptions { PropertyRegex = new("Header", ".*") }).MatchCount, Is.Zero);
		var suggestions = new SelectorSuggestionService(new TargetIdService(), snapshot).Suggest(snapshot.Nodes.Single(), false);
		Assert.That(suggestions.Suggestions.Any(s => s.CommandLine.Contains("PropertyExtractionError", StringComparison.Ordinal)), Is.False);
	}

	[Test]
	public void TextOutputIncludesActualFailureDetails()
	{
		var session = new FakeAppSessionService();
		session.Session.Snapshot = Snapshot();
		var result = CliTestHost.Run(["props", "--pid", "1234", "--target", "root-0001", "--props", "Broken", "--format", "text"],
			CliTestHost.CreateServices(targetResolver: new FakeTargetResolver(), appSessionService: session));
		Assert.That(result.Stdout, Does.Contain("Broken: [property-read-failed] getter failed"));
	}

	private static VisualTreeSnapshot Snapshot() => VisualTreeSnapshot.Create(1, [new VisualTreeNodeDto
	{
		TargetId = "root-0001", IsRoot = true, TypeName = "Button",
		Properties = new()
		{
			["Name"] = "Test", ["Text"] = null,
			["Header"] = PropertyExtractionError.Missing("Header"),
			["Broken"] = PropertyExtractionError.Failed("Broken", new InvalidOperationException("getter failed")),
		},
	}]);
}
