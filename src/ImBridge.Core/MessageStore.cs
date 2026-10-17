using ImBridge.Core.Map;
using ImBridge.Core.Models;

namespace ImBridge.Core;

/// <summary>
/// Groups flat MAP listing entries into conversations keyed by normalized
/// address. Pure, in-memory, and testable; the UI binds to its conversations.
/// </summary>
public sealed class MessageStore
{
    private readonly Dictionary<string, Conversation> _byAddress = new();

    public IReadOnlyCollection<Conversation> Conversations => _byAddress.Values;

    public Conversation Upsert(Message message)
    {
        var key = Conversation.NormalizeAddress(message.SenderAddress);
        if (key.Length == 0) key = Conversation.NormalizeAddress(message.Sender);

        if (!_byAddress.TryGetValue(key, out var convo))
        {
            convo = new Conversation
            {
                Address = key,
                DisplayName = !string.IsNullOrWhiteSpace(message.Sender) ? message.Sender : key,
            };
            _byAddress[key] = convo;
        }

        if (convo.Messages.All(m => m.Handle != message.Handle))
            convo.Messages.Add(message);

        if (string.IsNullOrWhiteSpace(convo.DisplayName) && !string.IsNullOrWhiteSpace(message.Sender))
            convo.DisplayName = message.Sender;

        return convo;
    }

    public static Message ToMessage(MessageListing.Entry e)
    {
        var address = e.Direction == MessageDirection.Outgoing
            ? e.RecipientAddressing
            : e.SenderAddressing;

        return new Message
        {
            Handle = e.Handle,
            Sender = string.IsNullOrWhiteSpace(e.SenderName) ? address : e.SenderName,
            SenderAddress = address,
            Body = e.Subject,
            Timestamp = e.DateTime,
            Direction = e.Direction,
            Read = e.Read,
            MapType = e.Type,
        };
    }

    /// <summary>Conversations ordered by most recent message first.</summary>
    public IReadOnlyList<Conversation> Ordered() =>
        _byAddress.Values
            .OrderByDescending(c => c.Latest?.Timestamp ?? DateTimeOffset.MinValue)
            .ToList();
}
