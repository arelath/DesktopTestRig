namespace DesktopTestPilot.AppDriverPayload.Commands;

using DesktopTestPilot.Contracts;
using DesktopTestPilot.Interop;

internal static class PipeStatusCommand
{
	public static object Process(PipeStatusCommandRequest request, AppDriverPayloadStartupOptions options, ReusablePipeSession? reusableSession)
	{
		return reusableSession?.CreateStatusResponse() ?? new PipeStatusCommandResponse
		{
			PipeName = options.PipeName,
			IsReusable = false,
			IsBusy = false,
			IsSending = false,
			ActiveSubscriptionCount = 0,
			TotalCommandsHandled = 1,
			DisconnectedClientCount = 0,
			ActiveConnectionCount = 1,
			IdleMode = "one-shot-command",
		};
	}
}
