using ImBridge.Core;
using ImBridge.Core.Map;
using ImBridge.Core.Models;
using Xunit;

namespace ImBridge.Core.Tests;

public class ListingAndStoreTests
{
    [Fact]
    public void Parses_map_datetime_with_offset()
    {
        var dt = MessageListing.ParseMapDateTime("20260115T143005-0600");
        Assert.Equal(2026, dt.Year);
        Assert.Equal(1, dt.Month);
        Assert.Equal(15, dt.Day);
        Assert.Equal(14, dt.Hour);
        Assert.Equal(TimeSpan.FromHours(-6), dt.Offset);
    }

    [Fact]
    public void Malformed_listing_returns_empty_not_throw()
    {
        Assert.Empty(MessageListing.Parse("<not valid xml"));
        Assert.Empty(MessageListing.Parse(""));
    }

    [Fact]
    public void Store_groups_messages_by_normalized_address()
    {
        var store = new MessageStore();
        store.Upsert(new Message
        {
            Handle = "1", Sender = "Alex", SenderAddress = "+1 (555) 123-4567",
            Body = "first", Timestamp = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Direction = MessageDirection.Incoming,
        });
        store.Upsert(new Message
        {
            Handle = "2", Sender = "Alex", SenderAddress = "+15551234567",
            Body = "second", Timestamp = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero),
            Direction = MessageDirection.Incoming,
        });

        var convo = Assert.Single(store.Conversations);
        Assert.Equal(2, convo.Messages.Count);              // same person despite formatting
        Assert.Equal("second", convo.Latest!.Body);
    }

    [Fact]
    public void Duplicate_handles_are_not_added_twice()
    {
        var store = new MessageStore();
        var msg = new Message
        {
            Handle = "42", Sender = "Sam", SenderAddress = "sam@example.com",
            Body = "hi", Timestamp = DateTimeOffset.UtcNow, Direction = MessageDirection.Incoming,
        };
        store.Upsert(msg);
        store.Upsert(msg);
        Assert.Single(store.Conversations.Single().Messages);
    }
}
