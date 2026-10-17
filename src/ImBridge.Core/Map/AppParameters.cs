using System.Buffers.Binary;

namespace ImBridge.Core.Map;

/// <summary>
/// Builds and parses the MAP Application Parameters header body: a flat list of
/// [tag:1][length:1][value:length] triplets.
/// </summary>
public static class AppParameters
{
    public static byte[] Build(params (MapConstants.AppParamTag Tag, byte[] Value)[] items)
    {
        int total = 0;
        foreach (var (_, v) in items) total += 2 + v.Length;
        var buf = new byte[total];
        int o = 0;
        foreach (var (tag, v) in items)
        {
            buf[o++] = (byte)tag;
            buf[o++] = (byte)v.Length;
            v.CopyTo(buf.AsSpan(o));
            o += v.Length;
        }
        return buf;
    }

    public static byte[] U16(ushort value)
    {
        var b = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(b, value);
        return b;
    }

    public static Dictionary<MapConstants.AppParamTag, byte[]> Parse(ReadOnlySpan<byte> body)
    {
        var map = new Dictionary<MapConstants.AppParamTag, byte[]>();
        int o = 0;
        while (o + 2 <= body.Length)
        {
            var tag = (MapConstants.AppParamTag)body[o];
            int len = body[o + 1];
            o += 2;
            if (o + len > body.Length) break;
            map[tag] = body.Slice(o, len).ToArray();
            o += len;
        }
        return map;
    }
}
