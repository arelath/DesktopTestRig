namespace DesktopTestRig.Automation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using DesktopTestRig.Contracts;
using DesktopTestRig.Interop;
using DesktopTestRig.Utility.WpfUtility.Tree;

/// <summary>A lossy interface outline; target IDs still refer to the original raw nodes.</summary>
public sealed class SemanticTreeService
{
	public static IReadOnlyList<string> RequestProperties { get; } =
	[
		"Name", "AutomationProperties.Name", "AutomationProperties.AutomationId", "Text", "Content", "Header", "Title",
		"IsVisible", "IsEnabled", "IsChecked", "Checked", "IsSelected", "IsExpanded", "IsOpen", "IsSubmenuOpen",
		"Value", "SelectedValue", "IsReadOnly",
	];

	public SemanticTreeData Shape(VisualTreeSnapshot snapshot, TreeSnapshotOptions options)
	{
		var relationships = SnapshotRelationships.Create(snapshot);
		var root = string.IsNullOrWhiteSpace(options.RootTargetId) ? null : new TargetIdService().Resolve(options.RootTargetId, snapshot);
		var scope = root is null ? snapshot.Nodes : new[] { relationships.Nodes[root] }.Concat(relationships.SubtreeOf(root, -1)).ToList();
		var compact = scope.ToDictionary(n => n.TargetId, n => SemanticSnapshotPolicy.CompactProperties(n.Properties), StringComparer.Ordinal);
		var states = new Dictionary<string, (bool? Visible, bool? Enabled)>(StringComparer.Ordinal);
		(bool? Visible, bool? Enabled) Effective(VisualTreeNodeDto n)
		{
			if (states.TryGetValue(n.TargetId, out var cached)) return cached;
			var visible = ReadBool(n, "IsVisible");
			var enabled = ReadBool(n, "IsEnabled");
			if (n.ParentId is not null && relationships.Nodes.TryGetValue(n.ParentId, out var parent))
			{
				var inherited = Effective(parent);
				visible = And(visible, inherited.Visible);
				enabled = And(enabled, inherited.Enabled);
			}
			return states[n.TargetId] = (visible, enabled);
		}

		var included = scope.Where(n => options.IncludeHidden || Effective(n).Visible is not false)
			.Where(n => n.TargetId == root || n.IsRoot || n.ParentId is null || HasAnchor(compact[n.TargetId]) || IsControl(n)
				|| SemanticSnapshotPolicy.ShouldInclude(n))
			.Where(n => !IsDuplicateLabel(n, compact, relationships))
			.ToList();
		var ids = included.Select(n => n.TargetId).ToHashSet(StringComparer.Ordinal);
		var clones = included.Select(n => n with
		{
			ParentId = relationships.AncestorsOf(n.TargetId).FirstOrDefault(a => ids.Contains(a.TargetId))?.TargetId,
			ChildIds = [],
			Properties = new Dictionary<string, object?>(n.Properties, StringComparer.Ordinal),
		}).ToList();
		var byId = clones.ToDictionary(n => n.TargetId, StringComparer.Ordinal);
		foreach (var node in clones)
		{
			node.IsRoot = node.ParentId is null;
			if (node.ParentId is not null) byId[node.ParentId].ChildIds.Add(node.TargetId);
		}
		var projected = VisualTreeSnapshot.Create(snapshot.SequenceNumber, clones, snapshot.RequestedPropertyNames,
			snapshot.TargetFrameworkFamily, snapshot.IsTruncated, snapshot.TruncationReason);
		var shaped = new TreeSnapshotService().Shape(projected, new TreeSnapshotOptions
		{
			Shape = TreeShape.Flat, IncludeHidden = true, IncludeTypeNames = true, TypeNames = options.TypeNames,
			MaxDepth = options.MaxDepth, Limit = options.Limit, SuppressProperties = true, UseShortIds = false,
		});
		var nodes = shaped.Nodes.Select(n =>
		{
			var source = relationships.Nodes[n.TargetId];
			var props = compact[n.TargetId];
			var effective = Effective(source);
			props.Remove("visible"); props.Remove("enabled");
			if (effective.Visible.HasValue) props["visible"] = effective.Visible.Value;
			if (effective.Enabled.HasValue) props["enabled"] = effective.Enabled.Value;
			foreach (var key in new[] { "Value", "SelectedValue", "IsReadOnly" }.Concat(options.Properties.Where(p => !RequestProperties.Contains(p, StringComparer.Ordinal))))
				if (source.Properties.TryGetValue(key, out var value) && (value is null or string or bool or int or long or float or double or decimal))
					props[key is "Value" or "SelectedValue" or "IsReadOnly" ? char.ToLowerInvariant(key[0]) + key[1..] : key] = value;
			return new SemanticTreeNode
			{
				TargetId = n.TargetId, ParentId = n.ParentId, Type = n.TypeName ?? source.TypeName,
				Label = options.SuppressProperties ? null : Label(props), Depth = n.Depth, Properties = options.SuppressProperties ? [] : props,
				Path = options.IncludePath ? relationships.PathOf(n.TargetId) : null,
				PropertyDiagnostics = options.SuppressProperties ? [] : source.Properties.Values.OfType<PropertyExtractionError>()
					.Where(e => options.IncludeMissingPropertyDiagnostics || e.ErrorCode != "missing-property").ToArray(),
			};
		}).ToList();
		if (options.Shape == TreeShape.Nested)
		{
			var outputs = nodes.ToDictionary(n => n.TargetId, StringComparer.Ordinal);
			foreach (var node in nodes)
				if (node.ParentId is not null && outputs.TryGetValue(node.ParentId, out var parent)) (parent.Children ??= []).Add(node);
		}
		return new SemanticTreeData
		{
			Shape = ProtocolValueMapper.FormatTreeShape(options.Shape), NodeCount = nodes.Count, CapturedNodeCount = snapshot.Nodes.Count,
			OmittedNodeCount = scope.Count - included.Count, Truncated = shaped.Truncated, TruncationReason = shaped.TruncationReason,
			Nodes = options.Shape == TreeShape.Flat ? nodes : [],
			Roots = options.Shape == TreeShape.Nested ? nodes.Where(n => n.ParentId is null).ToList() : [],
		};
	}

