using System.Globalization;
using System.Xml.Linq;
using ImBridge.Core.Models;

namespace ImBridge.Core.Map;

/// <summary>
/// Parses the MAP messages-listing XML returned by a GET with
/// type x-bt/MAP-msg-listing. Each &lt;msg&gt; element describes one message.
/// </summary>
public static class MessageListing
{
    public sealed record Entry(
        string Handle,
        string Subject,
        DateTimeOffset DateTime,
        string SenderName,
        string SenderAddressing,
        string RecipientAddressing,
        string Type,
        bool Read,
        MessageDirection Direction);

    public static IReadOnlyList<Entry> Parse(string xml)
    {
        var entries = new List<Entry>();
        if (string.IsNullOrWhiteSpace(xml)) return entries;

        XDocument doc;
        try { doc = XDocument.Parse(xml); }
        catch (System.Xml.XmlException) { return entries; }

        foreach (var msg in doc.Descendants("msg"))
        {
            string Attr(string name) => msg.Attribute(name)?.Value ?? "";

            entries.Add(new Entry(
                Handle: Attr("handle"),
                Subject: Attr("subject"),
                DateTime: ParseMapDateTime(Attr("datetime")),
                SenderName: Attr("sender_name"),
                SenderAddressing: Attr("sender_addressing"),
                RecipientAddressing: Attr("recipient_addressing"),
                Type: Attr("type"),
                Read: Attr("read").Equals("yes", StringComparison.OrdinalIgnoreCase),
                Direction: Attr("direction").Equals("outgoing", StringComparison.OrdinalIgnoreCase)
                    ? MessageDirection.Outgoing
                    : MessageDirection.Incoming));
        }
        return entries;
    }

    /// <summary>MAP timestamps look like 20260115T143005 or 20260115T143005-0600.</summary>
    public static DateTimeOffset ParseMapDateTime(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return DateTimeOffset.MinValue;

        string[] withOffset = { "yyyyMMddTHHmmsszzz", "yyyyMMddTHHmmsszz" };
        // Normalize a trailing +HHMM / -HHMM into +HH:MM for the parser.
        var v = value;
        if (v.Length >= 5 && (v[^5] == '+' || v[^5] == '-'))
            v = v[..^2] + ":" + v[^2..];

        if (DateTimeOffset.TryParseExact(v, withOffset, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dto))
            return dto;

        if (DateTime.TryParseExact(value, "yyyyMMddTHHmmss", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal, out var local))
            return new DateTimeOffset(local);

        return DateTimeOffset.MinValue;
    }
}
