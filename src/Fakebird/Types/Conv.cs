using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Fakebird.Types;

/// <summary>Conversions, casts and comparisons following Firebird dialect 3 semantics.</summary>
public static class Conv
{
    static readonly int MjdEpochDayNumber = new DateOnly(1858, 11, 17).DayNumber;
    static readonly CompareInfo Invariant = CultureInfo.InvariantCulture.CompareInfo;
    public static readonly UTF8Encoding Utf8 = new(false);

    // ------------------------------------------------------------------ dates

    public static long DateToDay(DateOnly d) => d.DayNumber - MjdEpochDayNumber;
    public static DateOnly DayToDate(long day) => DateOnly.FromDayNumber((int)(day + MjdEpochDayNumber));
    public static DateTime DateToDateTime(long day) => DayToDate(day).ToDateTime(TimeOnly.MinValue);

    public static long DateTimeToTimestamp(DateTime dt)
        => DateToDay(DateOnly.FromDateTime(dt)) * Value.TicksPerDay + dt.TimeOfDay.Ticks / 1000;

    public static DateTime TimestampToDateTime(long ts)
    {
        var (day, time) = SplitTimestamp(ts);
        return DateToDateTime(day).AddTicks(time * 1000);
    }

    public static (long day, long time) SplitTimestamp(long ts)
    {
        var day = Math.DivRem(ts, Value.TicksPerDay, out var time);
        if (time < 0) { time += Value.TicksPerDay; day--; }
        return (day, time);
    }

    public static Value Now()
    {
        var now = DateTime.Now;
        // CURRENT_TIMESTAMP has millisecond precision in Firebird 3
        var ticks = now.Ticks - now.Ticks % TimeSpan.TicksPerMillisecond;
        return Value.Timestamp(DateTimeToTimestamp(new DateTime(ticks)));
    }

    // ------------------------------------------------------------------ numbers

    public static decimal ToDecimal(in Value v) => v.Tag switch
    {
        VTag.Int => ScaledToDecimal(v.L, v.Scale),
        VTag.Double => (decimal)v.AsDouble,
        _ => ToDecimal(CastToExactAuto(v)),
    };

    public static decimal ScaledToDecimal(long l, int scale)
    {
        if (scale == 0) return l;
        var neg = l < 0;
        var abs = neg ? (ulong)(-(Int128)l) : (ulong)l;
        return new decimal((int)(uint)abs, (int)(uint)(abs >> 32), 0, neg, (byte)scale);
    }

    public static double ToDouble(in Value v) => v.Tag switch
    {
        VTag.Int => v.Scale == 0 ? v.L : v.L / Value.Pow10Double[v.Scale],
        VTag.Double => v.AsDouble,
        VTag.String => Cvt.StringToDouble(v.AsString),
        _ => throw Cvt.ConversionError(v),
    };

    public static long ToInt64(in Value v)
    {
        var r = CastExact(v, 0, TypeKind.BigInt);
        return r.L;
    }

    public static int ToInt32(in Value v)
    {
        var r = CastExact(v, 0, TypeKind.Integer);
        return (int)r.L;
    }

    static double ParseDouble(string s) => Cvt.StringToDouble(s);

    /// <summary>Parses a numeric string into an exact value (keeping its scale) or a double for exponent notation.</summary>
    public static Value ParseNumber(string s)
    {
        var span = s.AsSpan().Trim();
        if (span.Length == 0) throw Err.Conversion(s);
        var neg = false;
        var i = 0;
        if (span[0] is '-' or '+') { neg = span[0] == '-'; i++; }
        Int128 acc = 0;
        var scale = -1;
        var digits = 0;
        for (; i < span.Length; i++)
        {
            var c = span[i];
            if (c is >= '0' and <= '9')
            {
                acc = acc * 10 + (c - '0');
                digits++;
                if (scale >= 0) scale++;
                if (acc > long.MaxValue) return Value.Dbl(ParseDouble(s));
            }
            else if (c == '.' && scale < 0) scale = 0;
            else if (c is 'e' or 'E') return Value.Dbl(ParseDouble(s));
            else throw Err.Conversion(s);
        }
        if (digits == 0) throw Err.Conversion(s);
        if (scale < 0) scale = 0;
        if (scale > 18) return Value.Dbl(ParseDouble(s));
        var l = (long)acc;
        return Value.Int(neg ? -l : l, scale);
    }

