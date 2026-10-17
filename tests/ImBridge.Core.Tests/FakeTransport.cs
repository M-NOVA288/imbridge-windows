using System.Buffers.Binary;
using ImBridge.Core.Obex;

namespace ImBridge.Core.Tests;

/// <summary>
/// An in-memory <see cref="IObexTransport"/> that returns a scripted queue of
/// response packets and records every request it was given. Lets us drive the
/// MAP client through a whole conversation with no Bluetooth.
/// </summary>
public sealed class FakeTransport : IObexTransport
{
    private readonly Queue<byte[]> _responses = new();
    public List<byte[]> SentRequests { get; } = new();
    public bool Opened { get; private set; }

    public FakeTransport Enqueue(byte[] responsePacket)
    {
        _responses.Enqueue(responsePacket);
        return this;
    }

    public Task OpenAsync(CancellationToken ct = default)
    {
        Opened = true;
        return Task.CompletedTask;
    }

    public Task<byte[]> ExchangeAsync(byte[] requestPacket, CancellationToken ct = default)
    {
        SentRequests.Add(requestPacket);
        if (_responses.Count == 0)
            throw new InvalidOperationException("FakeTransport ran out of scripted responses.");
        return Task.FromResult(_responses.Dequeue());
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // --- helpers to build response packets ----------------------------------

    public static byte[] Response(ObexResponseCode code, byte[] fixedFields, params ObexHeader[] headers)
    {
        int headerBytes = headers.Sum(h => h.WireLength);
        int total = 3 + fixedFields.Length + headerBytes;
        var buf = new byte[total];
        buf[0] = (byte)code;
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(1), (ushort)total);
        fixedFields.CopyTo(buf, 3);
        int o = 3 + fixedFields.Length;
        foreach (var h in headers) { h.WriteTo(buf.AsSpan(o)); o += h.WireLength; }
        return buf;
    }

    /// <summary>A CONNECT success response carrying a connection id.</summary>
    public static byte[] ConnectSuccess(uint connectionId, ushort maxPacket = 0x2000)
    {
        var fixedFields = new byte[4];
        fixedFields[0] = 0x10;
        BinaryPrimitives.WriteUInt16BigEndian(fixedFields.AsSpan(2), maxPacket);
        return Response(ObexResponseCode.Success, fixedFields,
            ObexHeader.FourByte(ObexHeaderId.ConnectionId, connectionId));
    }
}
