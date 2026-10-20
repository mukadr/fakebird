using System.Globalization;
using System.Text;

namespace Fakebird.Types;

/// <summary>
/// Data conversions ported from Firebird 5 (common/cvt.cpp): text parsing of numbers and date/time values,
/// number formatting, and the text of conversion errors. Names follow the Firebird routines.
/// </summary>
public static class Cvt
{
    public enum Expect { Date, Time, Timestamp, TimeTz, TimestampTz }

    // ------------------------------------------------------------------ conversion errors

    /// <summary>CVT_conversion_error: "conversion error from string" with the value rendered like Firebird.</summary>
    public static FbError ConversionError(in Value v) => Err.Conversion(ErrorText(v));

    /// <summary>The value as CVT_make_string renders it for a conversion error message.</summary>
    public static string ErrorText(in Value v)
    {
        var s = v.Tag switch
        {
            VTag.Bool => "BOOLEAN",
            VTag.Timestamp => LegacyTimestampText(v.L),
            VTag.TimestampTz => LegacyTimestampText(TimeZones.ToLocal(v.L, v.Zone)) + " " + TimeZones.Name(v.Zone),
            VTag.Bytes => Encoding.Latin1.GetString(v.AsBytes),
            VTag.String => Encoding.Latin1.GetString(Conv.Utf8.GetBytes(v.AsString)),
            _ => Conv.ToStr(v),
        };
        // bytes below ' ' (and, as signed chars, all non-ASCII bytes) are shown as #xNN
        StringBuilder? sb = null;
        for (var i = 0; i < s.Length; i++)
        {
            var ch = s[i];
            if (ch >= ' ' && ch < 0x80)
            {
                sb?.Append(ch);
                continue;
            }
            sb ??= new StringBuilder(s, 0, i, s.Length + 8);
            sb.Append("#x").Append(((int)ch).ToString("x2", CultureInfo.InvariantCulture));
        }
        return sb?.ToString() ?? s;
    }

    static readonly string[] LongMonths =
        ["JANUARY", "FEBRUARY", "MARCH", "APRIL", "MAY", "JUNE", "JULY", "AUGUST", "SEPTEMBER", "OCTOBER", "NOVEMBER", "DECEMBER"];

