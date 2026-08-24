namespace DesktopTestRig.Mcp;

using System.Windows;
using DesktopTestRig.Mcp.ViewModels;

internal partial class MainWindow : Window
{
	public MainWindow(MainWindowViewModel viewModel)
	{
		InitializeComponent();
		DataContext = viewModel;
	}
}
