using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using OpenGimbalCam.Core.Camera;

namespace OpenGimbalCam.Devices.Windows;

/// <summary>
/// Raw access to a UVC Extension Unit through IKsControl, mirroring
/// libdev.dll's uvc_xu_init / uvcExtGet / uvcExtSet:
/// <list type="number">
/// <item>enumerate IKsTopologyInfo nodes whose type is KSNODETYPE_DEV_SPECIFIC;</item>
/// <item>CreateNodeInstance(node, IID_IKsControl);</item>
/// <item>probe with KSPROPERTY_TYPE_SETSUPPORT|TOPOLOGY (flags 0x10000100) on the XU GUID;</item>
/// <item>GET = flags 0x10000001, SET = flags 0x10000002, 60-byte buffer.</item>
/// </list>
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class KsExtensionUnit : IExtensionUnit
{
    private IKsControl? _control;

    private KsExtensionUnit(IKsControl control, uint nodeId, Guid set)
    {
        _control = control;
        NodeId = nodeId;
        Guid = set;
    }

    public uint NodeId { get; }
    public Guid Guid { get; }
    public string Location => $"nó KS {NodeId}";

    internal static KsExtensionUnit? Find(object filter, Guid set)
    {
        if (filter is not IKsTopologyInfo topo || topo.get_NumNodes(out var count) != 0)
            return null;

        var iid = KsGuids.IID_IKsControl;
        for (uint node = 0; node < count; node++)
        {
            if (topo.get_NodeType(node, out var type) != 0 || type != KsGuids.KSNODETYPE_DEV_SPECIFIC)
                continue;
            if (topo.CreateNodeInstance(node, ref iid, out var instance) != 0 || instance is not IKsControl control)
                continue;

            var probe = new KspNode
            {
                Set = set,
                Id = 0,
                Flags = KsPropertyFlags.SetSupport | KsPropertyFlags.Topology,
                NodeId = node,
            };
            if (Call(control, probe, null, out _) == 0)
                return new KsExtensionUnit(control, node, set);
            Marshal.ReleaseComObject(control);
        }
        return null;
    }

    /// <summary>GET_CUR of a control selector. Returns the bytes the device produced.</summary>
    public byte[] Get(uint selector, int length = ObsbotXu.PayloadLength)
    {
        var buffer = new byte[length];
        int hr = Call(Control, Node(selector, KsPropertyFlags.Get), buffer, out var returned);
        if (hr != 0)
            throw new XuException(selector, "GET", hr, $"HRESULT 0x{hr:X8}");
        return returned < buffer.Length ? buffer[..(int)returned] : buffer;
    }

    /// <summary>SET_CUR of a control selector. The payload is zero-padded to <paramref name="length"/>.</summary>
    public void Set(uint selector, ReadOnlySpan<byte> payload, int length = ObsbotXu.PayloadLength)
    {
        if (payload.Length > length)
            throw new ArgumentException($"Payload de {payload.Length} bytes excede {length}.", nameof(payload));
        var buffer = new byte[length];
        payload.CopyTo(buffer);
        int hr = Call(Control, Node(selector, KsPropertyFlags.Set), buffer, out _);
        if (hr != 0)
            throw new XuException(selector, "SET", hr, $"HRESULT 0x{hr:X8}");
    }

    public void Dispose()
    {
        if (_control is not null)
        {
            Marshal.ReleaseComObject(_control);
            _control = null;
        }
    }

    private IKsControl Control => _control ?? throw new ObjectDisposedException(nameof(KsExtensionUnit));

    private KspNode Node(uint selector, KsPropertyFlags kind) => new()
    {
        Set = Guid,
        Id = selector,
        Flags = kind | KsPropertyFlags.Topology,
        NodeId = NodeId,
    };

    private static int Call(IKsControl control, KspNode node, byte[]? data, out uint returned)
    {
        int size = Marshal.SizeOf<KspNode>();
        var prop = Marshal.AllocHGlobal(size);
        var handle = data is null ? default : GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            Marshal.StructureToPtr(node, prop, false);
            var ptr = data is null ? IntPtr.Zero : handle.AddrOfPinnedObject();
            return control.KsProperty(prop, (uint)size, ptr, (uint)(data?.Length ?? 0), out returned);
        }
        finally
        {
            if (handle.IsAllocated)
                handle.Free();
            Marshal.FreeHGlobal(prop);
        }
    }
}
