using OpenGimbalCam.Core.Camera;
using OpenGimbalCam.Core.Protocol;
using Xunit;

namespace OpenGimbalCam.Tests;

public class ProtocolTests
{
    [Fact]
    public void Crc16Usb_matches_catalog_check_value()
    {
        Assert.Equal(0xB4C8, Crc16Usb.Compute("123456789"u8));
    }

    [Fact]
    public void Message_header_layout_matches_libdev()
    {
        var msg = new RemoMessage(RemoPacketType.Type2, 0x1234, Sender: 3, Receiver: 0xE, CommandSet: 1, CommandId: 0x1CD, [0xAB, 0xCD]);
        var frame = msg.Encode();

        Assert.Equal(14, frame.Length);
        Assert.Equal(0xAA, frame[0]);
        Assert.Equal(new byte[] { 0x00, 0x0E }, frame[1..3]);   // length, big-endian
        Assert.Equal(0x12, frame[3]);                           // version 1 | type 2
        Assert.Equal(new byte[] { 0x12, 0x34 }, frame[4..6]);   // sequence, big-endian
        Assert.Equal(0x00, frame[8]);
        Assert.Equal(0xE3, frame[9]);                           // receiver<<4 | sender
        Assert.Equal(new byte[] { 0x11, 0xCD }, frame[10..12]); // set<<12 | id
        Assert.Equal(new byte[] { 0xAB, 0xCD }, frame[12..]);

        var zeroed = (byte[])frame.Clone();
        zeroed[6] = zeroed[7] = 0;
        ushort crc = Crc16Usb.Compute(zeroed);
        Assert.Equal(new byte[] { (byte)(crc >> 8), (byte)crc }, frame[6..8]);
    }

    [Fact]
    public void Message_roundtrip_and_crc_rejection()
    {
        var msg = new RemoMessage(RemoPacketType.Type0, 7, 1, 0xB, 3, 0x3D, [1, 2, 3]);
        var frame = msg.Encode();

        var padded = new byte[60];
        frame.CopyTo(padded, 0);
        var decoded = RemoMessage.TryDecode(padded, out var error);
        Assert.Null(error);
        Assert.NotNull(decoded);
        Assert.Equal(msg with { Payload = decoded!.Payload }, decoded);
        Assert.Equal(msg.Payload, decoded.Payload);

        padded[13] ^= 0xFF;
        Assert.Null(RemoMessage.TryDecode(padded, out error));
        Assert.Equal("CRC inválido", error);
    }

    [Fact]
    public void Message_longer_than_xu_payload_is_rejected()
    {
        var msg = new RemoMessage(RemoPacketType.Type2, 1, 1, 1, 1, 1, new byte[49]);
        Assert.Throws<ArgumentException>(() => msg.Encode());
    }

    [Fact]
    public void Direct_commands_use_id_length_payload_layout()
    {
        Assert.Equal(new byte[] { 0x14, 0x01, 0x01 }, DirectCommands.Find("flip-horizontal")!.Build([1]));
        Assert.Equal(new byte[] { 0x16, 0x02, 0x02, 0x00 }, DirectCommands.Find("ai-mode")!.Build([2, 0]));
        Assert.Equal(new byte[] { 0x0B, 0x02, 0x2C, 0x01 }, DirectCommands.Find("suspend-time")!.Build([300]));
    }

    [Fact]
    public void Pan_tilt_relative_encodes_direction_and_speed()
    {
        Assert.Equal(new byte[] { 0x09, 0x04, 0x01, 0x64, 0xFF, 0x32 }, DirectCommands.PanTiltRelative(100, -50));
        Assert.Equal(new byte[] { 0x09, 0x04, 0x00, 0x00, 0x00, 0x00 }, DirectCommands.PanTiltRelative(0, 0));
    }

    [Fact]
    public void Firmware_selectors_are_flagged_dangerous()
    {
        Assert.True(ObsbotXu.IsDangerous(3));
        Assert.True(ObsbotXu.IsDangerous(5));
        Assert.False(ObsbotXu.IsDangerous(2));
        Assert.False(ObsbotXu.IsDangerous(6));
    }

    [Fact]
    public void V1_requests_translate_logical_ids_to_wire_nibbles()
    {
        // aiSetTargetSelectR(true): sender 3, receiver 0x0B -> 14, set 3 -> 3, id 0x0F -> 0x67
        var wire = RemoV1.ToWire(RemoV1.TargetSelect(true), 0x1234);
        var frame = wire.Encode();
        Assert.Equal("AA000D1012", Convert.ToHexString(frame[..5]));
        Assert.Equal(0xE3, frame[9]);
        Assert.Equal("306700", Convert.ToHexString(frame[10..]));
        Assert.Equal(wire.ToString(), RemoMessage.TryDecode(frame, out _)?.ToString());
    }

    [Fact]
    public void V1_run_status_uses_set1_id_0x3C2()
    {
        var wire = RemoV1.ToWire(RemoV1.DevRunStatus(3), 1);
        Assert.Equal((1, 14, 1, 0x3C2), (wire.Sender, wire.Receiver, wire.CommandSet, wire.CommandId));
        Assert.Equal(new byte[] { 1, 3 }, wire.Payload);
    }

    [Fact]
    public void V1_rejects_commands_missing_from_its_tables()
    {
        var zoneTrack = new RemoRequest(3, 3, 0x5D, [1], "aiSetZoneTrackStateR");
        Assert.False(RemoV1.IsSupported(zoneTrack));
        Assert.Throws<NotSupportedException>(() => RemoV1.ToWire(zoneTrack, 1));
    }

    [Fact]
    public void Hdr_is_direct_command_01()
    {
        Assert.Equal(new byte[] { 0x01, 0x01, 0x01 }, DirectCommands.Find("hdr")!.Build([1]));
    }
}
