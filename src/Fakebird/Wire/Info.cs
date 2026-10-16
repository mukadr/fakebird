using System.Buffers.Binary;
using System.Text;

namespace Fakebird.Wire;

/// <summary>Builder for Firebird info response buffers (little-endian clumplets).</summary>
internal sealed class InfoBuffer
{
    readonly List<byte> _b = new(256);
    public int Length => _b.Count;

    public InfoBuffer Byte(byte b)
    {
        _b.Add(b);
        return this;
    }

    public InfoBuffer Int(byte item, int value)
    {
        _b.Add(item);
        _b.Add(4);
        _b.Add(0);
        Span<byte> tmp = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(tmp, value);
        foreach (var x in tmp) _b.Add(x);
        return this;
    }

    public InfoBuffer Bytes(byte item, ReadOnlySpan<byte> data)
    {
        _b.Add(item);
        _b.Add((byte)(data.Length & 0xFF));
        _b.Add((byte)(data.Length >> 8));
        foreach (var x in data) _b.Add(x);
        return this;
    }

    public InfoBuffer Str(byte item, string s) => Bytes(item, Encoding.UTF8.GetBytes(s));

    public InfoBuffer Raw(ReadOnlySpan<byte> data)
    {
        foreach (var x in data) _b.Add(x);
        return this;
    }

    public byte[] ToArray() => _b.ToArray();
}
