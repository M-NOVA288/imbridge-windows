namespace ImBridge.Core.Obex;

/// <summary>OBEX request opcodes. The high bit (0x80) marks the final packet.</summary>
public enum ObexOpcode : byte
{
    Connect = 0x80,     // CONNECT is always "final"
    Disconnect = 0x81,
    Put = 0x02,
    PutFinal = 0x82,
    Get = 0x03,
    GetFinal = 0x83,
    SetPath = 0x85,
    Abort = 0xFF,
}

/// <summary>OBEX response codes (high bit set). 0x90 = Continue, 0xA0 = Success.</summary>
public enum ObexResponseCode : byte
{
    Continue = 0x90,
    Success = 0xA0,
    Created = 0xA1,
    BadRequest = 0xC0,
    Unauthorized = 0xC1,
    Forbidden = 0xC3,
    NotFound = 0xC4,
    NotImplemented = 0xD1,
    ServiceUnavailable = 0xD3,
}

/// <summary>
/// Header identifiers. The top two bits of the id encode how the value is
/// stored: 00 = UTF-16BE text, 01 = byte sequence, 10 = one byte, 11 = four bytes.
/// </summary>
public enum ObexHeaderId : byte
{
    Count = 0xC0,
    Name = 0x01,                 // UTF-16BE, null-terminated
    Type = 0x42,                 // byte sequence (ASCII MIME-like string)
    Length = 0xC3,
    TimeIso = 0x44,
    Description = 0x05,
    Target = 0x46,               // byte sequence (service UUID)
    Http = 0x47,
    Body = 0x48,
    EndOfBody = 0x49,
    Who = 0x4A,
    ConnectionId = 0xCB,
    AppParameters = 0x4C,        // byte sequence of TLV triplets
    AuthChallenge = 0x4D,
    AuthResponse = 0x4E,
    SingleResponseMode = 0x97,   // one byte
}

public static class ObexHeaderIdExtensions
{
    public enum Encoding { UnicodeText, ByteSequence, OneByte, FourByte }

    public static Encoding GetEncoding(this ObexHeaderId id) =>
        (byte)id >> 6 switch
        {
            0b00 => Encoding.UnicodeText,
            0b01 => Encoding.ByteSequence,
            0b10 => Encoding.OneByte,
            _ => Encoding.FourByte,
        };
}
