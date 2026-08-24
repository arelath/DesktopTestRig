namespace DesktopTestPilot.Utility.WpfUtility.Tree;

using System.Collections.Generic;
using DesktopTestPilot.Contracts;

public sealed class TreeSnapshotOptions
{
	public IReadOnlyList<string>? RequestedPropertyNames { get; set; }

	public string? RootTargetId { get; set; }

	public bool IncludeHidden { get; set; } = true;

	public int? MaxDepth { get; set; }

	public int MaxNodeCount { get; set; } = VisualTreeDefaults.DefaultMaxNodeCount;
}
