using System.Windows;
using System.Windows.Controls;
using BleHid.App.Services;

namespace BleHid.App.Views;

public partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        DataContext = AppSettings.Instance;

        var available = StartupService.Command is not null;
        DevBar.IsOpen = !available;
        AutoStartToggle.IsEnabled = available;
        AutoStartToggle.IsChecked = available && StartupService.IsEnabled();
    }

    private void OnAutoStartChanged(object sender, RoutedEventArgs e) =>
        StartupService.Set(AutoStartToggle.IsChecked == true);

    private void OnExit(object sender, RoutedEventArgs e) => App.Current.ExitApplication();
}