    static Value CastToExactAuto(in Value v) => v.Tag switch
    {
        VTag.String => ParseNumber(v.AsString),
        _ => throw Cvt.ConversionError(v),
    };

    /// <summary>Rescales an exact value; rounding half away from zero when reducing scale.</summary>
    public static long Rescale(long value, int from, int to)
    {
        if (from == to) return value;
        if (to > from)
        {
            var f = Value.Pow10[to - from];
            try { return checked(value * f); }
            catch (OverflowException) { throw Err.NumericOverflow(); }
        }
        var d = Value.Pow10[from - to];
        var q = Math.DivRem(value, d, out var r);
        if (Math.Abs(r) * 2 >= d) q += value < 0 ? -1 : 1;
        return q;
    }

    static void CheckRange(long v, TypeKind kind)
    {
        switch (kind)
        {
            case TypeKind.SmallInt when v is < short.MinValue or > short.MaxValue:
            case TypeKind.Integer when v is < int.MinValue or > int.MaxValue:
                throw Err.NumericOverflow();
        }
    }

    public static Value CastExact(in Value v, int scale, TypeKind kind)
    {
        long r;
        switch (v.Tag)
        {
            case VTag.Int:
                r = Rescale(v.L, v.Scale, scale);
                break;
            case VTag.Double:
                r = Cvt.DoubleToExact(v.AsDouble, scale, kind, v.IsFloat);
                break;
            case VTag.String:
                r = Cvt.StringToExact(v.AsString, scale, kind);
                break;
            default:
                throw Cvt.ConversionError(v);
        }
        CheckRange(r, kind);
        return Value.Int(r, scale);
    }

    // ------------------------------------------------------------------ formatting

    public static string ToStr(in Value v) => v.Tag switch
    {
        VTag.Null => "",
        VTag.String => v.AsString,
        VTag.Int => FormatExact(v.L, v.Scale),
        VTag.Double => Cvt.FloatToText(v.AsDouble, v.IsFloat),
        VTag.Bool => v.AsBool ? "TRUE" : "FALSE",
        VTag.Date => FormatDate(v.L),
        VTag.Time => FormatTime(v.L),
        VTag.Timestamp => FormatTimestamp(v.L),
        VTag.Bytes => Utf8.GetString(v.AsBytes),
        _ => "",
    };

    public static string FormatExact(long l, int scale)
    {
        if (scale == 0) return l.ToString(CultureInfo.InvariantCulture);
        var neg = l < 0;
        var abs = neg ? -(Int128)l : l;
        var s = abs.ToString(CultureInfo.InvariantCulture).PadLeft(scale + 1, '0');
        var res = s[..^scale] + "." + s[^scale..];
        return neg ? "-" + res : res;
    }

    public static string FormatDouble(double d) => Cvt.FloatToText(d, isFloat: false);

