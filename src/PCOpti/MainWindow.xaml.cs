using System.Windows;

namespace PCOpti;

/// <summary>Shell window. Only window chrome behaviour lives here; everything else is in view models.</summary>
public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private void Minimize_Click(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this);
        else SystemCommands.MaximizeWindow(this);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);
}
