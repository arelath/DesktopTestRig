namespace DesktopTestRig;

using System.Collections.Generic;
using DesktopTestRig.Interop;

/// <summary>Shared one-shot access to the recording stack's semantic projection rules.</summary>
public static class SemanticSnapshotPolicy
{
	public static Dictionary<string, object?> CompactProperties(IReadOnlyDictionary<string, object?> properties)
	{
		var result = CompactSemanticRecordingFrame.CompactProperties(properties, includeDefaultStateValues: true);
		foreach (var name in new[] { "Text", "Content", "Header", "Title" })
			if (properties.TryGetValue(name, out var value) && value is null)
				result[char.ToLowerInvariant(name[0]) + name.Substring(1)] = null;
		return result;
	}

	public static bool ShouldInclude(VisualTreeNodeDto node) =>
		CompactSemanticRecordingFrame.ShouldIncludeNode(node,
			CompactSemanticRecordingFrame.CompactProperties(node.Properties),
			new SemanticRecordingFormattingOptions { PruneStructuralLayoutNodes = true });
}
