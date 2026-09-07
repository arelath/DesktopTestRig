namespace DesktopTestRig.Cli;

using System;

public sealed class CliServices
{
	private readonly Lazy<AutomationServices> automation;

	public CliServices(
		CliDefaultsStore? defaultsStore = null,
		IProcessSnapshotSource? processSnapshotSource = null,
		ITargetResolver? targetResolver = null,
		IAutomationSessionService? appSessionService = null)
	{
		DefaultsStore = defaultsStore ?? new CliDefaultsStore();
		automation = new(() => new AutomationServices(processSnapshotSource, targetResolver, appSessionService));
	}

	public CliDefaultsStore DefaultsStore { get; }

	public AutomationServices Automation => automation.Value;

	public IProcessSnapshotSource ProcessSnapshotSource => Automation.ProcessSnapshotSource;

	public ITargetResolver TargetResolver => Automation.TargetResolver;

	public IAutomationSessionService AppSessionService => Automation.SessionService;
}
