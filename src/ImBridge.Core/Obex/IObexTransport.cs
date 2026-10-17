namespace ImBridge.Core.Obex;

/// <summary>
/// The physical link underneath OBEX. The only thing the protocol layer needs
/// is: send one request packet, get one response packet back. The real
/// implementation (ImBridge.Windows) wraps an RFCOMM socket; tests use a fake.
/// </summary>
public interface IObexTransport : IAsyncDisposable
{
    Task OpenAsync(CancellationToken ct = default);

    /// <summary>Write a full request packet and read exactly one response packet.</summary>
    Task<byte[]> ExchangeAsync(byte[] requestPacket, CancellationToken ct = default);
}