    /// <summary>Timestamps rendered in the pre-BLR-5 format (dd-MMM-yyyy h:mm:ss.tttt, no time at midnight).</summary>
    public static string LegacyTimestampText(long ts)
    {
        var (day, time) = Conv.SplitTimestamp(ts);
        var d = Conv.DayToDate(day);
        var sb = new StringBuilder();
        sb.Append(d.Day.ToString("00", CultureInfo.InvariantCulture)).Append('-')
            .Append(LongMonths[d.Month - 1][..3]).Append('-')
            .Append(d.Year.ToString("0000", CultureInfo.InvariantCulture));
        if (time != 0)
        {
            var h = time / (3600 * Value.TicksPerSecond);
            var m = time / (60 * Value.TicksPerSecond) % 60;
            var s = time / Value.TicksPerSecond % 60;
            var f = time % Value.TicksPerSecond;
            sb.Append(' ').Append(h.ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(m.ToString("00", CultureInfo.InvariantCulture)).Append(':')
                .Append(s.ToString("00", CultureInfo.InvariantCulture)).Append('.')
                .Append(f.ToString("0000", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------ numbers to text

    /// <summary>Longest text float_to_text produces ("-1.234567890123456E-300").</summary>
    public const int MaxFloatText = 23;

    /// <summary>
    /// float_to_text: printf("%- #*.*g") with 16 (8 for FLOAT) significant digits, reduced until it fits in
    /// <paramref name="width"/> bytes (the destination's length, at most 23).
    /// </summary>
    public static string FloatToText(double d, bool isFloat, int width = MaxFloatText)
    {
        width = Math.Min(width, MaxFloatText);
        var precision = isFloat ? 8 : 16;
        var text = FormatG(d, precision);
        if (text.Length > width)
        {
            precision -= text.Length - width;
            if (precision < 2) throw Err.NumericOverflow();
            text = FormatG(d, precision);
            if (text.Length > width)
            {
                precision -= text.Length - width;
                if (precision < 2) throw Err.NumericOverflow();
                text = FormatG(d, precision);
                if (text.Length > width) throw Err.NumericOverflow();
            }
        }
        // the first character is the sign: '-' or the ' ' of the format, which is dropped
        return d < 0 ? text : text[1..];
    }

    /// <summary>C's "% #.*g" (sign or space, alternate form keeping trailing zeros), without width padding.</summary>
    static string FormatG(double d, int precision)
    {
        if (double.IsNaN(d) || double.IsInfinity(d)) throw Err.NumericOverflow();
        if (precision == 0) precision = 1;
        var sign = d < 0 || (d == 0 && double.IsNegative(d)) ? "-" : " ";
        var abs = Math.Abs(d);
        int exp;
        if (abs == 0) exp = 0;
        else
        {
            // exponent after rounding to the requested significant digits
            var e = abs.ToString("E" + (precision - 1), CultureInfo.InvariantCulture);
            exp = int.Parse(e[(e.IndexOf('E') + 1)..], CultureInfo.InvariantCulture);
        }
        string body;
        if (exp < -4 || exp >= precision)
        {
            var e = abs.ToString("E" + (precision - 1), CultureInfo.InvariantCulture);
            var mant = e[..e.IndexOf('E')];
            if (!mant.Contains('.')) mant += ".";
            body = mant + "e" + (exp < 0 ? "-" : "+") + Math.Abs(exp).ToString("00", CultureInfo.InvariantCulture);
        }
        else
        {
            body = abs.ToString("F" + (precision - 1 - exp), CultureInfo.InvariantCulture);
            if (!body.Contains('.')) body += ".";
        }
        return sign + body;
    }

    // ------------------------------------------------------------------ text to numbers

    /// <summary>CVT_make_string buffers: longer strings fail with "string right truncation" before parsing.</summary>
    static void CheckBuffer(string s, int size)
    {
        var len = Conv.Utf8.GetByteCount(s);
        if (len > size) throw Err.StringTruncation(size, len);
    }

    /// <summary>
    /// String to exact numeric of the given storage and scale (cvt_decompose + adjustForScale): digits are
    /// accumulated in the target's own size, 0x hex literals are accepted, rounding is half away from zero.
    /// </summary>
    public static long StringToExact(string s, int scale, TypeKind kind)
    {
        CheckBuffer(s, kind == TypeKind.SmallInt ? 22 : 52);
        var (max, min) = kind switch
        {
            TypeKind.SmallInt => ((Int128)short.MaxValue, (Int128)short.MinValue),
            TypeKind.Integer => (int.MaxValue, int.MinValue),
            _ => ((Int128)long.MaxValue, (Int128)long.MinValue),
        };
        var bytes = kind switch { TypeKind.SmallInt => 2, TypeKind.Integer => 4, _ => 8 };
        Int128 value;
        var exp = Decompose(s, max, min, bytes, out value);
        // Firebird scales are negative: the value has exp decimal exponent, the target wants -scale
        var adjust = -scale - exp;
        if (adjust > 0)
        {
            var fraction = 0;
            do
            {
                if (adjust == 1) fraction = (int)(value % 10);
                value /= 10;
            } while (--adjust > 0);
            if (fraction > 4) value++;
            else if (fraction < -4) value--;
        }
        else if (adjust < 0)
        {
            var limit = max / 10;
            do
            {
                if (value > limit || value < -limit) throw Err.NumericOverflow();
                value *= 10;
            } while (++adjust < 0);
        }
        return (long)value;
    }

    /// <summary>cvt_decompose: returns the decimal exponent of the parsed mantissa.</summary>
    static int Decompose(string s, Int128 max, Int128 min, int bytes, out Int128 value)
    {
        value = 0;
        var p = 0;
        var end = s.Length;
        while (p < end && s[p] == ' ') p++;

        if (p + 2 < end && s[p] == '0' && char.ToUpperInvariant(s[p + 1]) == 'X')
        {
            p += 2;
            var q = p;
            while (q < end && s[q] != '\0' && s[q] != ' ') q++;
            var digitsEnd = q;
            while (q < end && s[q] == ' ') q++;
            if (q != end || end - p == 0 || (end - p) * 4 > bytes * 8) throw ConversionError(Value.Str(s));
            ulong acc = 0;
            for (var i = p; i < digitsEnd; i++)
            {
                var c = char.ToUpperInvariant(s[i]);
                int dgt = c is >= '0' and <= '9' ? c - '0' : c is >= 'A' and <= 'F' ? c - 'A' + 10 : -1;
                if (dgt < 0) throw ConversionError(Value.Str(s));
                acc = acc * 16 + (ulong)dgt;
            }
            // 0xFFFFFFFF = -1; 0x0FFFFFFFF = 4294967295
            var n = digitsEnd - p;
            value = n <= 8 ? (int)(uint)acc : n <= 16 ? (long)acc : (Int128)acc;
            if (bytes == 2) value = (short)(long)value;
            return 0;
        }

        var scale = 0;
        var sign = 0;
        bool digitSeen = false, fraction = false;
        var limitBy10 = max / 10;
        for (; p < end; p++)
        {
            var c = s[p];
            if (c is >= '0' and <= '9')
            {
                digitSeen = true;
                if (value > limitBy10 || value < -limitBy10)
                {
                    if (fraction)
                    {
                        while (p < end && s[p] == '0') p++;
                        if (p >= end) continue;
                    }
                    throw Err.NumericOverflow();
                }
                if (value == limitBy10 && ((c > '8' && sign == -1) || (c > '7' && sign != -1)))
                    throw Err.NumericOverflow();
                value = value * 10 + (c - '0');
                if (fraction) --scale;
            }
            else if (c == '.')
            {
                if (fraction) throw ConversionError(Value.Str(s));
                fraction = true;
            }
            else if (c == '-' && !digitSeen && sign == 0 && !fraction) sign = -1;
            else if (c == '+' && !digitSeen && sign == 0 && !fraction) sign = 1;
            else if (c is 'e' or 'E') break;
            else if (c == ' ')
            {
                while (p < end && s[p] == ' ') p++;
                if (p < end) throw ConversionError(Value.Str(s));
            }
            else throw ConversionError(Value.Str(s));
        }
        if (!digitSeen) throw ConversionError(Value.Str(s));
        if (sign == -1) value = -value;

        if (p < end)
        {
            sign = 0;
            var exp = 0;
            digitSeen = false;
            for (p++; p < end; p++)
            {
                var c = s[p];
                if (c is >= '0' and <= '9')
                {
                    digitSeen = true;
                    exp = exp * 10 + c - '0';
                    if (exp >= 10000) throw Err.NumericOverflow();
                }
                else if (c == '-' && !digitSeen && sign == 0) sign = -1;
                else if (c == '+' && !digitSeen && sign == 0) sign = 1;
                else if (c == ' ')
                {
                    while (p < end && s[p] == ' ') p++;
                    if (p < end) throw ConversionError(Value.Str(s));
                }
                else throw ConversionError(Value.Str(s));
            }
            if (sign == -1) scale -= exp;
            else scale += exp;
            if (!digitSeen) throw ConversionError(Value.Str(s));
        }
        return scale;
    }

    static readonly double[] UpperPart = [1e0, 1e32, 1e64, 1e96, 1e128, 1e160, 1e192, 1e224, 1e256, 1e288];
    static readonly double[] LowerPart =
    [
        1e00, 1e01, 1e02, 1e03, 1e04, 1e05, 1e06, 1e07, 1e08, 1e09, 1e10, 1e11, 1e12, 1e13, 1e14, 1e15,
        1e16, 1e17, 1e18, 1e19, 1e20, 1e21, 1e22, 1e23, 1e24, 1e25, 1e26, 1e27, 1e28, 1e29, 1e30, 1e31,
    ];

    /// <summary>CVT_power_of_ten (0 &lt;= scale &lt; 320).</summary>
    public static double PowerOfTen(int scale) => UpperPart[scale >> 5] * LowerPart[scale & 0x1f];

    /// <summary>CVT_get_double for text: digits accumulated in a double, then one multiplication or division.</summary>
    public static double StringToDouble(string s)
    {
        CheckBuffer(s, 130);
        double value = 0;
        var scale = 0;
        var sign = 0;
        bool digitSeen = false, pastSign = false, fraction = false;
        var p = 0;
        var end = s.Length;
        while (p < end && s[p] == ' ') p++;
        for (; p < end; p++)
        {
            var c = s[p];
            if (c is >= '0' and <= '9')
            {
                digitSeen = true;
                pastSign = true;
                if (fraction) scale++;
                value = value * 10.0 + (c - '0');
            }
            else if (c == '.')
            {
                pastSign = true;
                if (fraction) throw ConversionError(Value.Str(s));
                fraction = true;
            }
            else if (!pastSign && c == '-') { sign = -1; pastSign = true; }
            else if (!pastSign && c == '+') { sign = 1; pastSign = true; }
            else if (c is 'e' or 'E') break;
            else if (c == ' ')
            {
                while (p < end && s[p] == ' ') p++;
                if (p < end) throw ConversionError(Value.Str(s));
            }
            else throw ConversionError(Value.Str(s));
        }
        if (!digitSeen) throw ConversionError(Value.Str(s));
        if (sign == -1) value = -value;
        if (p < end)
        {
            digitSeen = false;
            sign = 0;
            var exp = 0;
            for (p++; p < end; p++)
            {
                var c = s[p];
                if (c is >= '0' and <= '9')
                {
                    digitSeen = true;
                    exp = exp * 10 + c - '0';
                    if (exp >= 10000) throw Err.NumericOverflow();
                }
                else if (c == '-' && !digitSeen && sign == 0) sign = -1;
                else if (c == '+' && !digitSeen && sign == 0) sign = 1;
                else if (c == ' ')
                {
                    while (p < end && s[p] == ' ') p++;
                    if (p < end) throw ConversionError(Value.Str(s));
                }
                else throw ConversionError(Value.Str(s));
            }
            if (!digitSeen) throw ConversionError(Value.Str(s));
            if (sign == -1) scale += exp;
            else scale -= exp;
        }
        if (Math.Abs(scale) > 308) throw Err.NumericOverflow();
        if (scale > 0) value /= PowerOfTen(scale);
        else if (scale < 0) value *= PowerOfTen(-scale);
        if (double.IsInfinity(value)) throw Err.NumericOverflow();
        return value;
    }

    /// <summary>CVT_get_long / CVT_get_int64 for approximate numbers: round half away (with an epsilon) and range check.</summary>
    public static long DoubleToExact(double d, int scale, TypeKind kind, bool isFloat)
    {
        var eps = isFloat ? 1e-5 : 1e-14;
        // Firebird scales are negative: multiply by 10^scale here (a negative scale divides)
        if (scale > 0) d *= PowerOfTen(scale);
        else if (scale < 0) d /= PowerOfTen(-scale);
        if (d > 0) d += 0.5 + eps;
        else d -= 0.5 + eps;
        switch (kind)
        {
            case TypeKind.BigInt:
                if (double.IsNaN(d) || d < -9.2233720368547758E18 || d > 9.2233720368547758E18) throw Err.NumericOverflow();
                return (long)d;
            default:
            {
                if (d < int.MinValue)
                {
                    if (d > int.MinValue - 1.0) return CheckShort(int.MinValue, kind);
                    throw Err.NumericOverflow();
                }
                if (d > int.MaxValue)
                {
                    if (d < int.MaxValue + 1.0) return CheckShort(int.MaxValue, kind);
                    throw Err.NumericOverflow();
                }
                return CheckShort((int)d, kind);
            }
        }
    }

    static long CheckShort(long v, TypeKind kind)
    {
        if (kind == TypeKind.SmallInt && v is < short.MinValue or > short.MaxValue) throw Err.NumericOverflow();
        return v;
    }

    // ------------------------------------------------------------------ INT128

    static readonly Int128 Int128LimitBy10 = Int128.MaxValue / 10;

    /// <summary>Int128::setScale from scale <paramref name="from"/> to <paramref name="to"/> (positive scales):
    /// dropped digits round half away from zero, adding digits checks the 128-bit range.</summary>
    public static Int128 Int128SetScale(Int128 v, int from, int to)
    {
        var diff = from - to;
        if (diff > 0)
        {
            Int128 rem = 0;
            while (diff-- > 0)
            {
                if (diff == 0) rem = v % 10;
                v /= 10;
            }
            if (rem > 4) v++;
            else if (rem < -4) v--;
        }
        else
        {
            while (diff++ < 0)
            {
                if (v > Int128LimitBy10 || v < -Int128LimitBy10) throw Err.NumericOverflow();
                v *= 10;
            }
        }
        return v;
    }

    /// <summary>Text to INT128 at the given scale (CVT_decompose with a 128-bit accumulator).</summary>
    public static Int128 StringToInt128(string s, int scale)
    {
        CheckBuffer(s, 130);
        var exp = Decompose(s, Int128.MaxValue, Int128.MinValue, 16, out var value);
        return Int128SetScale(value, -exp, scale);
    }

    /// <summary>Double to INT128 (Int128 from a double, rounded half away).</summary>
    public static Int128 DoubleToInt128(double d, int scale)
    {
        if (scale > 0) d *= PowerOfTen(scale);
        d = Math.Round(d, MidpointRounding.AwayFromZero);
        if (double.IsNaN(d) || d >= 1.7014118346046923e38 || d <= -1.7014118346046923e38) throw Err.NumericOverflow();
        return (Int128)d;
    }

    // ------------------------------------------------------------------ text to date/time

    /// <summary>
    /// CVT_string_to_datetime: returns the day (DATE), time of day (TIME) or timestamp, in Fakebird ticks.
    /// <paramref name="allowSpecial"/> enables NOW/TODAY/TOMORROW/YESTERDAY.
    /// </summary>
    public static long StringToDateTime(string s, Expect expect, bool allowSpecial = true)
        => StringToDateTime(s, expect, out _, allowSpecial);

    /// <summary>
    /// CVT_string_to_datetime. Values without time zone are in the session zone (a zone given in the text converts
    /// them); for the WITH TIME ZONE types the result is the UTC time or timestamp of <paramref name="zone"/>.
    /// </summary>
    public static long StringToDateTime(string s, Expect expect, out ushort zone, bool allowSpecial = true)
        => StringToDateTime(s, expect, out zone, out _, allowSpecial);

    /// <summary>CVT_string_to_datetime, also telling whether the text named a time zone.</summary>
    public static long StringToDateTime(string s, Expect expect, out ushort zone, out bool hasZone, bool allowSpecial = true)
    {
        var session = TimeZones.Session;
        zone = session;
        hasZone = false;
        var isTime = expect is Expect.Time or Expect.TimeTz;
        CheckBuffer(s, 130);
        const int englishMonth = -1, special = -2;
        int positionYear, positionMonth = 1, positionDay;
        var haveEnglishMonth = false;
        var dateSep = '\0';
        var components = new int[7];
        var description = new int[7];
        var p = 0;
        var end = s.Length;
        var start = isTime ? 3 : 0;
        int i;
        for (i = start; i < 7; i++)
        {
            while (p < end && s[p] is ' ' or '\t') p++;
            if (p == end) break;
            var c = Upper7(s[p]);
            int n;
            if (c is >= '0' and <= '9')
            {
                var precision = 0;
                n = 0;
                while (p < end && s[p] is >= '0' and <= '9')
                {
                    n = (ushort)(n * 10 + s[p++] - '0');
                    precision++;
                }
                description[i] = precision;
            }
            else if (IsLetter7(c) && !haveEnglishMonth && i - start < 2)
            {
                var temp = new StringBuilder();
                while (p < end && temp.Length < 10)
                {
                    c = Upper7(s[p]);
                    if (!IsLetter7(c)) break;
                    temp.Append(c);
                    p++;
                }
                if (temp.Length < 3) throw ConversionError(Value.Str(s));
                var word = temp.ToString();
                var month = -1;
                if (i < 2)
                    for (var m = 0; m < 12; m++)
                        if (LongMonths[m].StartsWith(word, StringComparison.Ordinal)) { month = m; break; }
                if (month < 0)
                {
                    if (!allowSpecial || i != start) throw ConversionError(Value.Str(s));
                    description[i] = special;
                    while (++p < end)
                        if (s[p] is not (' ' or '\t' or '\0')) throw ConversionError(Value.Str(s));
                    // the current UTC timestamp in the session zone ("not really UTC" for the types without zone)
                    var now = Conv.Now().L;
                    if (expect is not Expect.TimestampTz) now = TimeZones.ToLocal(now, session);
                    var (today, timeNow) = Conv.SplitTimestamp(now);
                    if (expect == Expect.TimeTz) timeNow = TimeZones.TimeToUtc(timeNow, session);
                    if (word == "NOW")
                        return expect switch { Expect.Date => today, Expect.Time or Expect.TimeTz => timeNow, _ => now };
                    if (isTime) throw ConversionError(Value.Str(s));
                    var dayN = word switch
                    {
                        "TODAY" => today,
                        "TOMORROW" => today + 1,
                        "YESTERDAY" => today - 1,
                        _ => throw ConversionError(Value.Str(s)),
                    };
                    return expect == Expect.Date ? dayN : dayN * Value.TicksPerDay;
                }
                n = month + 1;
                positionMonth = i;
                description[i] = englishMonth;
                haveEnglishMonth = true;
            }
            else
            {
                if (expect == Expect.Date || i != 3) throw ConversionError(Value.Str(s));
                // may be a time zone after a date when expecting a timestamp
                --i;
                break;
            }

            components[i] = n;

            var hadSpace = false;
            while (p < end && s[p] is ' ' or '\t')
            {
                p++;
                hadSpace = true;
            }
            if (p == end) break;

            if (i <= 1)
            {
                if (dateSep == '\0' || (dateSep != ' ' && s[p] == dateSep) || (dateSep == ' ' && hadSpace))
                {
                    if (dateSep != ' ' && s[p] is '/' or '-' or '.')
                    {
                        dateSep = s[p++];
                        continue;
                    }
                    if (hadSpace)
                    {
                        dateSep = ' ';
                        continue;
                    }
                }
            }
            else if (i == 2) continue;
            else if (i is >= 3 and <= 5)
            {
                if (s[p] == ':')
                {
                    p++;
                    continue;
                }
                if (s[p] == '.')
                {
                    p++;
                    i = 6 - 1;
                    continue;
                }
                i = 7;
                break;
            }
            else if (i == 6) break;

            throw ConversionError(Value.Str(s));
        }

        if (i - start < 1) throw ConversionError(Value.Str(s));
        if (expect == Expect.Date && i > 2) throw ConversionError(Value.Str(s));

        if (expect != Expect.Date)
        {
            while (p < end && s[p] is ' ' or '\t') p++;
            if (p < end)
            {
                zone = TimeZones.Parse(s[p..]);
                hasZone = true;
            }
        }
        else
        {
            for (; p < end; p++)
                if (s[p] is not (' ' or '\t' or '\0')) throw ConversionError(Value.Str(s));
        }

        int year, mon, mday;
        if (!isTime)
        {
            if (description[0] >= 3) { positionYear = 0; positionMonth = 1; positionDay = 2; }
            else if (description[0] == englishMonth) { positionYear = 2; positionMonth = 0; positionDay = 1; }
            else if (description[1] == englishMonth) { positionYear = 2; positionMonth = 1; positionDay = 0; }
            else if (dateSep == '.') { positionYear = 2; positionMonth = 1; positionDay = 0; }
            else { positionYear = 2; positionMonth = 0; positionDay = 1; }

            if (description[positionYear] > 4 || description[positionMonth] > 2 || description[positionMonth] == 0
                || description[positionDay] > 2 || description[positionDay] <= 0)
                throw ConversionError(Value.Str(s));

            year = components[positionYear];
            mon = components[positionMonth];
            mday = components[positionDay];
            var currentYear = DateTime.Now.Year;
            if (description[positionYear] == 0) year = currentYear;
            else if (description[positionYear] <= 2)
                year += year < (currentYear - 1900 - 50) % 100 ? 2000 : 1900;
        }
        else
        {
            year = 2020;
            mon = 1;
            mday = 1;
        }

        int hour = 0, min = 0, sec = 0;
        if (i > 2 &&
            ((hour = components[3]) > 23 || (min = components[4]) > 59 || (sec = components[5]) > 59 ||
             description[3] > 2 || description[3] == 0 || description[4] > 2 || description[4] == 0 ||
             description[5] > 2 || description[6] > 4))
            throw ConversionError(Value.Str(s));

        // Firebird encodes the tm fields (normalizing overflows) and rejects values that do not decode back
        if (year is < 1 or > 9999)
            throw new StatusBuilder().Gds(expect switch
            {
                Expect.Date => FbMessages.date_range_exceeded,
                Expect.Time or Expect.TimeTz => FbMessages.time_range_exceeded,
                _ => FbMessages.datetime_range_exceeded,
            }).Build();
        if (mon is < 1 or > 12 || mday < 1 || mday > DateTime.DaysInMonth(year, mon))
            throw ConversionError(Value.Str(s));

        var fractionDigits = description[6];
        long frac = components[6];
        while (fractionDigits++ < 4) frac *= 10;

        var dayNumber = Conv.DateToDay(new DateOnly(year, mon, mday));
        var timeOfDay = ((hour * 60L + min) * 60 + sec) * Value.TicksPerSecond + frac;
        var ticks = dayNumber * Value.TicksPerDay + timeOfDay;
        if (expect is Expect.TimeTz or Expect.TimestampTz || zone != session)
        {
            if (expect != Expect.Date) ticks = TimeZones.ToUtc(ticks, zone);
            if (zone != session)
            {
                if (expect == Expect.Time) ticks = TimeZones.TimeTzToTime(Conv.SplitTimestamp(ticks).time, zone);
                else if (expect == Expect.Timestamp) ticks = TimeZones.ToLocal(ticks, session);
            }
        }
        return expect switch
        {
            Expect.Date => dayNumber,
            Expect.Time or Expect.TimeTz => Conv.SplitTimestamp(ticks).time,
            _ => ticks,
        };
    }

    static char Upper7(char c) => c is >= 'a' and <= 'z' ? (char)(c - 32) : c;
    static bool IsLetter7(char c) => c is >= 'A' and <= 'Z';
}
