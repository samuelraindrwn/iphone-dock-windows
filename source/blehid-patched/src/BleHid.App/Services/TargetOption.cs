using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BleHid.App.Services;

public enum TargetKind { Host, Local }

/// <summary>
/// One entry in the mutually exclusive list of places input can go: a bonded host,
/// this PC, or every host at once.
/// </summary>
public sealed class TargetOption : INotifyPropertyChanged
{
    public required string Title { get; init; }
    public required string Detail { get; init; }
    public TargetKind Kind { get; init; }
    public string? DeviceId { get; init; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
