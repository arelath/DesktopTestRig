namespace DesktopTestRig.AppDriverPayload.Commands;

using DesktopTestRig.Contracts;
using DesktopTestRig.Utility;

internal static class UnsupportedUiCommand
{
	public static object Process(string commandKind)
	{
		var availability = ThreadUtility.GetAvailability();
		return StandardIpcResponse.FromError(
			$"Command '{commandKind}' requires WPF, WinForms, or native HWND target support. Availability: WPF={availability.IsWpfAvailable}; WinForms={availability.IsWinFormsAvailable}; NativeFallback={availability.IsNativeFallbackAvailable}.",
			ProtocolConstants.ErrorCodes.UnsupportedTarget,
			PayloadLog.CurrentCorrelationId);
	}
}
