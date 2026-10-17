namespace ImBridge.Windows.Ancs;

/// <summary>
/// Apple Notification Center Service UUIDs and enums. ANCS is a BLE service the
/// iPhone exposes; a paired accessory subscribes to be told when notifications
/// (including new iMessages) arrive. We use it as the instant "new message"
/// signal, then refresh over MAP.
/// </summary>
public static class AncsConstants
{
    public static readonly Guid ServiceUuid = Guid.Parse("7905F431-B5CE-4E99-A40F-4B1E122D00D0");
    public static readonly Guid NotificationSourceUuid = Guid.Parse("9FBF120D-6301-42D9-8C58-25E699A21DBD");
    public static readonly Guid ControlPointUuid = Guid.Parse("69D1D8F3-45E1-49A8-9821-9BBDFDAAD9D9");
    public static readonly Guid DataSourceUuid = Guid.Parse("22EAC6E9-24D6-4BB5-BE44-B36ACE7C7BFB");

    public enum EventId : byte { Added = 0, Modified = 1, Removed = 2 }

    public enum CategoryId : byte
    {
        Other = 0, IncomingCall = 1, MissedCall = 2, Voicemail = 3,
        Social = 4, Schedule = 5, Email = 6, News = 7,
        HealthAndFitness = 8, BusinessAndFinance = 9, Location = 10, Entertainment = 11,
    }
}

/// <summary>A decoded ANCS Notification Source event (8 bytes).</summary>
public sealed record AncsNotification(
    AncsConstants.EventId Event,
    AncsConstants.CategoryId Category,
    byte CategoryCount,
    uint NotificationUid)
{
    public static AncsNotification Parse(ReadOnlySpan<byte> data) => new(
        (AncsConstants.EventId)data[0],
        (AncsConstants.CategoryId)data[2],
        data[3],
        BitConverter.ToUInt32(data.Slice(4, 4)));
}
