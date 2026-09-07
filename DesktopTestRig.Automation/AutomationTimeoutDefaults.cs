namespace DesktopTestRig.Automation;

using DesktopTestRig.Contracts;

public static class AutomationTimeoutDefaults
{
	public const int InitialPipeProbeTimeoutMs = TimeoutDefaults.CliInitialPipeProbeTimeoutMs;

	public const int AttachTimeoutMs = TimeoutDefaults.CliAttachTimeoutMs;

	public const int AttachRetrySleepMs = TimeoutDefaults.CliAttachRetrySleepMs;

	public const int OneShotConnectTimeoutCapMs = TimeoutDefaults.CliOneShotConnectTimeoutCapMs;
}
