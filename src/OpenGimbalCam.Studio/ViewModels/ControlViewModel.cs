using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenGimbalCam.Core.Camera;

namespace OpenGimbalCam.Studio.ViewModels;

/// <summary>One standard UVC control as a slider plus an optional "auto" box.</summary>
public sealed partial class ControlViewModel : ObservableObject
{
    private readonly ICameraSession _session;
    private readonly Action<Exception> _onError;
    private bool _loading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValueText))]
    private double _value;

    [ObservableProperty]
    private bool _auto;

    public ControlViewModel(ICameraSession session, CameraControl control, ControlRange range, Action<Exception> onError)
    {
        _session = session;
        _onError = onError;
        Control = control;
        Min = range.Min;
        Max = range.Max;
        Step = Math.Max(range.Step, 1);
        HasAuto = range.Caps.HasFlag(ControlFlags.Auto);
        Reload();
    }

    public CameraControl Control { get; }

    public string Name => Control.ToString();

    public double Min { get; }

    public double Max { get; }

    public double Step { get; }

    public bool HasAuto { get; }

    public string ValueText => ((int)Value).ToString(CultureInfo.InvariantCulture);

    public void Reload()
    {
        _loading = true;
        try
        {
            if (_session.Get(Control) is { } v)
            {
                Value = v.Value;
                Auto = v.Flags.HasFlag(ControlFlags.Auto);
            }
        }
        catch (Exception ex)
        {
            _onError(ex);
        }
        finally
        {
            _loading = false;
        }
    }

    partial void OnValueChanged(double value)
    {
        if (_loading || Auto)
            return;
        Apply((int)Math.Round(value), ControlFlags.Manual);
    }

    partial void OnAutoChanged(bool value)
    {
        if (_loading)
            return;
        Apply((int)Value, value ? ControlFlags.Auto : ControlFlags.Manual);
        Reload();
    }

    private void Apply(int value, ControlFlags flags)
    {
        try
        {
            _session.Set(Control, value, flags);
        }
        catch (Exception ex)
        {
            _onError(ex);
        }
    }
}
