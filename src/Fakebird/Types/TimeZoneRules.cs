using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Fakebird.Types;

/// <summary>
/// The rules of a region zone, from the TZif data bundled in TimeZoneData.bin (tools/gentzdata.py): the main IANA
/// data that Firebird's ICU carries, whatever the host's tz database (Debian/Ubuntu's includes backzone).
/// </summary>
internal sealed class TimeZoneRules
{
    static readonly Dictionary<string, TimeZoneRules> All = Load();

    readonly long[] _times;  // transitions, UTC seconds since 1970
    readonly int[] _offsets; // offset (seconds east of UTC) from each transition on
    readonly int _initial;   // offset before the first transition
    readonly PosixRule? _footer;

    TimeZoneRules(long[] times, int[] offsets, int initial, PosixRule? footer)
    {
        _times = times;
        _offsets = offsets;
        _initial = initial;
        _footer = footer;
    }

    public static TimeZoneRules? Find(string name) => All.GetValueOrDefault(name);

    static Dictionary<string, TimeZoneRules> Load()
    {
        using var stream = typeof(TimeZoneRules).Assembly.GetManifestResourceStream("Fakebird.Types.TimeZoneData.bin")!;
        using var r = new BinaryReader(new DeflateStream(stream, CompressionMode.Decompress));
        var names = new (string Name, int Rule)[r.ReadUInt16()];
        for (var i = 0; i < names.Length; i++)
            names[i] = (Encoding.UTF8.GetString(r.ReadBytes(r.ReadByte())), r.ReadUInt16());
        var rules = new TimeZoneRules[r.ReadUInt16()];
        for (var i = 0; i < rules.Length; i++) rules[i] = Parse(r.ReadBytes(r.ReadInt32()));
        var d = new Dictionary<string, TimeZoneRules>(names.Length, StringComparer.Ordinal);
        foreach (var (name, rule) in names) d[name] = rules[rule];
        return d;
    }

