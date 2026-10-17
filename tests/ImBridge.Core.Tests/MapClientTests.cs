using System.Text;
using ImBridge.Core.Map;
using ImBridge.Core.Obex;
using Xunit;

namespace ImBridge.Core.Tests;

public class MapClientTests
{
    private const string ListingXml =
        "<MAP-msg-listing version=\"1.0\">" +
        "<msg handle=\"0400000000000001\" subject=\"Hey there\" datetime=\"20260115T143005-0600\" " +
        "sender_name=\"Alex\" sender_addressing=\"+15551234567\" recipient_addressing=\"+15559999999\" " +
        "type=\"SMS_GSM\" read=\"no\" direction=\"incoming\"/>" +
        "</MAP-msg-listing>";

    [Fact]
    public async Task Connect_then_get_listing_reassembles_multipacket_body()
    {
        var xml = Encoding.UTF8.GetBytes(ListingXml);
        int half = xml.Length / 2;

        var transport = new FakeTransport()
            .Enqueue(FakeTransport.ConnectSuccess(connectionId: 1))
            // First GET: a Continue with the first half of the body.
            .Enqueue(FakeTransport.Response(ObexResponseCode.Continue, Array.Empty<byte>(),
                ObexHeader.Bytes(ObexHeaderId.Body, xml[..half])))
            // Continuation GET: Success with the rest as End-of-Body.
            .Enqueue(FakeTransport.Response(ObexResponseCode.Success, Array.Empty<byte>(),
                ObexHeader.Bytes(ObexHeaderId.EndOfBody, xml[half..])));

        var map = new MapClient(transport);
        await map.ConnectAsync();
        var entries = await map.GetMessageListingAsync();

        var entry = Assert.Single(entries);
        Assert.Equal("0400000000000001", entry.Handle);
        Assert.Equal("Alex", entry.SenderName);
        Assert.Equal("+15551234567", entry.SenderAddressing);
        Assert.True(transport.Opened);
        Assert.Equal(3, transport.SentRequests.Count);      // CONNECT + 2 GETs
    }

    [Fact]
    public async Task Connect_carries_connection_id_into_later_requests()
    {
        var transport = new FakeTransport()
            .Enqueue(FakeTransport.ConnectSuccess(connectionId: 0x11223344))
            .Enqueue(FakeTransport.Response(ObexResponseCode.Success, Array.Empty<byte>(),
                ObexHeader.Bytes(ObexHeaderId.EndOfBody, Encoding.UTF8.GetBytes(ListingXml))));

        var map = new MapClient(transport);
        await map.ConnectAsync();
        await map.GetMessageListingAsync();

        // The GET (second request) must begin with GET-Final and include the connection-id header.
        var getRequest = transport.SentRequests[1];
        Assert.Equal((byte)ObexOpcode.GetFinal, getRequest[0]);
        Assert.Contains((byte)ObexHeaderId.ConnectionId, getRequest);
    }

    [Fact]
    public async Task Rejected_connect_throws()
    {
        var transport = new FakeTransport()
            .Enqueue(FakeTransport.Response(ObexResponseCode.Forbidden, new byte[] { 0x10, 0x00, 0x20, 0x00 }));

        var map = new MapClient(transport);
        await Assert.ThrowsAsync<MapException>(() => map.ConnectAsync());
    }
}
