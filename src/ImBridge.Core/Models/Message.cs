namespace ImBridge.Core.Models;

public enum MessageDirection { Incoming, Outgoing }

/// <summary>One message as surfaced from the phone's MAP message store.</summary>
public sealed record Message
{
    public required string Handle { get; init; }      // MAP message handle (hex)
    public required string Sender { get; init; }      // display name or address
    public required string SenderAddress { get; init; }
    public string Body { get; init; } = "";
    public DateTimeOffset Timestamp { get; init; }
    public MessageDirection Direction { get; init; }
    public bool Read { get; init; }

    /// <summary>
    /// MAP message type as reported by the phone (SMS_GSM, SMS_CDMA, MMS, EMAIL, IM).
    /// iMessages are delivered to MAP clients tagged as SMS or MMS; see README.
    /// </summary>
    public string MapType { get; init; } = "SMS_GSM";
}
