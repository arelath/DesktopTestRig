namespace DesktopTestRig.Utility.WpfUtility.Tree;

using DesktopTestRig.Utility.WpfUtility.Tree.TreeAdapters;

public sealed partial class TreeService
{
	private static readonly ITreeTargetAdapter[] TreeTargetAdapters =
	[
		new HybridBridgeTreeTargetAdapter(),
		new ApplicationTreeTargetAdapter(),
		new ResourceDictionaryTreeTargetAdapter(),
		new SystemResourceTreeTargetAdapter(),
		new FrameworkElementResourceTreeTargetAdapter(),
		new ImageTreeTargetAdapter(),
		new WpfTreeTargetAdapter(),
		new WinFormsTreeTargetAdapter(),
		new NativeHwndTreeTargetAdapter(),
		new AutomationTreeTargetAdapter(),
	];
}
