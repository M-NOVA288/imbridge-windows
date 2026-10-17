using System.Text;
using ImBridge.Core.Models;
using ImBridge.Core.Obex;

namespace ImBridge.Core.Map;

/// <summary>
/// MAP Message Client Equipment (MCE) logic, built entirely on top of an
/// <see cref="IObexTransport"/>. It knows the OBEX conversation — CONNECT with
/// the MAS target, multi-packet GET reassembly, SETPATH, PUT — and nothing
/// about Bluetooth. That keeps it unit-testable with a fake transport.
/// </summary>
public sealed class MapClient
{
    private readonly IObexTransport _transport;
    private uint? _connectionId;
    private ushort _peerMaxPacket = 255;

    public MapClient(IObexTransport transport) => _transport = transport;

    private async Task<ObexResponse> ExchangeAsync(ObexPacket request, bool isConnect, CancellationToken ct)
    {
        var raw = await _transport.ExchangeAsync(request.Serialize(), ct).ConfigureAwait(false);
        return ObexResponse.Parse(raw, isConnect);
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await _transport.OpenAsync(ct).ConfigureAwait(false);

        var headers = new List<ObexHeader>
        {
            ObexHeader.Bytes(ObexHeaderId.Target, MapConstants.MasTargetUuid),
        };
        var resp = await ExchangeAsync(ObexPacket.Connect(ushort.MaxValue, headers), isConnect: true, ct)
            .ConfigureAwait(false);

        if (!resp.IsSuccess)
            throw new MapException($"CONNECT rejected by phone: {resp.Code}.");

        if (resp.PeerMaxPacketSize > 0) _peerMaxPacket = resp.PeerMaxPacketSize;
        _connectionId = resp.Find(ObexHeaderId.ConnectionId)?.AsFourByte();
    }

    private List<ObexHeader> BaseHeaders()
    {
        var list = new List<ObexHeader>();
        if (_connectionId is { } id) list.Add(ObexHeader.FourByte(ObexHeaderId.ConnectionId, id));
        return list;
    }

    /// <summary>Move the virtual current folder to an absolute path like telecom/msg/inbox.</summary>
    public async Task SetFolderAsync(string path, CancellationToken ct = default)
    {
        // Reset to root, then descend one segment at a time.
        await ExchangeAsync(ObexPacket.SetPath(0x02, BaseHeaders()), isConnect: false, ct).ConfigureAwait(false);
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var headers = BaseHeaders();
            headers.Add(ObexHeader.Text(ObexHeaderId.Name, segment));
            var resp = await ExchangeAsync(ObexPacket.SetPath(0x02, headers), isConnect: false, ct)
                .ConfigureAwait(false);
            if (!resp.IsSuccess)
                throw new MapException($"SETPATH to '{segment}' failed: {resp.Code}.");
        }
    }

    /// <summary>
    /// Run a GET to completion, following OBEX Continue (0x90) responses and
    /// concatenating every Body / End-of-Body chunk into one buffer.
    /// </summary>
    private async Task<(ObexResponseCode Code, byte[] Body)> GetAsync(
        IReadOnlyList<ObexHeader> firstHeaders, CancellationToken ct)
    {
        var body = new List<byte>();
        var headers = firstHeaders;
        bool first = true;

        while (true)
        {
            // Only the first request carries the Type / Name / AppParameters headers;
            // continuation requests are a bare GET-Final with the connection id.
            var request = new ObexPacket(ObexOpcode.GetFinal,
                first ? headers : BaseHeaders());
            first = false;

            var resp = await ExchangeAsync(request, isConnect: false, ct).ConfigureAwait(false);

            foreach (var h in resp.Headers)
                if (h.Id is ObexHeaderId.Body or ObexHeaderId.EndOfBody)
                    body.AddRange(h.Value);

            if (resp.IsContinue) continue;
            if (!resp.IsSuccess) throw new MapException($"GET failed: {resp.Code}.");
            return (resp.Code, body.ToArray());
        }
    }

    /// <summary>Fetch and parse the listing for the current folder.</summary>
    public async Task<IReadOnlyList<MessageListing.Entry>> GetMessageListingAsync(
        ushort maxCount = 256, CancellationToken ct = default)
    {
        var headers = BaseHeaders();
        headers.Add(ObexHeader.AsciiType(MapConstants.TypeMessagesListing));
        headers.Add(ObexHeader.Text(ObexHeaderId.Name, ""));   // current folder
        headers.Add(ObexHeader.Bytes(ObexHeaderId.AppParameters, AppParameters.Build(
            (MapConstants.AppParamTag.MaxListCount, AppParameters.U16(maxCount)))));

        var (_, body) = await GetAsync(headers, ct).ConfigureAwait(false);
        return MessageListing.Parse(Encoding.UTF8.GetString(body));
    }

    /// <summary>Download a single message by its MAP handle and parse the bMessage.</summary>
    public async Task<BMessage> GetMessageAsync(string handle, CancellationToken ct = default)
    {
        var headers = BaseHeaders();
        headers.Add(ObexHeader.AsciiType(MapConstants.TypeMessage));
        headers.Add(ObexHeader.Text(ObexHeaderId.Name, handle));
        headers.Add(ObexHeader.Bytes(ObexHeaderId.AppParameters, AppParameters.Build(
            (MapConstants.AppParamTag.Attachment, new byte[] { 0x00 }),
            (MapConstants.AppParamTag.Charset, new byte[] { MapConstants.CharsetUtf8 }))));

        var (_, body) = await GetAsync(headers, ct).ConfigureAwait(false);
        return BMessage.Parse(Encoding.UTF8.GetString(body));
    }

    /// <summary>
    /// Send a message by PUTting a bMessage into the outbox. The body is split
    /// across packets if it exceeds the peer's maximum packet size.
    /// </summary>
    public async Task SendMessageAsync(string recipient, string text, string type = "SMS_GSM",
        CancellationToken ct = default)
    {
        await SetFolderAsync("telecom/msg/outbox", ct).ConfigureAwait(false);

        var bmsg = new BMessage { RecipientAddress = recipient, Body = text, Type = type };
        var payload = Encoding.UTF8.GetBytes(bmsg.BuildForSend());

        // Reserve room for opcode, length, connection-id, type and body header overhead.
        int chunkSize = Math.Max(64, _peerMaxPacket - 32);
        int offset = 0;

        while (offset < payload.Length)
        {
            int take = Math.Min(chunkSize, payload.Length - offset);
            bool last = offset + take >= payload.Length;

            var headers = BaseHeaders();
            if (offset == 0)
                headers.Add(ObexHeader.AsciiType(MapConstants.TypeMessage));
            headers.Add(ObexHeader.Bytes(
                last ? ObexHeaderId.EndOfBody : ObexHeaderId.Body,
                payload.AsSpan(offset, take).ToArray()));

            var resp = await ExchangeAsync(
                new ObexPacket(last ? ObexOpcode.PutFinal : ObexOpcode.Put, headers),
                isConnect: false, ct).ConfigureAwait(false);

            if (!last && !resp.IsContinue)
                throw new MapException($"PUT chunk rejected: {resp.Code}.");
            if (last && !resp.IsSuccess)
                throw new MapException($"Send failed: {resp.Code}.");

            offset += take;
        }
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        try
        {
            await ExchangeAsync(new ObexPacket(ObexOpcode.Disconnect, BaseHeaders()),
                isConnect: false, ct).ConfigureAwait(false);
        }
        catch (MapException) { /* best effort */ }
    }
}

public sealed class MapException : Exception
{
    public MapException(string message) : base(message) { }
}
