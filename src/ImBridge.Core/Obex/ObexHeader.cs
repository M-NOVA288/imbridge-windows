using System.Buffers.Binary;
using System.Text;

namespace ImBridge.Core.Obex;

/// <summary>
/// A single OBEX header: an id plus its raw value bytes. Helper factories and
/// accessors convert to and from the id's natural encoding.
/// </summary>
public sealed class ObexHeader
{
    public ObexHeaderId Id { get; }
    public byte[] Value { get; }

    public ObexHeader(ObexHeaderId id, byte[] value)
    {
        Id = id;
        Value = value;
    }

    // ---- factories ----------------------------------------------------------

    public static ObexHeader Text(ObexHeaderId id, string text)
    {
        // UTF-16 big-endian, null terminated (two zero bytes), per OBEX spec.
        var chars = Encoding.BigEndianUnicode.GetBytes(text);
        var buf = new byte[chars.Length + 2];
        Array.Copy(chars, buf, chars.Length);
        return new ObexHeader(id, buf);
    }

    public static ObexHeader Bytes(ObexHeaderId id, byte[] value) => new(id, value);

    public static ObexHeader AsciiType(string mimeType)
    {
        // Type header is an ASCII string, null terminated.
        var ascii = Encoding.ASCII.GetBytes(mimeType);
        var buf = new byte[ascii.Length + 1];
        Array.Copy(ascii, buf, ascii.Length);
        return new ObexHeader(ObexHeaderId.Type, buf);
    }

    public static ObexHeader FourByte(ObexHeaderId id, uint value)
    {
        var buf = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buf, value);
        return new ObexHeader(id, buf);
    }

    public static ObexHeader OneByte(ObexHeaderId id, byte value) => new(id, new[] { value });

    // ---- accessors ----------------------------------------------------------

    public string AsText()
    {
        var bytes = Value;
        // Drop a trailing UTF-16 null if present.
        if (bytes.Length >= 2 && bytes[^1] == 0 && bytes[^2] == 0)
            bytes = bytes[..^2];
        return Encoding.BigEndianUnicode.GetString(bytes);
    }

    public string AsAscii()
    {
        var bytes = Value;
        if (bytes.Length >= 1 && bytes[^1] == 0)
            bytes = bytes[..^1];
        return Encoding.ASCII.GetString(bytes);
    }

    public uint AsFourByte() => BinaryPrimitives.ReadUInt32BigEndian(Value);

    // ---- wire format --------------------------------------------------------

    /// <summary>Total serialized size of this header in bytes.</summary>
    public int WireLength => Id.GetEncoding() switch
    {
        ObexHeaderIdExtensions.Encoding.OneByte => 2,
        ObexHeaderIdExtensions.Encoding.FourByte => 5,
        _ => 3 + Value.Length, // 1 id + 2 length prefix + payload
    };

    public void WriteTo(Span<byte> dest)
    {
        dest[0] = (byte)Id;
        switch (Id.GetEncoding())
        {
            case ObexHeaderIdExtensions.Encoding.OneByte:
                dest[1] = Value[0];
                break;
            case ObexHeaderIdExtensions.Encoding.FourByte:
                Value.AsSpan(0, 4).CopyTo(dest[1..]);
                break;
            default:
                BinaryPrimitives.WriteUInt16BigEndian(dest[1..], (ushort)(3 + Value.Length));
                Value.CopyTo(dest[3..]);
                break;
        }
    }

    /// <summary>Parse one header at <paramref name="offset"/>; advances it past the header.</summary>
    public static ObexHeader Parse(ReadOnlySpan<byte> data, ref int offset)
    {
        var id = (ObexHeaderId)data[offset];
        switch (id.GetEncoding())
        {
            case ObexHeaderIdExtensions.Encoding.OneByte:
                var one = new[] { data[offset + 1] };
                offset += 2;
                return new ObexHeader(id, one);
            case ObexHeaderIdExtensions.Encoding.FourByte:
                var four = data.Slice(offset + 1, 4).ToArray();
                offset += 5;
                return new ObexHeader(id, four);
            default:
                int len = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset + 1, 2));
                var payload = data.Slice(offset + 3, len - 3).ToArray();
                offset += len;
                return new ObexHeader(id, payload);
        }
    }
}
