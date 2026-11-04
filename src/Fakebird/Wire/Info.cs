using System.Buffers.Binary;
using System.Text;

namespace Fakebird.Wire;

/// <summary>Builder for Firebird info response buffers (little-endian clumplets).</summary>
internal sealed class InfoBuffer
{
    byte[] _b = new byte[256];
    int _n;
    public int Length => _n;

    Span<byte> Take(int count)
    {
        if (_n + count > _b.Length) Array.Resize(ref _b, Math.Max(_b.Length * 2, _n + count));
        var span = _b.AsSpan(_n, count);
        _n += count;
        return span;
    }

    public InfoBuffer Byte(byte b)
    {
        Take(1)[0] = b;
        return this;
    }

    public InfoBuffer Int(byte item, int value)
    {
        var t = Take(7);
        t[0] = item;
        t[1] = 4;
        t[2] = 0;
        BinaryPrimitives.WriteInt32LittleEndian(t[3..], value);
        return this;
    }

    public InfoBuffer Bytes(byte item, ReadOnlySpan<byte> data)
    {
        var t = Take(3 + data.Length);
        t[0] = item;
        t[1] = (byte)(data.Length & 0xFF);
        t[2] = (byte)(data.Length >> 8);
        data.CopyTo(t[3..]);
        return this;
    }

    public InfoBuffer Str(byte item, string s)
    {
        var n = Encoding.UTF8.GetByteCount(s);
        var t = Take(3 + n);
        t[0] = item;
        t[1] = (byte)(n & 0xFF);
        t[2] = (byte)(n >> 8);
        Encoding.UTF8.GetBytes(s, t[3..]);
        return this;
    }

    public InfoBuffer Raw(ReadOnlySpan<byte> data)
    {
        data.CopyTo(Take(data.Length));
        return this;
    }

    /// <summary>Empties the buffer for the next response (a connection reuses one).</summary>
    public InfoBuffer Clear()
    {
        _n = 0;
        return this;
    }

    public ReadOnlySpan<byte> Span => _b.AsSpan(0, _n);

    public byte[] ToArray() => _b.AsSpan(0, _n).ToArray();
}
