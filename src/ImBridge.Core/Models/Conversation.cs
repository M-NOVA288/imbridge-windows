namespace ImBridge.Core.Models;

/// <summary>
/// Messages grouped by the other party. MAP has no real thread concept, so we
/// group by normalized address on the client side.
/// </summary>
public sealed class Conversation
{
    public required string Address { get; init; }
    public string DisplayName { get; set; } = "";
    public List<Message> Messages { get; } = new();

    public Message? Latest => Messages.Count == 0
        ? null
        : Messages.MaxBy(m => m.Timestamp);

    public static string NormalizeAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return "";
        if (address.Contains('@')) return address.Trim().ToLowerInvariant();
        // Phone number: keep a leading +, drop spaces, dashes, parens.
        var digits = new string(address.Where(c => char.IsDigit(c) || c == '+').ToArray());
        return digits;
    }
}
