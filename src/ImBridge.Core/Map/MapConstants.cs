namespace ImBridge.Core.Map;

/// <summary>Message Access Profile constants (Bluetooth MAP 1.4).</summary>
public static class MapConstants
{
    /// <summary>MAS (Message Access Service) OBEX Target UUID.</summary>
    public static readonly byte[] MasTargetUuid =
    {
        0xbb, 0x58, 0x2b, 0x40, 0x42, 0x0c, 0x11, 0xdb,
        0xb0, 0xde, 0x08, 0x00, 0x20, 0x0c, 0x9a, 0x66,
    };

    // OBEX Type header values used by MAP.
    public const string TypeMessagesListing = "x-bt/MAP-msg-listing";
    public const string TypeFolderListing = "x-obex/folder-listing";
    public const string TypeMessage = "x-bt/message";
    public const string TypeMessageStatus = "x-bt/messageStatus";
    public const string TypeSetNotificationRegistration = "x-bt/MAP-NotificationRegistration";

    // Application-parameter tag ids (TLV triplets inside the AppParameters header).
    public enum AppParamTag : byte
    {
        MaxListCount = 0x01,
        ListStartOffset = 0x02,
        FilterMessageType = 0x03,
        Attachment = 0x0A,
        Charset = 0x14,
        MessageListingSize = 0x12,
        NotificationStatus = 0x0E,
    }

    // Charset values for the GET-message request.
    public const byte CharsetNative = 0x00;
    public const byte CharsetUtf8 = 0x01;
}
