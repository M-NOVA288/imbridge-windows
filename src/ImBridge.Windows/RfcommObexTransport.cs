using System.Buffers.Binary;
using ImBridge.Core.Obex;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace ImBridge.Windows;

/// <summary>
/// An <see cref="IObexTransport"/> backed by a classic-Bluetooth RFCOMM socket
/// to the phone's Message Access Service. One request packet out, one response
/// packet in.
/// </summary>
public sealed class RfcommObexTransport : IObexTransport
{
    private readonly RfcommDeviceService _service;
    private StreamSocket? _socket;
    private DataWriter? _writer;
    private DataReader? _reader;

    public RfcommObexTransport(RfcommDeviceService service) => _service = service;

    public async Task OpenAsync(CancellationToken ct = default)
    {
        _socket = new StreamSocket();
        await _socket.ConnectAsync(
            _service.ConnectionHostName,
            _service.ConnectionServiceName,
            SocketProtectionLevel.BluetoothEncryptionAllowNullAuthentication).AsTask(ct);

        _writer = new DataWriter(_socket.OutputStream);
        _reader = new DataReader(_socket.InputStream) { InputStreamOptions = InputStreamOptions.Partial };
    }

    public async Task<byte[]> ExchangeAsync(byte[] requestPacket, CancellationToken ct = default)
    {
        if (_writer is null || _reader is null)
            throw new InvalidOperationException("Transport is not open. Call OpenAsync first.");

        _writer.WriteBytes(requestPacket);
        await _writer.StoreAsync().AsTask(ct);
        await _writer.FlushAsync().AsTask(ct);

        // Every OBEX packet starts with [code:1][length:2 BE]; read that, then the rest.
        var header = await ReadExactAsync(3, ct);
        int total = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(1, 2));
        if (total < 3) throw new InvalidOperationException($"Invalid OBEX length {total}.");

        var packet = new byte[total];
        header.CopyTo(packet, 0);
        if (total > 3)
        {
            var rest = await ReadExactAsync(total - 3, ct);
            rest.CopyTo(packet, 3);
        }
        return packet;
    }

    private async Task<byte[]> ReadExactAsync(int count, CancellationToken ct)
    {
        var reader = _reader!;
        while (reader.UnconsumedBufferLength < count)
        {
            uint loaded = await reader.LoadAsync((uint)(count - reader.UnconsumedBufferLength)).AsTask(ct);
            if (loaded == 0) throw new IOException("Bluetooth connection closed by the phone.");
        }
        var buf = new byte[count];
        reader.ReadBytes(buf);
        return buf;
    }

    public ValueTask DisposeAsync()
    {
        _writer?.Dispose();
        _reader?.Dispose();
        _socket?.Dispose();
        return ValueTask.CompletedTask;
    }
}
