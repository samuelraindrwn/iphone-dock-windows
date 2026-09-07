namespace BleHid.Core;

/// <summary>
/// Pump-owned relative-motion gain and rotation. Fractional remainders preserve small movements;
/// saturation discards excess displacement instead of replaying it in later reports.
/// This class performs no IO and generates no reports by itself.
/// </summary>
internal sealed class PointerMotionScaler
{
    private double _gain = PointerSettingsMonitor.DefaultSensitivity;
    private long _revision = -1;
    private int _rotationDegrees;
    private double _remainderX, _remainderY;

    public (int Dx, int Dy) Scale(long dx, long dy, PointerSettingsSnapshot settings)
    {
        var gain = PointerSettingsMonitor.Normalize(settings.Sensitivity);
        var rotationDegrees = PointerSettingsMonitor.IsValidRotation(settings.RotationDegrees)
            ? settings.RotationDegrees : 0;
        if (_revision != settings.Revision || _gain != gain || _rotationDegrees != rotationDegrees)
        {
            Reset();
            _revision = settings.Revision;
            _gain = gain;
            _rotationDegrees = rotationDegrees;
        }

        // Rotate only bounded HID components, so negation cannot overflow even
        // when the original coalesced input is long.MinValue. In screen coordinates
        // (positive Y down), 90 degrees maps screen-right to device-down.
        var x = ScaleAxis(dx, ref _remainderX);
        var y = ScaleAxis(dy, ref _remainderY);
        return _rotationDegrees switch
        {
            90 => (-y, x),
            180 => (-x, -y),
            270 => (y, -x),
            _ => (x, y)
        };
    }

    public void Reset()
    {
        _remainderX = _remainderY = 0;
        _revision = -1;
    }

    private int ScaleAxis(long delta, ref double remainder)
    {
        var scaled = delta * _gain + remainder;
        // Match the existing HID report descriptor's signed 16-bit range. Do not
        // retain overflow: that would move the pointer after physical motion stops.
        if (scaled >= 32767) { remainder = 0; return 32767; }
        if (scaled <= -32767) { remainder = 0; return -32767; }
        var whole = (int)Math.Truncate(scaled);
        remainder = scaled - whole;
        return whole;
    }
}
