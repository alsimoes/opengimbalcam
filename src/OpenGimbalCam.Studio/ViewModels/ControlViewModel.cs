using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGimbalCam.Core.Camera;

namespace OpenGimbalCam.Studio.ViewModels;

/// <summary>One standard UVC control: a slider, a segmented choice or a switch, plus an optional "auto" toggle.</summary>
public sealed partial class ControlViewModel : ObservableObject
{
    private readonly ICameraSession _session;
    private readonly Action<Exception> _onError;
    private readonly ControlRange _range;
    private readonly ControlSpec _spec;
    private bool _loading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayValue))]
    [NotifyPropertyChangedFor(nameof(IsModified))]
    [NotifyPropertyChangedFor(nameof(SelectedOption))]
    [NotifyPropertyChangedFor(nameof(ToggleValue))]
    private double _value;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayValue))]
    [NotifyPropertyChangedFor(nameof(IsModified))]
    private bool _auto;

    public ControlViewModel(ICameraSession session, CameraControl control, ControlRange range, Action<Exception> onError)
    {
        _session = session;
        _onError = onError;
        _range = range;
        _spec = ControlCatalog.Get(control);
        Control = control;
        Min = range.Min;
        Max = range.Max;
        Step = Math.Max(range.Step, 1);
        Default = Math.Clamp(range.Default, range.Min, range.Max);
        HasAuto = range.Caps.HasFlag(ControlFlags.Auto);
        if (_spec.Options is { } all && range.Min >= 0 && range.Max < all.Count)
            Options = all.Skip(range.Min).Take(range.Max - range.Min + 1).ToList();
        Reload();
    }

    public CameraControl Control { get; }

    public string Label => _spec.Label;

    public string IconKey => _spec.IconKey;

    public ControlGroup Group => _spec.Group;

    public int Order => _spec.Order;

    public double Min { get; }

    public double Max { get; }

    public double Step { get; }

    public int Default { get; }

    public bool HasAuto { get; }

    public IReadOnlyList<string>? Options { get; }

    public bool IsOptions => Options is not null;

    public bool IsToggle => !IsOptions && Max - Min == 1;

    public bool IsSlider => !IsOptions && !IsToggle;

    public int SelectedOption
    {
        get => (int)Math.Round(Value) - (int)Min;
        set
        {
            if (value >= 0)
                Value = Min + value;
        }
    }

    public bool ToggleValue
    {
        get => Value > Min;
        set => Value = value ? Max : Min;
    }

    public string DisplayValue
    {
        get
        {
            if (Auto)
                return "Auto";
            if (Options is not null)
            {
                int i = SelectedOption;
                return i >= 0 && i < Options.Count ? Options[i] : ((int)Value).ToString(CultureInfo.InvariantCulture);
            }
            if (IsToggle)
                return ToggleValue ? "Ligado" : "Desligado";
            return ControlCatalog.FormatValue(Control, Value, _range);
        }
    }

    /// <summary>Differs from the reset state: controls with an automatic mode are modified when not automatic.</summary>
    public bool IsModified => HasAuto ? !Auto : (int)Math.Round(Value) != Default;

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

    /// <summary>Back to the camera default value, and to automatic where the control has it.</summary>
    [RelayCommand]
    private void Reset()
    {
        Apply(Default, HasAuto ? ControlFlags.Auto : ControlFlags.Manual);
        Reload();
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
