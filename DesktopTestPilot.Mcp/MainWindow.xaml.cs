namespace DesktopTestPilot.Mcp;

using System.Windows;
using DesktopTestPilot.Mcp.ViewModels;

internal partial class MainWindow : Window
{
	public MainWindow(MainWindowViewModel viewModel)
	{
		InitializeComponent();
		DataContext = viewModel;
	}
}
