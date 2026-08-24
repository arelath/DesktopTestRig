namespace DesktopTestPilot.AppDriverPayload.Commands;

using DesktopTestPilot.AppDriverPayload.Diagnostics;
using DesktopTestPilot.Contracts;

internal static class GetBindingFailuresCommand
{
	public static object Process(GetBindingFailuresCommandRequest request)
	{
		if (request is null)
			return StandardIpcResponse.FromError("Binding failure request is required.", ProtocolConstants.ErrorCodes.ProtocolError, PayloadLog.CurrentCorrelationId);

		if (request.MaxCount < 0)
			return StandardIpcResponse.FromError("Binding failure max count must be zero or greater.", ProtocolConstants.ErrorCodes.InvalidArguments, PayloadLog.CurrentCorrelationId);

		return BindingFailureCaptureService.Instance.ReadSince(request.AfterSequenceNumber, request.MaxCount);
	}
}
