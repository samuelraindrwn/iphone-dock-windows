using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using BleHid.App.Services;

namespace BleHid.App.Views;

public partial class HostsPage : Page
{
    private readonly PeripheralService _service = PeripheralService.Instance;

    public HostsPage()
    {
        InitializeComponent();
        DataContext = _service;
        _service.Hosts.CollectionChanged += OnHostsChanged;
        Unloaded += (_, _) => _service.Hosts.CollectionChanged -= OnHostsChanged;
        UpdateEmptyState();
    }

    private void OnHostsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateEmptyState();

    private void UpdateEmptyState() => NoHostsBar.IsOpen = _service.Hosts.Count == 0;

    private void OnTargetChecked(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is TargetOption option) _service.Select(option);
    }

    private async void OnRefresh(object sender, RoutedEventArgs e) =>
        await _service.RefreshHostsAsync();
}