    /// <summary>The version 2+ part of a TZif file (RFC 8536): header, 64-bit data block and footer.</summary>
    static TimeZoneRules Parse(byte[] b)
    {
        int Count(int i) => BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(20 + 4 * i));
        int isutCnt = Count(0), isstdCnt = Count(1), leapCnt = Count(2), timeCnt = Count(3), typeCnt = Count(4), charCnt = Count(5);
        var p = 44;
        var times = new long[timeCnt];
        for (var i = 0; i < timeCnt; i++, p += 8) times[i] = BinaryPrimitives.ReadInt64BigEndian(b.AsSpan(p));
        var typeIdx = b.AsSpan(p, timeCnt);
        p += timeCnt;
        var typeOffsets = new int[typeCnt];
        for (var i = 0; i < typeCnt; i++, p += 6) typeOffsets[i] = BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(p));
        p += charCnt + leapCnt * 12 + isstdCnt + isutCnt;
        var offsets = new int[timeCnt];
        for (var i = 0; i < timeCnt; i++) offsets[i] = typeOffsets[typeIdx[i]];
        var footer = Encoding.ASCII.GetString(b, p, b.Length - p).Trim('\n');
        return new TimeZoneRules(times, offsets, typeOffsets[0], footer.Length == 0 ? null : PosixRule.Parse(footer));
    }

    /// <summary>The offset, in seconds east of UTC, at a UTC instant (seconds since 1970).</summary>
    public int OffsetAt(long t)
    {
        if (_times.Length == 0 || t < _times[0]) return _times.Length == 0 && _footer != null ? _footer.OffsetAt(t) : _initial;
        if (t >= _times[^1] && _footer != null) return _footer.OffsetAt(t);
        var i = Array.BinarySearch(_times, t);
        return _offsets[i >= 0 ? i : ~i - 1];
    }

    /// <summary>A TZif footer: a POSIX TZ string with the RFC 8536 extensions (hours up to ±167 in rule times).</summary>
    sealed class PosixRule
    {
        int _std, _dst; // seconds east of UTC
        bool _hasDst;
        (char Kind, int A, int B, int C) _start, _end;
        int _startTime = 7200, _endTime = 7200;

        public static PosixRule Parse(string s)
        {
            var p = 0;
            var r = new PosixRule();
            SkipName(s, ref p);
            r._std = -ParseTime(s, ref p);
            if (p == s.Length) return r;
            SkipName(s, ref p);
            r._hasDst = true;
            r._dst = p < s.Length && s[p] != ',' ? -ParseTime(s, ref p) : r._std + 3600;
            Expect(s, ref p, ',');
            r._start = ParseDate(s, ref p);
            if (p < s.Length && s[p] == '/') { p++; r._startTime = ParseTime(s, ref p); }
            Expect(s, ref p, ',');
            r._end = ParseDate(s, ref p);
            if (p < s.Length && s[p] == '/') { p++; r._endTime = ParseTime(s, ref p); }
            return r;
        }

        static void SkipName(string s, ref int p)
        {
            if (s[p] == '<') p = s.IndexOf('>', p) + 1;
            else while (p < s.Length && char.IsAsciiLetter(s[p])) p++;
        }

        static void Expect(string s, ref int p, char c)
        {
            if (p >= s.Length || s[p] != c) throw new FormatException($"bad TZ rule: {s}");
            p++;
        }

        static int ParseNumber(string s, ref int p)
        {
            var n = 0;
            while (p < s.Length && char.IsAsciiDigit(s[p])) n = n * 10 + s[p++] - '0';
            return n;
        }

        /// <summary>[+-]hh[:mm[:ss]] in seconds (positive west of UTC for offsets).</summary>
        static int ParseTime(string s, ref int p)
        {
            var sign = 1;
            if (p < s.Length && s[p] is '+' or '-') sign = s[p++] == '-' ? -1 : 1;
            var t = ParseNumber(s, ref p) * 3600;
            if (p < s.Length && s[p] == ':') { p++; t += ParseNumber(s, ref p) * 60; }
            if (p < s.Length && s[p] == ':') { p++; t += ParseNumber(s, ref p); }
            return sign * t;
        }

        static (char, int, int, int) ParseDate(string s, ref int p)
        {
            if (s[p] == 'J') { p++; return ('J', ParseNumber(s, ref p), 0, 0); }
            if (s[p] != 'M') return ('n', ParseNumber(s, ref p), 0, 0);
            p++;
            var m = ParseNumber(s, ref p);
            Expect(s, ref p, '.');
            var w = ParseNumber(s, ref p);
            Expect(s, ref p, '.');
            return ('M', m, w, ParseNumber(s, ref p));
        }

        const int UnixEpochDay = 719162; // DateOnly(1970, 1, 1).DayNumber

        /// <summary>The day (DateOnly.DayNumber) a rule falls on in a year.</summary>
        static int Day((char Kind, int A, int B, int C) rule, int year)
        {
            var jan1 = new DateOnly(year, 1, 1).DayNumber;
            switch (rule.Kind)
            {
                case 'J': return jan1 + rule.A - 1 + (DateTime.IsLeapYear(year) && rule.A >= 60 ? 1 : 0);
                case 'n': return jan1 + rule.A;
            }
            var first = new DateOnly(year, rule.A, 1);
            var day = 1 + (rule.C - (int)first.DayOfWeek + 7) % 7 + (rule.B - 1) * 7;
            var days = DateTime.DaysInMonth(year, rule.A);
            while (day > days) day -= 7;
            return first.DayNumber + day - 1;
        }

        public int OffsetAt(long t)
        {
            if (!_hasDst) return _std;
            var localDay = Math.Clamp(Math.Floor((t + _std) / 86400.0) + UnixEpochDay, DateOnly.MinValue.DayNumber, DateOnly.MaxValue.DayNumber);
            var year = DateOnly.FromDayNumber((int)localDay).Year;
            var start = (Day(_start, year) - (long)UnixEpochDay) * 86400 + _startTime - _std;
            var end = (Day(_end, year) - (long)UnixEpochDay) * 86400 + _endTime - _dst;
            var dst = start < end ? t >= start && t < end : !(t >= end && t < start);
            return dst ? _dst : _std;
        }
    }
}
