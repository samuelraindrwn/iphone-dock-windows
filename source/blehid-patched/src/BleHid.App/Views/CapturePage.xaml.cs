using System.Windows.Controls;
using BleHid.App.Services;

namespace BleHid.App.Views;

public partial class CapturePage : Page
{
    private readonly PeripheralService _service = PeripheralService.Instance;

    public CapturePage()
    {
        InitializeComponent();
        DataContext = _service;
    }

    private async void OnToggleCapture(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_service.IsCapturing) await _service.StopCaptureAsync();
        else await _service.StartCaptureAsync();

        // IsChecked is bound one-way, so snap the switch back to whatever the session actually did.
        CaptureToggle.IsChecked = _service.IsCapturing;
    }
}
