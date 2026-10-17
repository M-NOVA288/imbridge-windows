using ImBridge.Core.Obex;
using Xunit;

namespace ImBridge.Core.Tests;

public class ObexTests
{
    [Fact]
    public void Connect_packet_has_correct_framing_and_target()
    {
        var target = new byte[] { 0xAA, 0xBB, 0xCC };
        var packet = ObexPacket.Connect(0xFFFF, new[] { ObexHeader.Bytes(ObexHeaderId.Target, target) });
        var bytes = packet.Serialize();

        Assert.Equal(0x80, bytes[0]);                       // CONNECT opcode
        int declared = (bytes[1] << 8) | bytes[2];
        Assert.Equal(bytes.Length, declared);               // length field matches real length
        Assert.Equal(0x10, bytes[3]);                       // version 1.0
        Assert.Equal(0x00, bytes[4]);                       // flags
        Assert.Equal(0xFF, bytes[5]);                       // max packet high byte
        Assert.Equal(0xFF, bytes[6]);
        Assert.Equal((byte)ObexHeaderId.Target, bytes[7]);  // first header is Target
        int headerLen = (bytes[8] << 8) | bytes[9];
        Assert.Equal(3 + target.Length, headerLen);
    }

    [Fact]
    public void Text_header_round_trips_through_utf16be()
    {
        var header = ObexHeader.Text(ObexHeaderId.Name, "inbox");
        var buf = new byte[header.WireLength];
        header.WriteTo(buf);

        int offset = 0;
        var parsed = ObexHeader.Parse(buf, ref offset);
        Assert.Equal(ObexHeaderId.Name, parsed.Id);
        Assert.Equal("inbox", parsed.AsText());
        Assert.Equal(buf.Length, offset);
    }

    [Fact]
    public void FourByte_header_round_trips()
    {
        var header = ObexHeader.FourByte(ObexHeaderId.ConnectionId, 0x01020304);
        var buf = new byte[header.WireLength];
        header.WriteTo(buf);

        int offset = 0;
        var parsed = ObexHeader.Parse(buf, ref offset);
        Assert.Equal(0x01020304u, parsed.AsFourByte());
    }

    [Fact]
    public void Connect_response_reads_fixed_fields_and_connection_id()
    {
        var raw = FakeTransport.ConnectSuccess(connectionId: 7, maxPacket: 0x1000);
        var resp = ObexResponse.Parse(raw, isConnectResponse: true);

        Assert.True(resp.IsSuccess);
        Assert.Equal(0x1000, resp.PeerMaxPacketSize);
        Assert.Equal(7u, resp.Find(ObexHeaderId.ConnectionId)!.AsFourByte());
    }
}
