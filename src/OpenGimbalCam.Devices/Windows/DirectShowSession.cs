using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using OpenGimbalCam.Core.Camera;

namespace OpenGimbalCam.Devices.Windows;

/// <summary>
/// An opened capture filter. Exposes the standard UVC controls (what Windows'
/// usbvideo.sys maps to IAMCameraControl / IAMVideoProcAmp) and the OBSBOT
/// vendor Extension Unit.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class DirectShowSession : ICameraSession
{
    private object? _filter;
    private KsExtensionUnit? _xu;
    private bool _xuSearched;

    internal DirectShowSession(CameraInfo info, object filter)
    {
        Info = info;
        _filter = filter;
    }

    public CameraInfo Info { get; }

    private object Filter => _filter ?? throw new ObjectDisposedException(nameof(DirectShowSession));

    public ControlRange? GetRange(CameraControl control)
    {
        int hr, min, max, step, def;
        ControlFlags caps;
        if (ToCameraControl(control) is { } cp)
        {
            if (Filter is not IAMCameraControl cc)
                return null;
            hr = cc.GetRange(cp, out min, out max, out step, out def, out caps);
        }
        else if (ToProcAmp(control) is { } vp)
        {
            if (Filter is not IAMVideoProcAmp pa)
                return null;
            hr = pa.GetRange(vp, out min, out max, out step, out def, out caps);
        }
        else
        {
            return null;
        }
        return hr == 0 ? new ControlRange(min, max, step, def, caps) : null;
    }

    public ControlValue? Get(CameraControl control)
    {
        int hr, value;
        ControlFlags flags;
        if (ToCameraControl(control) is { } cp)
        {
            if (Filter is not IAMCameraControl cc)
                return null;
            hr = cc.Get(cp, out value, out flags);
        }
        else if (ToProcAmp(control) is { } vp)
        {
            if (Filter is not IAMVideoProcAmp pa)
                return null;
            hr = pa.Get(vp, out value, out flags);
        }
        else
        {
            return null;
        }
        return hr == 0 ? new ControlValue(value, flags) : null;
    }

    public void Set(CameraControl control, int value, ControlFlags flags = ControlFlags.Manual)
    {
        if (ToCameraControl(control) is { } cp)
        {
            if (Filter is not IAMCameraControl cc)
                throw new NotSupportedException("O filtro não expõe IAMCameraControl.");
            Marshal.ThrowExceptionForHR(cc.Set(cp, value, flags));
        }
        else if (ToProcAmp(control) is { } vp)
        {
            if (Filter is not IAMVideoProcAmp pa)
                throw new NotSupportedException("O filtro não expõe IAMVideoProcAmp.");
            Marshal.ThrowExceptionForHR(pa.Set(vp, value, flags));
        }
        else
        {
            throw new NotSupportedException($"{control} não existe no DirectShow.");
        }
    }

    /// <summary>The OBSBOT Extension Unit, located the same way libdev.dll does it.</summary>
    public IExtensionUnit? ExtensionUnit
    {
        get
        {
            if (!_xuSearched)
            {
                _xu = KsExtensionUnit.Find(Filter, ObsbotXu.Guid);
                _xuSearched = true;
            }
            return _xu;
        }
    }

    /// <summary>All nodes of the kernel-streaming topology (Camera Terminal, Processing Unit, XUs...).</summary>
    public IReadOnlyList<string> DescribeTopology()
    {
        var lines = new List<string> { "Topologia Kernel Streaming (usbvideo.sys):" };
        if (Filter is not IKsTopologyInfo topo || topo.get_NumNodes(out var count) != 0)
            return lines;
        for (uint i = 0; i < count; i++)
        {
            if (topo.get_NodeType(i, out var type) != 0)
                continue;
            var tag = type == KsGuids.KSNODETYPE_DEV_SPECIFIC ? "  <- Extension Unit" : "";
            lines.Add($"  nó {i,2}: {type:B} {ReadNodeName(topo, i)}{tag}");
        }
        return lines;
    }

    public void Dispose()
    {
        _xu?.Dispose();
        _xu = null;
        if (_filter is not null)
        {
            Marshal.ReleaseComObject(_filter);
            _filter = null;
        }
    }

    private static CameraControlProperty? ToCameraControl(CameraControl c) => c switch
    {
        CameraControl.Pan => CameraControlProperty.Pan,
        CameraControl.Tilt => CameraControlProperty.Tilt,
        CameraControl.Roll => CameraControlProperty.Roll,
        CameraControl.Zoom => CameraControlProperty.Zoom,
        CameraControl.Exposure => CameraControlProperty.Exposure,
        CameraControl.Iris => CameraControlProperty.Iris,
        CameraControl.Focus => CameraControlProperty.Focus,
        CameraControl.PanRelative => CameraControlProperty.PanRelative,
        CameraControl.TiltRelative => CameraControlProperty.TiltRelative,
        _ => null,
    };

    private static VideoProcAmpProperty? ToProcAmp(CameraControl c) => c switch
    {
        CameraControl.Brightness => VideoProcAmpProperty.Brightness,
        CameraControl.Contrast => VideoProcAmpProperty.Contrast,
        CameraControl.Hue => VideoProcAmpProperty.Hue,
        CameraControl.Saturation => VideoProcAmpProperty.Saturation,
        CameraControl.Sharpness => VideoProcAmpProperty.Sharpness,
        CameraControl.Gamma => VideoProcAmpProperty.Gamma,
        CameraControl.WhiteBalance => VideoProcAmpProperty.WhiteBalance,
        CameraControl.BacklightCompensation => VideoProcAmpProperty.BacklightCompensation,
        CameraControl.Gain => VideoProcAmpProperty.Gain,
        CameraControl.PowerlineFrequency => VideoProcAmpProperty.PowerlineFrequency,
        _ => null,
    };

    private static string? ReadNodeName(IKsTopologyInfo topo, uint node)
    {
        const int chars = 256;
        var buffer = Marshal.AllocCoTaskMem(chars * 2);
        try
        {
            return topo.get_NodeName(node, buffer, chars * 2, out _) == 0 ? Marshal.PtrToStringUni(buffer) : null;
        }
        finally
        {
            Marshal.FreeCoTaskMem(buffer);
        }
    }
}
