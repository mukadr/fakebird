namespace Fakebird.Types;

/// <summary>
/// Single-byte character sets whose repertoire is smaller than Unicode (ASCII, ISO8859_1, WIN1252): text moved into
/// them is transliterated like Firebird does, failing for characters they cannot hold.
/// </summary>
public static class Charsets
{
    /// <summary>WIN1252 0x80-0x9F; the five unassigned bytes map to the C1 controls, like Windows' MultiByteToWideChar.</summary>
    const string Win1252High = "€\u0081‚ƒ„…†‡ˆ‰Š‹Œ\u008DŽ\u008F\u0090‘’“”•–—˜™š›œ\u009DžŸ";

    static readonly Dictionary<char, byte> Win1252Bytes = Win1252High.Select((ch, i) => (ch, b: (byte)(0x80 + i)))
        .ToDictionary(x => x.ch, x => x.b);

    /// <summary>The character set's name in RDB$CHARACTER_SETS.</summary>
    public static string Name(Charset cs) => cs switch
    {
        Charset.None => "NONE",
        Charset.Octets => "OCTETS",
        Charset.Ascii => "ASCII",
        Charset.UnicodeFss => "UNICODE_FSS",
        Charset.Win1252 => "WIN1252",
        Charset.Win1250 => "WIN1250",
        Charset.Win1251 => "WIN1251",
        Charset.Iso8859_1 => "ISO8859_1",
        _ => "UTF8",
    };

    /// <summary>True for the character sets that cannot hold every character (text moved into them is checked).</summary>
    public static bool IsNarrow(Charset cs) => cs is Charset.Ascii or Charset.Iso8859_1 or Charset.Win1252;

    /// <summary>The byte of a character in a narrow character set, or -1 when the character set has none.</summary>
    public static int ByteOf(char ch, Charset cs)
    {
        if (ch < 0x80) return ch;
        switch (cs)
        {
            case Charset.Ascii: return -1;
            case Charset.Iso8859_1: return ch <= 0xFF ? ch : -1;
            default:
                if (ch is >= ' ' and <= 'ÿ') return ch;
                return Win1252Bytes.TryGetValue(ch, out var b) ? b : -1;
        }
    }

    public static bool Fits(string s, Charset cs)
    {
        if (!IsNarrow(cs)) return true;
        foreach (var ch in s)
            if (ch >= 0x80 && ByteOf(ch, cs) < 0) return false;
        return true;
    }

    /// <summary>A character the target cannot hold: Firebird's isc_transliteration_failed, as an arithmetic exception.</summary>
    public static FbError TransliterationFailed()
        => new StatusBuilder().Gds(Err.isc_arith_except).Gds(FbMessages.transliteration_failed).Build();

    /// <summary>Text moved into a narrow character set must fit it.</summary>
    public static string Check(string s, Charset cs) => Fits(s, cs) ? s : throw TransliterationFailed();

    /// <summary>The bytes of text in a single-byte character set (NONE and the Unicode ones are UTF-8).</summary>
    public static byte[] Encode(string s, Charset cs)
    {
        if (!IsNarrow(cs)) return Conv.Utf8.GetBytes(s);
        var bytes = new byte[s.Length];
        for (var i = 0; i < s.Length; i++)
        {
            var b = ByteOf(s[i], cs);
            bytes[i] = b < 0 ? throw TransliterationFailed() : (byte)b;
        }
        return bytes;
    }

    /// <summary>Text from bytes of a character set (NONE and the Unicode ones are UTF-8).</summary>
    public static string Decode(ReadOnlySpan<byte> bytes, Charset cs)
    {
        if (!IsNarrow(cs)) return Conv.Utf8.GetString(bytes);
        var chars = new char[bytes.Length];
        for (var i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            if (b >= 0x80 && cs == Charset.Ascii) throw TransliterationFailed();
            chars[i] = b is >= 0x80 and <= 0x9F && cs == Charset.Win1252 ? Win1252High[b - 0x80] : (char)b;
        }
        return new string(chars);
    }

    /// <summary>WIN1252's default collation: the order of the bytes. Both strings must be in WIN1252.</summary>
    public static int CompareWin1252(ReadOnlySpan<char> x, ReadOnlySpan<char> y)
    {
        var n = Math.Min(x.Length, y.Length);
        for (var i = 0; i < n; i++)
        {
            if (x[i] == y[i]) continue;
            var a = ByteOf(x[i], Charset.Win1252);
            var b = ByteOf(y[i], Charset.Win1252);
            if (a < 0 || b < 0) throw TransliterationFailed();
            return a < b ? -1 : 1;
        }
        if (x.Length == y.Length) return 0;
        // the rest of the longer one must still be WIN1252
        if (!Fits((x.Length > n ? x : y)[n..].ToString(), Charset.Win1252)) throw TransliterationFailed();
        return x.Length < y.Length ? -1 : 1;
    }
}