	private static bool? ReadBool(VisualTreeNodeDto n, string key) => n.Properties.TryGetValue(key, out var value) && value is bool b ? b : null;
	private static bool? And(bool? a, bool? b) => a is false || b is false ? false : a is true && b is true ? true : null;
	private static bool HasAnchor(Dictionary<string, object?> p) => p.ContainsKey("automationId")
		|| (p.TryGetValue("name", out var name) && name is string s && !s.StartsWith("PART_", StringComparison.Ordinal));
	private static bool IsControl(VisualTreeNodeDto n) => (n.FrameworkTypeName ?? n.TypeName).Split('.').Last() is
		"Button" or "RepeatButton" or "ToggleButton" or "CheckBox" or "RadioButton" or "TextBox" or "PasswordBox" or "ComboBox"
		or "Slider" or "TabControl" or "TabItem" or "Menu" or "MenuItem" or "TreeView" or "TreeViewItem" or "ListBox" or "ListBoxItem"
		or "DataGrid" or "Expander" or "GroupBox" or "ToolBar" or "StatusBar" or "Window" or "Form";
	private static string? Label(Dictionary<string, object?> props) => new[] { "automationName", "title", "header", "text", "content", "name" }
		.Select(key => props.TryGetValue(key, out var value) ? value as string : null).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
	private static bool IsDuplicateLabel(VisualTreeNodeDto n, Dictionary<string, Dictionary<string, object?>> props, SnapshotRelationships relationships)
	{
		if (n.IsRoot || n.TypeName is not ("TextBlock" or "AccessText") || HasAnchor(props[n.TargetId])) return false;
		var label = Label(props[n.TargetId]);
		return label is not null && relationships.AncestorsOf(n.TargetId).Take(4)
			.Any(a => IsControl(a) && props.TryGetValue(a.TargetId, out var parentProps) && Label(parentProps) == label);
	}
}

public sealed class SemanticTreeData
{
	public string View => "semantic";
	public string Shape { get; set; } = "flat";
	public int NodeCount { get; set; }
	public int CapturedNodeCount { get; set; }
	public int OmittedNodeCount { get; set; }
	public bool Truncated { get; set; }
	public string? TruncationReason { get; set; }
	public string ExpandHint => "Use node --target <targetId> --subtree, or tree --view raw --root <targetId>. Increase --limit if capture is truncated.";
	public IReadOnlyList<SemanticTreeNode> Nodes { get; set; } = [];
	public IReadOnlyList<SemanticTreeNode> Roots { get; set; } = [];
}

public sealed class SemanticTreeNode
{
	public string TargetId { get; set; } = "";
	public string? ParentId { get; set; }
	public string Type { get; set; } = "";
	public string? Label { get; set; }
	public int Depth { get; set; }
	public string? Path { get; set; }
	public Dictionary<string, object?> Properties { get; set; } = [];
	public IReadOnlyList<PropertyExtractionError> PropertyDiagnostics { get; set; } = [];
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public List<SemanticTreeNode>? Children { get; set; }
}