    public static string FormatDate(long day)
    {
        var d = DayToDate(day);
        return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public static string FormatTime(long t)
    {
        var h = t / (3600 * Value.TicksPerSecond);
        var m = t / (60 * Value.TicksPerSecond) % 60;
        var s = t / Value.TicksPerSecond % 60;
        var f = t % Value.TicksPerSecond;
        return $"{h:00}:{m:00}:{s:00}.{f:0000}";
    }

    public static string FormatTimestamp(long ts)
    {
        var (day, time) = SplitTimestamp(ts);
        return FormatDate(day) + " " + FormatTime(time);
    }

    // ------------------------------------------------------------------ casting

    /// <summary>Converts a value to the given type (CAST / assignment semantics).</summary>
    public static Value Cast(in Value v, SqlType t)
    {
        if (v.IsNull) return v;
        switch (t.Kind)
        {
            case TypeKind.SmallInt:
            case TypeKind.Integer:
            case TypeKind.BigInt:
                if (v.Tag == VTag.Int && v.Scale == t.Scale)
                {
                    CheckRange(v.L, t.Kind);
                    return v;
                }
                if (v.Tag is VTag.Date or VTag.Time or VTag.Timestamp) throw Cvt.ConversionError(v);
                return CastExact(v, t.Scale, t.Kind);
            case TypeKind.Float:
            {
                if (v.Tag is VTag.Date or VTag.Time or VTag.Timestamp) throw Cvt.ConversionError(v);
                if (v.IsFloat) return v;
                var d = ToDouble(v);
                if (Math.Abs(d) > float.MaxValue) throw Err.NumericOverflow();
                return Value.Flt((float)d);
            }
            case TypeKind.Double:
                if (v.Tag == VTag.Double) return v.IsFloat ? Value.Dbl(v.AsDouble) : v;
                if (v.Tag is VTag.Date or VTag.Time or VTag.Timestamp) throw Cvt.ConversionError(v);
                return Value.Dbl(ToDouble(v));
            case TypeKind.Boolean:
                if (v.Tag == VTag.Bool) return v;
                if (v.Tag == VTag.String)
                {
                    // CVT_get_boolean: TRUE/FALSE ignoring case and surrounding white space
                    var s = v.AsString.Trim(' ', '\t', '\n', '\r', '\v', '\f');
                    if (s.Equals("TRUE", StringComparison.OrdinalIgnoreCase)) return Value.True;
                    if (s.Equals("FALSE", StringComparison.OrdinalIgnoreCase)) return Value.False;
                }
                throw Cvt.ConversionError(v);
            case TypeKind.Date:
                switch (v.Tag)
                {
                    case VTag.Date: return v;
                    case VTag.Timestamp: return Value.Date(SplitTimestamp(v.L).day);
                    case VTag.String: return Value.Date(Cvt.StringToDateTime(v.AsString, Cvt.Expect.Date));
                }
                throw Cvt.ConversionError(v);
            case TypeKind.Time:
                switch (v.Tag)
                {
                    case VTag.Time: return v;
                    case VTag.Timestamp: return Value.Time(SplitTimestamp(v.L).time);
                    case VTag.String: return Value.Time(Cvt.StringToDateTime(v.AsString, Cvt.Expect.Time));
                }
                throw Cvt.ConversionError(v);
            case TypeKind.Timestamp:
                switch (v.Tag)
                {
                    case VTag.Timestamp: return v;
                    case VTag.Date: return Value.Timestamp(v.L * Value.TicksPerDay);
                    case VTag.Time: return Value.Timestamp(DateToDay(DateOnly.FromDateTime(DateTime.Now)) * Value.TicksPerDay + v.L);
                    case VTag.String: return Value.Timestamp(Cvt.StringToDateTime(v.AsString, Cvt.Expect.Timestamp));
                }
                throw Cvt.ConversionError(v);
            case TypeKind.Char:
            case TypeKind.VarChar:
                return CastString(v, t);
            case TypeKind.Blob:
                if (t.SubType == 1)
                    return v.Tag == VTag.String ? v : Value.Str(ToStr(v));
                return v.Tag == VTag.Bytes ? v : Value.Bytes(Utf8.GetBytes(ToStr(v)));
            case TypeKind.Null:
                return v;
        }
        throw Err.Unsupported("cast to " + t);
    }

    /// <summary>
    /// Text of a value moved into a string of <paramref name="t"/>: doubles get as many digits as fit in its byte
    /// length (float_to_text) and exact numbers that do not fit fail with a conversion error (integer_to_text).
    /// </summary>
    static string NumberText(in Value v, SqlType t)
    {
        var bytes = t.Length * (t.Charset == Charset.Octets ? 1 : t.BytesPerChar);
        if (v.Tag == VTag.Double) return Cvt.FloatToText(v.AsDouble, v.IsFloat, bytes);
        var s = FormatExact(v.L, v.Scale);
        if (s.Length > bytes) throw Cvt.ConversionError(v);
        return s;
    }

    static Value CastString(in Value v, SqlType t)
    {
        if (t.Charset == Charset.Octets)
        {
            var bytes = v.Tag == VTag.Bytes ? v.AsBytes : Utf8.GetBytes(v.Tag is VTag.Int or VTag.Double ? NumberText(v, t) : ToStr(v));
            if (bytes.Length > t.Length)
            {
                // excess must be all zero/space padding
                for (var i = t.Length; i < bytes.Length; i++)
                    if (bytes[i] != 0 && bytes[i] != (byte)' ') throw Err.StringTruncation(t.Length, bytes.Length);
                bytes = bytes[..t.Length];
            }
            if (t.Kind == TypeKind.Char && bytes.Length < t.Length)
            {
                var nb = new byte[t.Length];
                bytes.CopyTo(nb, 0);
                bytes = nb;
            }
            return Value.Bytes(bytes);
        }
        var s = v.Tag == VTag.String ? v.AsString : v.Tag is VTag.Int or VTag.Double ? NumberText(v, t) : ToStr(v);
        var len = CharLength(s);
        if (len > t.Length)
        {
            // Firebird allows truncating trailing blanks silently.
            var trimmed = s.TrimEnd(' ');
            var tlen = CharLength(trimmed);
            if (tlen > t.Length) throw Err.StringTruncation(t.Length, len);
            s = trimmed;
            len = tlen;
        }
        if (t.Kind == TypeKind.Char && len < t.Length) s = s + new string(' ', t.Length - len);
        else if (!ReferenceEquals(s, v.O)) return Value.Str(s);
        else return v;
        return Value.Str(s);
    }

    /// <summary>Length in characters (Unicode code points, like Firebird UTF8).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CharLength(string s)
    {
        var n = s.Length;
        for (var i = 0; i < s.Length; i++)
            if (char.IsHighSurrogate(s[i])) n--;
        return n;
    }

    // ------------------------------------------------------------------ comparison

    /// <summary>Compares two non-null values. Mixed types are converted like Firebird does.</summary>
    public static int Compare(in Value a, in Value b, Collation collation = Collation.Binary)
    {
        if (a.Tag == b.Tag)
        {
            switch (a.Tag)
            {
                case VTag.Int:
                    if (a.Scale == b.Scale) return a.L.CompareTo(b.L);
                    return CompareScaled(a.L, a.Scale, b.L, b.Scale);
                case VTag.String: return CompareStrings(a.AsString, b.AsString, collation);
                case VTag.Double: return a.AsDouble.CompareTo(b.AsDouble);
                case VTag.Date:
                case VTag.Time:
                case VTag.Timestamp:
                case VTag.Bool:
                    return a.L.CompareTo(b.L);
                case VTag.Bytes: return CompareBytes(a.AsBytes, b.AsBytes);
            }
        }
        return CompareMixed(a, b, collation);
    }

    static int CompareScaled(long a, int sa, long b, int sb)
    {
        Int128 x = a, y = b;
        if (sa < sb) x *= Value.Pow10[sb - sa];
        else y *= Value.Pow10[sa - sb];
        return x.CompareTo(y);
    }

    static int CompareMixed(in Value a, in Value b, Collation collation)
    {
        switch (a.Tag)
        {
            case VTag.Int when b.Tag == VTag.Double:
            case VTag.Double when b.Tag == VTag.Int:
                return ToDouble(a).CompareTo(ToDouble(b));
            case VTag.Int or VTag.Double when b.Tag == VTag.String:
                return Compare(a, ParseNumber(b.AsString), collation);
            case VTag.String when b.Tag is VTag.Int or VTag.Double:
                return Compare(ParseNumber(a.AsString), b, collation);
            case VTag.Date when b.Tag == VTag.Timestamp:
                return (a.L * Value.TicksPerDay).CompareTo(b.L);
            case VTag.Timestamp when b.Tag == VTag.Date:
                return a.L.CompareTo(b.L * Value.TicksPerDay);
            case VTag.Date or VTag.Time or VTag.Timestamp when b.Tag == VTag.String:
                return a.L.CompareTo(Cast(b, TypeOfTag(a.Tag)).L);
            case VTag.String when b.Tag is VTag.Date or VTag.Time or VTag.Timestamp:
                return Cast(a, TypeOfTag(b.Tag)).L.CompareTo(b.L);
            case VTag.Bool when b.Tag == VTag.String:
                return a.L.CompareTo(Cast(b, SqlType.Boolean).L);
            case VTag.String when b.Tag == VTag.Bool:
                return Cast(a, SqlType.Boolean).L.CompareTo(b.L);
            case VTag.Bytes when b.Tag == VTag.String:
                return CompareBytes(a.AsBytes, Utf8.GetBytes(b.AsString));
            case VTag.String when b.Tag == VTag.Bytes:
                return CompareBytes(Utf8.GetBytes(a.AsString), b.AsBytes);
        }
        throw Cvt.ConversionError(b);
    }

    static SqlType TypeOfTag(VTag t) => t switch
    {
        VTag.Date => SqlType.Date,
        VTag.Time => SqlType.Time,
        _ => SqlType.Timestamp,
    };

    public static int CompareStrings(string a, string b, Collation collation)
    {
        var x = a.AsSpan().TrimEnd(' ');
        var y = b.AsSpan().TrimEnd(' ');
        return collation switch
        {
            Collation.Binary => CompareOrdinalCodePoints(x, y),
            Collation.CaseInsensitive => x.CompareTo(y, StringComparison.OrdinalIgnoreCase),
            _ => Invariant.Compare(x, y, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace),
        };
    }

    static int CompareOrdinalCodePoints(ReadOnlySpan<char> x, ReadOnlySpan<char> y)
    {
        // Ordinal UTF-16 comparison differs from UTF-8 byte order only for surrogates; good enough.
        var c = x.SequenceCompareTo(y);
        return c < 0 ? -1 : c > 0 ? 1 : 0;
    }

    public static int CompareBytes(byte[] a, byte[] b)
    {
        var c = a.AsSpan().SequenceCompareTo(b);
        return c < 0 ? -1 : c > 0 ? 1 : 0;
    }

    /// <summary>Hash consistent with <see cref="Compare"/> equality.</summary>
    public static int Hash(in Value v, Collation collation = Collation.Binary)
    {
        switch (v.Tag)
        {
            case VTag.Null: return 0;
            case VTag.Int:
            {
                long l = v.L;
                int s = v.Scale;
                while (s > 0 && l % 10 == 0) { l /= 10; s--; }
                return s == 0 ? l.GetHashCode() : HashCode.Combine(l, s);
            }
            case VTag.Double:
            {
                var d = v.AsDouble;
                if (d == Math.Floor(d) && Math.Abs(d) < 9e18) return ((long)d).GetHashCode();
                return d.GetHashCode();
            }
            case VTag.String:
            {
                var span = v.AsString.AsSpan().TrimEnd(' ');
                return collation switch
                {
                    Collation.Binary => string.GetHashCode(span),
                    Collation.CaseInsensitive => string.GetHashCode(span, StringComparison.OrdinalIgnoreCase),
                    _ => Invariant.GetHashCode(span, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace),
                };
            }
            case VTag.Date: return (v.L * Value.TicksPerDay).GetHashCode();
            case VTag.Bytes:
            {
                var h = new HashCode();
                h.AddBytes(v.AsBytes);
                return h.ToHashCode();
            }
            default: return v.L.GetHashCode();
        }
    }

    /// <summary>Equality for grouping/distinct/index keys (NULLs equal each other).</summary>
    public static bool KeyEquals(in Value a, in Value b, Collation collation = Collation.Binary)
    {
        if (a.IsNull || b.IsNull) return a.IsNull && b.IsNull;
        return Compare(a, b, collation) == 0;
    }
}
