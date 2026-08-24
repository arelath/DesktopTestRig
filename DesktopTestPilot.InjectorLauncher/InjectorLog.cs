namespace DesktopTestPilot.InjectorLauncher;

using System;
using System.IO;

internal static class InjectorLog
{
	public static string DefaultLogDirectory =>
		Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopTestPilot", "logs");

	public static string DefaultLogPath => Path.Combine(DefaultLogDirectory, "DesktopTestPilot-injector.log");

	public static void Reset()
	{
		Directory.CreateDirectory(DefaultLogDirectory);
		if (File.Exists(DefaultLogPath))
			File.Delete(DefaultLogPath);
	}

	public static void Write(string message)
	{
		Directory.CreateDirectory(DefaultLogDirectory);
		File.AppendAllText(DefaultLogPath, $"{DateTimeOffset.Now:O}: {message}{Environment.NewLine}");
	}

	public static string CreateNativeLogPath(int processId)
	{
		Directory.CreateDirectory(DefaultLogDirectory);
		return Path.Combine(DefaultLogDirectory, $"DesktopTestPilot-native-{processId}-{Guid.NewGuid():N}.log");
	}
}
