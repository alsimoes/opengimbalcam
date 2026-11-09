using System.Runtime.InteropServices;

namespace OpenGimbalCam.Devices.Windows;

// Kernel Streaming interfaces exposed by usbvideo.sys through the DirectShow
// capture filter. libdev.dll walks IKsTopologyInfo to find the UVC Extension
// Unit node and then talks to it with IKsControl::KsProperty (KSP_NODE).

internal static class KsGuids
{
    public static readonly Guid IID_IKsControl = new("28F54685-06FD-11D2-B27A-00A0C9223196");

    // Node type reported by usbvideo.sys for every UVC Extension Unit.
    public static readonly Guid KSNODETYPE_DEV_SPECIFIC = new("941C7AC0-C559-11D0-8A2B-00A0C9255AC1");
}

[Flags]
internal enum KsPropertyFlags : uint
{
    Get = 0x00000001,
    Set = 0x00000002,
    BasicSupport = 0x00000200,
    SetSupport = 0x00000100,
    Topology = 0x10000000,
}

[StructLayout(LayoutKind.Sequential)]
internal struct KspNode
{
    public Guid Set;
    public uint Id;
    public KsPropertyFlags Flags;
    public uint NodeId;
    public uint Reserved;
}

[ComImport, Guid("28F54685-06FD-11D2-B27A-00A0C9223196"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IKsControl
{
    [PreserveSig]
    int KsProperty(IntPtr property, uint propertyLength, IntPtr propertyData, uint dataLength, out uint bytesReturned);

    [PreserveSig]
    int KsMethod(IntPtr method, uint methodLength, IntPtr methodData, uint dataLength, out uint bytesReturned);

    [PreserveSig]
    int KsEvent(IntPtr ksEvent, uint eventLength, IntPtr eventData, uint dataLength, out uint bytesReturned);
}

[ComImport, Guid("720D4AC0-7533-11D0-A5D6-28DB04C10000"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IKsTopologyInfo
{
    [PreserveSig]
    int get_NumCategories(out uint count);

    [PreserveSig]
    int get_Category(uint index, out Guid category);

    [PreserveSig]
    int get_NumConnections(out uint count);

    [PreserveSig]
    int get_ConnectionInfo(uint index, IntPtr connectionInfo);

    [PreserveSig]
    int get_NodeName(uint nodeId, IntPtr name, uint bufferSize, out uint nameLength);

    [PreserveSig]
    int get_NumNodes(out uint count);

    [PreserveSig]
    int get_NodeType(uint nodeId, out Guid nodeType);

    [PreserveSig]
    int CreateNodeInstance(uint nodeId, [In] ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object? instance);
}
