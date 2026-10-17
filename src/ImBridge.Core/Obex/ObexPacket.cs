using System.Buffers.Binary;

namespace ImBridge.Core.Obex;

/// <summary>
/// An OBEX request packet: [opcode:1][length:2 BE][optional fixed fields][headers...].
/// CONNECT and SETPATH carry a few fixed bytes before the headers; everything
/// else is just headers.
/// </summary>
public sealed class ObexPacket
{
    public ObexOpcode Opcode { get; }
    public IReadOnlyList<ObexHeader> Headers { get; }
    private readonly byte[] _fixedFields;

    public ObexPacket(ObexOpcode opcode, IReadOnlyList<ObexHeader> headers, byte[]? fixedFields = null)
    {
        Opcode = opcode;
        Headers = headers;
        _fixedFields = fixedFields ?? Array.Empty<byte>();
    }

    /// <summary>CONNECT with the standard fixed fields (version 1.0, no flags).</summary>
    public static ObexPacket Connect(ushort maxPacketSize, IReadOnlyList<ObexHeader> headers)
    {
        var fixedFields = new byte[4];
        fixedFields[0] = 0x10;  // OBEX version 1.0
        fixedFields[1] = 0x00;  // flags
        BinaryPrimitives.WriteUInt16BigEndian(fixedFields.AsSpan(2), maxPacketSize);
        return new ObexPacket(ObexOpcode.Connect, headers, fixedFields);
    }

    /// <summary>SETPATH. flags 0x02 = go to the root; 0x01 with an empty name = go up one level.</summary>
    public static ObexPacket SetPath(byte flags, IReadOnlyList<ObexHeader> headers)
        => new(ObexOpcode.SetPath, headers, new byte[] { flags, 0x00 });

    public byte[] Serialize()
    {
        int headerBytes = 0;
        foreach (var h in Headers) headerBytes += h.WireLength;
        int total = 3 + _fixedFields.Length + headerBytes;

        var buf = new byte[total];
        buf[0] = (byte)Opcode;
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(1), (ushort)total);
        _fixedFields.CopyTo(buf.AsSpan(3));

        int offset = 3 + _fixedFields.Length;
        foreach (var h in Headers)
        {
            h.WriteTo(buf.AsSpan(offset));
            offset += h.WireLength;
        }
        return buf;
    }
}

/// <summary>A parsed OBEX response packet.</summary>
public sealed class ObexResponse
{
    public ObexResponseCode Code { get; }
    public IReadOnlyList<ObexHeader> Headers { get; }

    /// <summary>CONNECT responses report the peer's maximum packet size; 0 otherwise.</summary>
    public ushort PeerMaxPacketSize { get; }

    public bool IsSuccess => Code is ObexResponseCode.Success or ObexResponseCode.Created;
    public bool IsContinue => Code == ObexResponseCode.Continue;

    public ObexResponse(ObexResponseCode code, IReadOnlyList<ObexHeader> headers, ushort peerMaxPacketSize)
    {
        Code = code;
        Headers = headers;
        PeerMaxPacketSize = peerMaxPacketSize;
    }

    public ObexHeader? Find(ObexHeaderId id)
    {
        foreach (var h in Headers) if (h.Id == id) return h;
        return null;
    }

    /// <param name="isConnectResponse">
    /// CONNECT responses include 4 fixed bytes (version, flags, max packet size)
    /// before the headers; pass true so they are read, not treated as a header.
    /// </param>
    public static ObexResponse Parse(ReadOnlySpan<byte> packet, bool isConnectResponse)
    {
        var code = (ObexResponseCode)packet[0];
        int declaredLength = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(1, 2));
        int offset = 3;
        ushort peerMax = 0;

        if (isConnectResponse && declaredLength >= 7)
        {
            peerMax = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(5, 2));
            offset = 7;
        }

        var headers = new List<ObexHeader>();
        while (offset < declaredLength)
            headers.Add(ObexHeader.Parse(packet, ref offset));

        return new ObexResponse(code, headers, peerMax);
    }
}
