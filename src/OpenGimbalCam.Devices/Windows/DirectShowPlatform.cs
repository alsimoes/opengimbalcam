using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using OpenGimbalCam.Core.Camera;

namespace OpenGimbalCam.Devices.Windows;

/// <summary>
/// Video capture devices from the DirectShow system device enumerator, the same
/// path libdev.dll takes (CLSID_SystemDeviceEnum + CLSID_VideoInputDeviceCategory).
/// COM objects are apartment bound: use it through <see cref="CameraPlatform.Create"/>,
/// which serializes every call on one MTA thread.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DirectShowPlatform : ICameraPlatform
{
    public string Name => "Windows (DirectShow + Kernel Streaming)";

    public IReadOnlyList<CameraInfo> Enumerate()
    {
        var result = new List<CameraInfo>();
        foreach (var (moniker, info) in EnumerateMonikers())
        {
            result.Add(info);
            Marshal.ReleaseComObject(moniker);
        }
        return result;
    }

    /// <summary>Binds the device's moniker to its capture filter (IBaseFilter).</summary>
    public ICameraSession Open(CameraInfo camera)
    {
        IMoniker? match = null;
        foreach (var (moniker, info) in EnumerateMonikers())
        {
            bool same = match is null && (camera.Path is not null
                ? string.Equals(info.Path, camera.Path, StringComparison.OrdinalIgnoreCase)
                : info.Index == camera.Index && info.Name == camera.Name);
            if (same)
                match = moniker;
            else
                Marshal.ReleaseComObject(moniker);
        }
        if (match is null)
            throw new IOException($"{camera.Name}: dispositivo não encontrado (desconectado?).");

        try
        {
            var iid = DsGuids.IID_IBaseFilter;
            match.BindToObject(null!, null, ref iid, out var filter);
            return new DirectShowSession(camera, filter);
        }
        finally
        {
            Marshal.ReleaseComObject(match);
        }
    }

    private static List<(IMoniker Moniker, CameraInfo Info)> EnumerateMonikers()
    {
        var result = new List<(IMoniker, CameraInfo)>();
        var type = Type.GetTypeFromCLSID(DsGuids.CLSID_SystemDeviceEnum, throwOnError: true)!;
        var devEnum = (ICreateDevEnum)Activator.CreateInstance(type)!;
        try
        {
            var category = DsGuids.CLSID_VideoInputDeviceCategory;
            int hr = devEnum.CreateClassEnumerator(ref category, out var enumMoniker, 0);
            // S_FALSE (1) means the category is empty.
            if (hr != 0 || enumMoniker is null)
                return result;

            var monikers = new IMoniker[1];
            while (enumMoniker.Next(1, monikers, IntPtr.Zero) == 0)
            {
                var (name, path) = ReadProperties(monikers[0]);
                // ADR-0007: anything but the original Tiny is ignored and never opened.
                if (!TinyIdentity.TryParseDevicePath(path, out var vid, out var pid) || !TinyIdentity.IsMatch(vid, pid))
                {
                    Marshal.ReleaseComObject(monikers[0]);
                    continue;
                }
                result.Add((monikers[0], new CameraInfo(result.Count, name ?? "(sem nome)", path, vid, pid)));
            }
            Marshal.ReleaseComObject(enumMoniker);
        }
        finally
        {
            Marshal.ReleaseComObject(devEnum);
        }
        return result;
    }

    private static (string? Name, string? Path) ReadProperties(IMoniker moniker)
    {
        var iid = DsGuids.IID_IPropertyBag;
        moniker.BindToStorage(null!, null, ref iid, out var bagObj);
        var bag = (IPropertyBag)bagObj;
        try
        {
            string? Read(string key) => bag.Read(key, out var v, IntPtr.Zero) == 0 ? v as string : null;
            return (Read("FriendlyName"), Read("DevicePath"));
        }
        finally
        {
            Marshal.ReleaseComObject(bag);
        }
    }
}
