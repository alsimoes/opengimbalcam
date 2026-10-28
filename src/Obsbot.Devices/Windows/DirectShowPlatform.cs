using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Obsbot.Core.Camera;

namespace Obsbot.Devices.Windows;

/// <summary>
/// Video capture devices from the DirectShow system device enumerator, the same
/// path libdev.dll takes (CLSID_SystemDeviceEnum + CLSID_VideoInputDeviceCategory).
/// COM objects are apartment bound: use it through <see cref="CameraPlatform.Create"/>,
/// which serializes every call on one MTA thread.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class DirectShowPlatform : ICameraPlatform
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
                int? vid = null, pid = null;
                var m = VidPidRegex().Match(path ?? "");
                if (m.Success)
                {
                    vid = Convert.ToInt32(m.Groups[1].Value, 16);
                    pid = Convert.ToInt32(m.Groups[2].Value, 16);
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

    [GeneratedRegex(@"vid_([0-9a-f]{4})&pid_([0-9a-f]{4})", RegexOptions.IgnoreCase)]
    private static partial Regex VidPidRegex();
}
