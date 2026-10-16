using System.Buffers.Binary;
using System.Text;

namespace Fakebird.Wire;

/// <summary>
/// Buffered big-endian XDR reader/writer over a stream. Responses are buffered and only flushed when
/// the server is about to block on a read, so pipelined (lazy-send) requests cost a single write.
/// </summary>
internal sealed class Xdr
{
    readonly Stream _stream;
    readonly byte[] _in = new byte[64 * 1024];
    int _inPos, _inLen;
    byte[] _out = new byte[64 * 1024];
    int _outLen;

    public Xdr(Stream stream) => _stream = stream;

    // ---------------- reading ----------------

    void Fill(int needed)
    {
        if (_inLen - _inPos >= needed) return;
        if (_inPos > 0)
        {
            Buffer.BlockCopy(_in, _inPos, _in, 0, _inLen - _inPos);
            _inLen -= _inPos;
            _inPos = 0;
        }
        while (_inLen < needed)
        {
            // About to block waiting on the client: push out everything we owe it first.
            Flush();
            var n = _stream.Read(_in, _inLen, _in.Length - _inLen);
            if (n <= 0) throw new EndOfStreamException();
            _inLen += n;
        }
    }

    public int ReadInt32()
    {
        Fill(4);
        var v = BinaryPrimitives.ReadInt32BigEndian(_in.AsSpan(_inPos));
        _inPos += 4;
        return v;
    }

    public long ReadInt64()
    {
        Fill(8);
        var v = BinaryPrimitives.ReadInt64BigEndian(_in.AsSpan(_inPos));
        _inPos += 8;
        return v;
    }

    public int ReadOperation()
    {
        int op;
        do op = ReadInt32(); while (op == Op.Dummy);
        return op;
    }

    public void ReadRaw(Span<byte> dest)
    {
        var off = 0;
        while (off < dest.Length)
        {
            if (_inPos == _inLen) Fill(1);
            var n = Math.Min(dest.Length - off, _inLen - _inPos);
            _in.AsSpan(_inPos, n).CopyTo(dest[off..]);
            _inPos += n;
            off += n;
        }
    }

    void SkipPad(int length)
    {
        var pad = (4 - length) & 3;
        if (pad == 0) return;
        Fill(pad);
        _inPos += pad;
    }

    public byte[] ReadOpaque(int length)
    {
        var buf = length == 0 ? Array.Empty<byte>() : new byte[length];
        ReadRaw(buf);
        SkipPad(length);
        return buf;
    }

    public byte[] ReadBuffer() => ReadOpaque(ReadInt32());

    public string ReadString(Encoding enc)
    {
        var len = ReadInt32();
        if (len <= _in.Length)
        {
            Fill(len);
            var s = enc.GetString(_in, _inPos, len);
            _inPos += len;
            SkipPad(len);
            return s;
        }
        return enc.GetString(ReadOpaque(len));
    }

    // ---------------- writing ----------------

    void Ensure(int n)
    {
        if (_outLen + n <= _out.Length) return;
        Flush();
        if (n > _out.Length) Array.Resize(ref _out, Math.Max(n, _out.Length * 2));
    }

    public void Flush()
    {
        if (_outLen == 0) return;
        _stream.Write(_out, 0, _outLen);
        _outLen = 0;
    }

    public void WriteInt32(int v)
    {
        Ensure(4);
        BinaryPrimitives.WriteInt32BigEndian(_out.AsSpan(_outLen), v);
        _outLen += 4;
    }

    public void WriteInt64(long v)
    {
        Ensure(8);
        BinaryPrimitives.WriteInt64BigEndian(_out.AsSpan(_outLen), v);
        _outLen += 8;
    }

    public void WriteDouble(double v) => WriteInt64(BitConverter.DoubleToInt64Bits(v));
    public void WriteFloat(float v) => WriteInt32(BitConverter.SingleToInt32Bits(v));

    public void WritePad(int length)
    {
        var pad = (4 - length) & 3;
        if (pad == 0) return;
        Ensure(pad);
        _out.AsSpan(_outLen, pad).Clear();
        _outLen += pad;
    }

    public void WriteRaw(ReadOnlySpan<byte> data)
    {
        Ensure(data.Length);
        data.CopyTo(_out.AsSpan(_outLen));
        _outLen += data.Length;
    }

    /// <summary>Opaque of fixed length, padded with <paramref name="fill"/> up to <paramref name="length"/>.</summary>
    public void WriteOpaqueFixed(ReadOnlySpan<byte> data, int length, byte fill)
    {
        Ensure(length + 3);
        var n = Math.Min(data.Length, length);
        data[..n].CopyTo(_out.AsSpan(_outLen));
        _out.AsSpan(_outLen + n, length - n).Fill(fill);
        _outLen += length;
        WritePad(length);
    }

    public void WriteBuffer(ReadOnlySpan<byte> data)
    {
        WriteInt32(data.Length);
        WriteRaw(data);
        WritePad(data.Length);
    }

    /// <summary>Writes a string as an XDR buffer (length + bytes + pad) without an intermediate array.</summary>
    public void WriteString(string s, Encoding enc)
    {
        var max = enc.GetMaxByteCount(s.Length);
        Ensure(4 + max + 3);
        var n = enc.GetBytes(s, 0, s.Length, _out, _outLen + 4);
        BinaryPrimitives.WriteInt32BigEndian(_out.AsSpan(_outLen), n);
        _outLen += 4 + n;
        WritePad(n);
    }

    /// <summary>Writes a CHAR(n) value: encoded bytes padded with spaces to the declared byte length.</summary>
    public void WriteFixedString(string s, int byteLength, Encoding enc)
    {
        var max = enc.GetMaxByteCount(s.Length);
        Ensure(Math.Max(max, byteLength) + 3);
        var n = enc.GetBytes(s, 0, s.Length, _out, _outLen);
        if (n > byteLength) n = byteLength;
        _out.AsSpan(_outLen + n, byteLength - n).Fill((byte)' ');
        _outLen += byteLength;
        WritePad(byteLength);
    }
}
