namespace DesktopTestRig.InjectorLauncher;

using DesktopTestRig.Shared;

internal static class ConsoleHelper
{
	public static void AttachConsoleToParentProcessOrAllocateNewOne()
	{
		if (!NativeMethods.AttachConsole(NativeMethods.AttachParentProcess))
			_ = NativeMethods.AllocConsole();
	}
}
