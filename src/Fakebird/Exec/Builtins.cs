using System.Globalization;
using System.Text;
using Fakebird.Types;

namespace Fakebird.Exec;

public sealed class BuiltinDef
{
    public string Name = null!;
    public int MinArgs;
    public int MaxArgs;
    /// <summary>Expected type for parameter markers at position i (given the already bound args).</summary>
    public Func<int, XNode?[], SqlType?>? ParamHint;
    public Func<XNode[], SqlType> ResultType = null!;
    public Func<Ctx, XNode[], FuncNode, Value> Impl = null!;
    public bool Deterministic = true;
}

public static class Builtins
{
    static readonly Dictionary<string, BuiltinDef> Defs = new(StringComparer.Ordinal);
    static readonly SqlType LongVarchar = SqlType.VarChar(8191);

    public static BuiltinDef? Find(string name) => Defs.TryGetValue(name, out var d) ? d : null;

    static void Def(string name, int min, int max, Func<XNode[], SqlType> type, Func<Ctx, XNode[], FuncNode, Value> impl,
        Func<int, XNode?[], SqlType?>? hint = null, bool deterministic = true)
        => Defs[name] = new BuiltinDef
        {
            Name = name, MinArgs = min, MaxArgs = max, ResultType = type, Impl = impl, ParamHint = hint,
            Deterministic = deterministic,
        };

    // ---------------------------------------------------------------- helpers

    static string? Str(Ctx c, XNode n)
    {
        var v = n.Eval(c);
        return v.IsNull ? null : v.Tag == VTag.String ? v.AsString : Conv.ToStr(v);
    }

    static long? Long(Ctx c, XNode n)
    {
        var v = n.Eval(c);
        return v.IsNull ? null : Conv.ToInt64(v);
    }

    static double? Dbl(Ctx c, XNode n)
    {
        var v = n.Eval(c);
        return v.IsNull ? null : Conv.ToDouble(v);
    }

    static SqlType StrResult(XNode a, int? len = null)
    {
        var t = a.Type;
        if (t.Kind == TypeKind.Blob) return t;
        var l = len ?? (t.IsString ? t.Length : 8191);
        return SqlType.VarChar(Math.Max(1, l), t.IsString ? t.Charset : Charset.Utf8, t.IsString ? t.Collation : Collation.Binary);
    }

    static SqlType HintString(int i, XNode?[] a) => LongVarchar;
    static SqlType? HintInt(int i, XNode?[] a) => SqlType.Integer;

    static Value S(string s) => Value.Str(s);
    static Value D(double d)
    {
        if (double.IsNaN(d) || double.IsInfinity(d)) throw Err.Random("Floating-point overflow / invalid operation");
        return Value.Dbl(d);
    }

    static Value Unary(Ctx c, XNode[] a, Func<double, double> f)
    {
        var x = Dbl(c, a[0]);
        return x == null ? Value.Null : D(f(x.Value));
    }

    static SqlType DoubleT(XNode[] _) => SqlType.Double;

    // character position helpers (code points)
    static int[] CodePointIndex(string s)
    {
        var idx = new List<int>(s.Length + 1);
        for (var i = 0; i < s.Length; i++)
        {
            idx.Add(i);
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length) i++;
        }
        idx.Add(s.Length);
        return idx.ToArray();
    }

    static bool HasSurrogates(string s)
    {
        foreach (var ch in s) if (char.IsSurrogate(ch)) return true;
        return false;
    }

    /// <summary>Substring in code points (1-based start).</summary>
    public static string Substr(string s, long start, long? length)
    {
        if (!HasSurrogates(s))
        {
            long from = start - 1;
            long to = length == null ? s.Length : from + length.Value;
            if (from < 0) from = 0;
            if (to > s.Length) to = s.Length;
            if (to <= from) return "";
            return s.Substring((int)from, (int)(to - from));
        }
        var idx = CodePointIndex(s);
        var n = idx.Length - 1;
        long f = start - 1, t = length == null ? n : f + length.Value;
        if (f < 0) f = 0;
        if (t > n) t = n;
        if (t <= f) return "";
        return s[idx[f]..idx[t]];
    }

    static string PadOrCut(string s, int len, string pad, bool left)
    {
        var cur = Conv.CharLength(s);
        if (cur >= len) return Substr(s, 1, len);
        if (pad.Length == 0) return s;
        var sb = new StringBuilder();
        var need = len - cur;
        while (Conv.CharLength(sb.ToString()) < need) sb.Append(pad);
        var p = Substr(sb.ToString(), 1, need);
        return left ? p + s : s + p;
    }

    // ---------------------------------------------------------------- date parts

    public static Value Extract(string part, in Value v)
    {
        if (v.IsNull) return v;
        long day = 0, time = 0;
        bool hasDate = true, hasTime = true;
        switch (v.Tag)
        {
            case VTag.Date: day = v.L; hasTime = false; break;
            case VTag.Time: time = v.L; hasDate = false; break;
            case VTag.Timestamp: (day, time) = Conv.SplitTimestamp(v.L); break;
            case VTag.String:
                return Extract(part, Conv.Cast(v, SqlType.Timestamp));
            default: throw Err.Conversion(Conv.ToStr(v));
        }
        DateOnly d = hasDate ? Conv.DayToDate(day) : default;
        switch (part)
        {
            case "YEAR": Need(hasDate); return Value.Int(d.Year);
            case "MONTH": Need(hasDate); return Value.Int(d.Month);
            case "DAY": Need(hasDate); return Value.Int(d.Day);
            case "WEEKDAY": Need(hasDate); return Value.Int((int)d.DayOfWeek);
            case "YEARDAY": Need(hasDate); return Value.Int(d.DayOfYear - 1);
            case "WEEK": Need(hasDate); return Value.Int(ISOWeek.GetWeekOfYear(d.ToDateTime(TimeOnly.MinValue)));
            case "HOUR": Need(hasTime); return Value.Int(time / (3600 * Value.TicksPerSecond));
            case "MINUTE": Need(hasTime); return Value.Int(time / (60 * Value.TicksPerSecond) % 60);
            case "SECOND": Need(hasTime); return Value.Int(time % (60 * Value.TicksPerSecond), 4);
            case "MILLISECOND": Need(hasTime); return Value.Int(time % Value.TicksPerSecond, 1);
        }
        throw Err.Unsupported("EXTRACT(" + part + ")");

        static void Need(bool ok)
        {
            if (!ok) throw Err.Random("Specified EXTRACT part does not exist in input datatype");
        }
    }

    public static SqlType ExtractType(string part) => part switch
    {
        "SECOND" => new SqlType(TypeKind.Integer, scale: 4, precision: 9, subType: 1),
        "MILLISECOND" => new SqlType(TypeKind.Integer, scale: 1, precision: 9, subType: 1),
        _ => SqlType.SmallInt,
    };

    static Value DateAdd(string part, long n, in Value v)
    {
        switch (v.Tag)
        {
            case VTag.Date:
            {
                var d = Conv.DayToDate(v.L);
                d = part switch
                {
                    "YEAR" => d.AddYears((int)n),
                    "MONTH" => d.AddMonths((int)n),
                    "WEEK" => d.AddDays((int)n * 7),
                    "DAY" => d.AddDays((int)n),
                    "WEEKDAY" or "YEARDAY" => d.AddDays((int)n),
                    _ => throw Err.Random("Specified DATEADD part does not exist in input datatype"),
                };
                return Value.Date(Conv.DateToDay(d));
            }
            case VTag.Time:
            {
                long ticks = part switch
                {
                    "HOUR" => n * 3600 * Value.TicksPerSecond,
                    "MINUTE" => n * 60 * Value.TicksPerSecond,
                    "SECOND" => n * Value.TicksPerSecond,
                    "MILLISECOND" => n * 10,
                    _ => throw Err.Random("Specified DATEADD part does not exist in input datatype"),
                };
                var t = (v.L + ticks) % Value.TicksPerDay;
                if (t < 0) t += Value.TicksPerDay;
                return Value.Time(t);
            }
            case VTag.Timestamp:
            {
                switch (part)
                {
                    case "YEAR":
                    case "MONTH":
                    {
                        var (day, time) = Conv.SplitTimestamp(v.L);
                        var d = Conv.DayToDate(day);
                        d = part == "YEAR" ? d.AddYears((int)n) : d.AddMonths((int)n);
                        return Value.Timestamp(Conv.DateToDay(d) * Value.TicksPerDay + time);
                    }
                    case "WEEK": return Value.Timestamp(v.L + n * 7 * Value.TicksPerDay);
                    case "DAY":
                    case "WEEKDAY":
                    case "YEARDAY": return Value.Timestamp(v.L + n * Value.TicksPerDay);
                    case "HOUR": return Value.Timestamp(v.L + n * 3600 * Value.TicksPerSecond);
                    case "MINUTE": return Value.Timestamp(v.L + n * 60 * Value.TicksPerSecond);
                    case "SECOND": return Value.Timestamp(v.L + n * Value.TicksPerSecond);
                    case "MILLISECOND": return Value.Timestamp(v.L + n * 10);
                }
                break;
            }
            case VTag.String:
                return DateAdd(part, n, Conv.Cast(v, SqlType.Timestamp));
        }
        throw Err.Conversion(Conv.ToStr(v));
    }

    static Value DateDiff(string part, Value a, Value b)
    {
        if (a.Tag == VTag.String) a = Conv.Cast(a, b.Tag == VTag.Date ? SqlType.Date : b.Tag == VTag.Time ? SqlType.Time : SqlType.Timestamp);
        if (b.Tag == VTag.String) b = Conv.Cast(b, a.Tag == VTag.Date ? SqlType.Date : a.Tag == VTag.Time ? SqlType.Time : SqlType.Timestamp);
        long ta, tb;
        if (a.Tag == VTag.Time || b.Tag == VTag.Time)
        {
            if (a.Tag != VTag.Time || b.Tag != VTag.Time) throw Err.Conversion(Conv.ToStr(a));
            ta = a.L;
            tb = b.L;
        }
        else
        {
            ta = a.Tag == VTag.Date ? a.L * Value.TicksPerDay : a.L;
            tb = b.Tag == VTag.Date ? b.L * Value.TicksPerDay : b.L;
        }
        switch (part)
        {
            case "YEAR":
            {
                var da = Conv.DayToDate(Conv.SplitTimestamp(ta).day);
                var db = Conv.DayToDate(Conv.SplitTimestamp(tb).day);
                return Value.Int(db.Year - da.Year);
            }
            case "MONTH":
            {
                var da = Conv.DayToDate(Conv.SplitTimestamp(ta).day);
                var db = Conv.DayToDate(Conv.SplitTimestamp(tb).day);
                return Value.Int((db.Year - da.Year) * 12L + db.Month - da.Month);
            }
            case "WEEK": return Value.Int((Conv.SplitTimestamp(tb).day - Conv.SplitTimestamp(ta).day) / 7);
            case "DAY":
            case "WEEKDAY":
            case "YEARDAY":
                return Value.Int(Conv.SplitTimestamp(tb).day - Conv.SplitTimestamp(ta).day);
            case "HOUR": return Value.Int(tb / (3600 * Value.TicksPerSecond) - ta / (3600 * Value.TicksPerSecond));
            case "MINUTE": return Value.Int(tb / (60 * Value.TicksPerSecond) - ta / (60 * Value.TicksPerSecond));
            case "SECOND": return Value.Int(tb / Value.TicksPerSecond - ta / Value.TicksPerSecond);
            case "MILLISECOND": return Value.Int((tb - ta) / 10 * 10, 1);
        }
        throw Err.Unsupported("DATEDIFF part " + part);
    }

    // ---------------------------------------------------------------- uuid

    static byte[] UuidBytes(Guid g)
    {
        // Firebird stores UUIDs in RFC 4122 (big-endian) byte order
        var b = g.ToByteArray(bigEndian: true);
        return b;
    }

    static string UuidToChar(byte[] b)
    {
        if (b.Length != 16) throw Err.Random("Human readable UUID argument for UUID_TO_CHAR must be of exact length 16");
        var hex = Convert.ToHexString(b);
        return $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}";
    }

    static byte[] CharToUuid(string s)
    {
        var h = s.Replace("-", "");
        if (h.Length != 32 || s.Length != 36) throw Err.Random("Human readable UUID argument for CHAR_TO_UUID must be of exact length 36");
        return Convert.FromHexString(h);
    }

    // ---------------------------------------------------------------- registry

    static Builtins()
    {
        // ---- strings
        Def("UPPER", 1, 1, a => a[0].Type.IsTextual ? a[0].Type : StrResult(a[0]), (c, a, f) =>
        {
            var v = a[0].Eval(c);
            if (v.IsNull) return v;
            return S(Conv.ToStr(v).ToUpperInvariant());
        }, HintString);
        Def("LOWER", 1, 1, a => a[0].Type.IsTextual ? a[0].Type : StrResult(a[0]), (c, a, f) =>
        {
            var v = a[0].Eval(c);
            if (v.IsNull) return v;
            return S(Conv.ToStr(v).ToLowerInvariant());
        }, HintString);
        Def("TRIM", 3, 3, a => StrResult(a[2]), (c, a, f) =>
        {
            var spec = ((ConstNode)a[0]).Value.AsString;
            var s = Str(c, a[2]);
            if (s == null) return Value.Null;
            var charsV = a[1].Eval(c);
            string chars;
            if (a[1] is ConstNode { Value.IsNull: true }) chars = " ";
            else if (charsV.IsNull) return Value.Null;
            else chars = Conv.ToStr(charsV);
            if (chars.Length == 0) return S(s);
            if (spec is "BOTH" or "LEADING")
                while (s.StartsWith(chars, StringComparison.Ordinal)) s = s[chars.Length..];
            if (spec is "BOTH" or "TRAILING")
                while (s.EndsWith(chars, StringComparison.Ordinal)) s = s[..^chars.Length];
            return S(s);
        }, HintString);
        Def("SUBSTRING", 2, 3, a => StrResult(a[0]), (c, a, f) =>
        {
            var v = a[0].Eval(c);
            if (v.IsNull) return v;
            var start = Long(c, a[1]);
            if (start == null) return Value.Null;
            long? len = null;
            if (a.Length > 2)
            {
                len = Long(c, a[2]);
                if (len == null) return Value.Null;
                if (len < 0) throw Err.Random("Invalid length parameter " + len + " to SUBSTRING. Negative integers are not allowed.");
            }
            if (v.Tag == VTag.Bytes)
            {
                var b = v.AsBytes;
                var from = (int)Math.Clamp(start.Value - 1, 0, b.Length);
                var to = (int)Math.Clamp(len == null ? b.Length : start.Value - 1 + len.Value, from, b.Length);
                return Value.Bytes(b[from..to]);
            }
            var s = Conv.ToStr(v);
            if (start.Value < 1 && len != null)
            {
                len = len.Value + start.Value - 1;
                start = 1;
                if (len < 0) len = 0;
            }
            return S(Substr(s, start.Value, len));
        }, (i, a) => i == 0 ? LongVarchar : SqlType.Integer);
        Def("LEFT", 2, 2, a => StrResult(a[0]), (c, a, f) =>
        {
            var s = Str(c, a[0]);
            var n = Long(c, a[1]);
            if (s == null || n == null) return Value.Null;
            if (n < 0) throw Err.Random("Invalid length parameter " + n + " to LEFT. Negative integers are not allowed.");
            return S(Substr(s, 1, n));
        }, (i, a) => i == 0 ? LongVarchar : SqlType.Integer);
        Def("RIGHT", 2, 2, a => StrResult(a[0]), (c, a, f) =>
        {
            var s = Str(c, a[0]);
            var n = Long(c, a[1]);
            if (s == null || n == null) return Value.Null;
            var len = Conv.CharLength(s);
            if (n >= len) return S(s);
            return S(Substr(s, len - n.Value + 1, n));
        }, (i, a) => i == 0 ? LongVarchar : SqlType.Integer);
        Def("CHAR_LENGTH", 1, 1, a => a[0].Type.Kind == TypeKind.Blob ? SqlType.BigInt : SqlType.Integer, (c, a, f) =>
        {
            var v = a[0].Eval(c);
            if (v.IsNull) return v;
            if (v.Tag == VTag.Bytes) return Value.Int(v.AsBytes.Length);
            return Value.Int(Conv.CharLength(Conv.ToStr(v)));
        }, HintString);
        Defs["CHARACTER_LENGTH"] = Defs["CHAR_LENGTH"];
        Def("OCTET_LENGTH", 1, 1, a => a[0].Type.Kind == TypeKind.Blob ? SqlType.BigInt : SqlType.Integer, (c, a, f) =>
        {
            var v = a[0].Eval(c);
            if (v.IsNull) return v;
            if (v.Tag == VTag.Bytes) return Value.Int(v.AsBytes.Length);
            return Value.Int(Conv.Utf8.GetByteCount(Conv.ToStr(v)));
        }, HintString);
        Def("BIT_LENGTH", 1, 1, a => a[0].Type.Kind == TypeKind.Blob ? SqlType.BigInt : SqlType.Integer, (c, a, f) =>
        {
            var v = a[0].Eval(c);
            if (v.IsNull) return v;
            if (v.Tag == VTag.Bytes) return Value.Int(v.AsBytes.Length * 8L);
            return Value.Int(Conv.Utf8.GetByteCount(Conv.ToStr(v)) * 8L);
        }, HintString);
        Def("POSITION", 2, 3, a => SqlType.Integer, (c, a, f) =>
        {
            var sub = Str(c, a[0]);
            var s = Str(c, a[1]);
            if (sub == null || s == null) return Value.Null;
            long start = 1;
            if (a.Length > 2)
            {
                var st = Long(c, a[2]);
                if (st == null) return Value.Null;
                start = st.Value;
            }
            if (sub.Length == 0) return Value.Int(start <= Conv.CharLength(s) + 1 ? start : 0);
            var from = Substr(s, start, null);
            var idx = from.IndexOf(sub, StringComparison.Ordinal);
            if (idx < 0) return Value.Int(0);
            return Value.Int(Conv.CharLength(from[..idx]) + start);
        }, (i, a) => i == 2 ? SqlType.Integer : LongVarchar);
        Def("REPLACE", 3, 3, a => a[0].Type.Kind == TypeKind.Blob ? a[0].Type : SqlType.VarChar(8191), (c, a, f) =>
        {
            var s = Str(c, a[0]);
            var find = Str(c, a[1]);
            var repl = Str(c, a[2]);
            if (s == null || find == null || repl == null) return Value.Null;
            if (find.Length == 0) return S(s);
            return S(s.Replace(find, repl, StringComparison.Ordinal));
        }, HintString);
        Def("REVERSE", 1, 1, a => StrResult(a[0]), (c, a, f) =>
        {
            var s = Str(c, a[0]);
            if (s == null) return Value.Null;
            var arr = s.ToCharArray();
            Array.Reverse(arr);
            return S(new string(arr));
        }, HintString);
        Def("LPAD", 2, 3, a => PadType(a), (c, a, f) => Pad(c, a, true), (i, a) => i == 1 ? SqlType.Integer : LongVarchar);
        Def("RPAD", 2, 3, a => PadType(a), (c, a, f) => Pad(c, a, false), (i, a) => i == 1 ? SqlType.Integer : LongVarchar);
        Def("OVERLAY", 3, 4, a => SqlType.VarChar(8191), (c, a, f) =>
        {
            var s = Str(c, a[0]);
            var p = Str(c, a[1]);
            var from = Long(c, a[2]);
            if (s == null || p == null || from == null) return Value.Null;
            long len = Conv.CharLength(p);
            if (a.Length > 3)
            {
                var l = Long(c, a[3]);
                if (l == null) return Value.Null;
                len = l.Value;
            }
            return S(Substr(s, 1, from.Value - 1) + p + Substr(s, from.Value + len, null));
        }, (i, a) => i >= 2 ? SqlType.Integer : LongVarchar);
        Def("ASCII_CHAR", 1, 1, a => SqlType.Char(1), (c, a, f) =>
        {
            var n = Long(c, a[0]);
            if (n == null) return Value.Null;
            if (n is < 0 or > 255) throw Err.NumericOverflow();
            return S(((char)n.Value).ToString());
        }, HintInt);
        Def("ASCII_VAL", 1, 1, a => SqlType.SmallInt, (c, a, f) =>
        {
            var s = Str(c, a[0]);
            if (s == null) return Value.Null;
            return Value.Int(s.Length == 0 ? 0 : s[0]);
        }, HintString);
        Def("HASH", 1, 1, a => SqlType.BigInt, (c, a, f) =>
        {
            var v = a[0].Eval(c);
            if (v.IsNull) return v;
            var bytes = v.Tag == VTag.Bytes ? v.AsBytes : Conv.Utf8.GetBytes(Conv.ToStr(v));
            // Firebird's legacy HASH (PJW/ELF hash)
            ulong h = 0;
            foreach (var b in bytes)
            {
                h = (h << 4) + b;
                var g = h & 0xF0000000;
                if (g != 0) h ^= g >> 24;
                h &= ~g;
            }
            return Value.Int((long)h);
        }, HintString);

        // ---- math
        Def("ABS", 1, 1, a => a[0].Type.IsNumeric ? a[0].Type : SqlType.Double, (c, a, f) =>
        {
            var v = a[0].Eval(c);
            if (v.IsNull) return v;
            if (v.Tag == VTag.Int)
            {
                if (v.L == long.MinValue) throw Err.NumericOverflow();
                return Value.Int(Math.Abs(v.L), v.Scale);
            }
            return D(Math.Abs(Conv.ToDouble(v)));
        }, (i, a) => SqlType.Double);
        Def("SIGN", 1, 1, a => SqlType.SmallInt, (c, a, f) =>
        {
            var v = a[0].Eval(c);
            if (v.IsNull) return v;
            return Value.Int(v.Tag == VTag.Int ? Math.Sign(v.L) : Math.Sign(Conv.ToDouble(v)));
        }, (i, a) => SqlType.Double);
        Def("CEIL", 1, 1, a => a[0].Type.IsExact ? SqlType.BigInt : SqlType.Double, (c, a, f) => Round(c, a, f, MidpointRounding.ToPositiveInfinity, true), (i, a) => SqlType.Double);
        Defs["CEILING"] = Defs["CEIL"];
        Def("FLOOR", 1, 1, a => a[0].Type.IsExact ? SqlType.BigInt : SqlType.Double, (c, a, f) => Round(c, a, f, MidpointRounding.ToNegativeInfinity, true), (i, a) => SqlType.Double);
        Def("ROUND", 1, 2, a => a[0].Type.IsNumeric ? a[0].Type : SqlType.Double, (c, a, f) => Round(c, a, f, MidpointRounding.AwayFromZero, false), (i, a) => i == 0 ? SqlType.Double : SqlType.Integer);
        Def("TRUNC", 1, 2, a => a[0].Type.IsNumeric ? a[0].Type : SqlType.Double, (c, a, f) => Round(c, a, f, MidpointRounding.ToZero, false), (i, a) => i == 0 ? SqlType.Double : SqlType.Integer);
        Def("MOD", 2, 2, a => a[0].Type.IsExact ? SqlType.Exact(a[0].Type.Kind, 0) : SqlType.BigInt, (c, a, f) =>
        {
            var x = a[0].Eval(c);
            var y = a[1].Eval(c);
            if (x.IsNull || y.IsNull) return Value.Null;
            var lx = (long)Math.Round(Conv.ToDouble(x), MidpointRounding.AwayFromZero);
            var ly = (long)Math.Round(Conv.ToDouble(y), MidpointRounding.AwayFromZero);
            if (x.Tag == VTag.Int && x.Scale == 0) lx = x.L;
            if (y.Tag == VTag.Int && y.Scale == 0) ly = y.L;
            if (ly == 0) throw Err.DivideByZero();
            return Value.Int(lx % ly);
        }, (i, a) => SqlType.BigInt);
        Def("POWER", 2, 2, DoubleT, (c, a, f) =>
        {
            var x = Dbl(c, a[0]);
            var y = Dbl(c, a[1]);
            if (x == null || y == null) return Value.Null;
            if (x == 0 && y < 0) throw Err.Random("Zero to the negative power is not allowed");
            if (x < 0 && Math.Floor(y.Value) != y) throw Err.Random("Negative argument to non-integer power");
            return D(Math.Pow(x.Value, y.Value));
        }, (i, a) => SqlType.Double);
        Def("SQRT", 1, 1, DoubleT, (c, a, f) =>
        {
            var x = Dbl(c, a[0]);
            if (x == null) return Value.Null;
            if (x < 0) throw Err.Random("Negative argument to SQRT");
            return D(Math.Sqrt(x.Value));
        }, (i, a) => SqlType.Double);
        Def("EXP", 1, 1, DoubleT, (c, a, f) => Unary(c, a, Math.Exp), (i, a) => SqlType.Double);
        Def("LN", 1, 1, DoubleT, (c, a, f) =>
        {
            var x = Dbl(c, a[0]);
            if (x == null) return Value.Null;
            if (x <= 0) throw Err.Random("Argument for LN must be positive");
            return D(Math.Log(x.Value));
        }, (i, a) => SqlType.Double);
        Def("LOG10", 1, 1, DoubleT, (c, a, f) =>
        {
            var x = Dbl(c, a[0]);
            if (x == null) return Value.Null;
            if (x <= 0) throw Err.Random("Argument for LOG10 must be positive");
            return D(Math.Log10(x.Value));
        }, (i, a) => SqlType.Double);
        Def("LOG", 2, 2, DoubleT, (c, a, f) =>
        {
            var b = Dbl(c, a[0]);
            var x = Dbl(c, a[1]);
            if (b == null || x == null) return Value.Null;
            if (b <= 0 || x <= 0) throw Err.Random("Arguments for LOG must be positive");
            return D(Math.Log(x.Value) / Math.Log(b.Value));
        }, (i, a) => SqlType.Double);
        Def("PI", 0, 0, DoubleT, (c, a, f) => Value.Dbl(Math.PI));
        Def("RAND", 0, 0, DoubleT, (c, a, f) => Value.Dbl(Random.Shared.NextDouble()), deterministic: false);
        Def("SIN", 1, 1, DoubleT, (c, a, f) => Unary(c, a, Math.Sin), (i, a) => SqlType.Double);
        Def("COS", 1, 1, DoubleT, (c, a, f) => Unary(c, a, Math.Cos), (i, a) => SqlType.Double);
        Def("TAN", 1, 1, DoubleT, (c, a, f) => Unary(c, a, Math.Tan), (i, a) => SqlType.Double);
        Def("COT", 1, 1, DoubleT, (c, a, f) => Unary(c, a, x => 1 / Math.Tan(x)), (i, a) => SqlType.Double);
        Def("ASIN", 1, 1, DoubleT, (c, a, f) => Unary(c, a, Math.Asin), (i, a) => SqlType.Double);
        Def("ACOS", 1, 1, DoubleT, (c, a, f) => Unary(c, a, Math.Acos), (i, a) => SqlType.Double);
        Def("ATAN", 1, 1, DoubleT, (c, a, f) => Unary(c, a, Math.Atan), (i, a) => SqlType.Double);
        Def("SINH", 1, 1, DoubleT, (c, a, f) => Unary(c, a, Math.Sinh), (i, a) => SqlType.Double);
        Def("COSH", 1, 1, DoubleT, (c, a, f) => Unary(c, a, Math.Cosh), (i, a) => SqlType.Double);
        Def("TANH", 1, 1, DoubleT, (c, a, f) => Unary(c, a, Math.Tanh), (i, a) => SqlType.Double);
        Def("ASINH", 1, 1, DoubleT, (c, a, f) => Unary(c, a, Math.Asinh), (i, a) => SqlType.Double);
        Def("ACOSH", 1, 1, DoubleT, (c, a, f) => Unary(c, a, Math.Acosh), (i, a) => SqlType.Double);
        Def("ATANH", 1, 1, DoubleT, (c, a, f) => Unary(c, a, Math.Atanh), (i, a) => SqlType.Double);
        Def("ATAN2", 2, 2, DoubleT, (c, a, f) =>
        {
            var y = Dbl(c, a[0]);
            var x = Dbl(c, a[1]);
            if (x == null || y == null) return Value.Null;
            return D(Math.Atan2(y.Value, x.Value));
        }, (i, a) => SqlType.Double);

        // ---- bit operations
        Def("BIN_AND", 1, 255, BinType, (c, a, f) => Bin(c, a, (x, y) => x & y), (i, a) => SqlType.BigInt);
        Def("BIN_OR", 1, 255, BinType, (c, a, f) => Bin(c, a, (x, y) => x | y), (i, a) => SqlType.BigInt);
        Def("BIN_XOR", 1, 255, BinType, (c, a, f) => Bin(c, a, (x, y) => x ^ y), (i, a) => SqlType.BigInt);
        Def("BIN_NOT", 1, 1, BinType, (c, a, f) =>
        {
            var x = Long(c, a[0]);
            return x == null ? Value.Null : Value.Int(~x.Value);
        }, (i, a) => SqlType.BigInt);
        Def("BIN_SHL", 2, 2, a => SqlType.BigInt, (c, a, f) =>
        {
            var x = Long(c, a[0]);
            var y = Long(c, a[1]);
            return x == null || y == null ? Value.Null : Value.Int(x.Value << (int)y.Value);
        }, (i, a) => SqlType.BigInt);
        Def("BIN_SHR", 2, 2, a => SqlType.BigInt, (c, a, f) =>
        {
            var x = Long(c, a[0]);
            var y = Long(c, a[1]);
            return x == null || y == null ? Value.Null : Value.Int(x.Value >> (int)y.Value);
        }, (i, a) => SqlType.BigInt);

        // ---- date / time
        Def("EXTRACT", 2, 2, a => ExtractType(((ConstNode)a[0]).Value.AsString), (c, a, f) =>
            Extract(((ConstNode)a[0]).Value.AsString, a[1].Eval(c)), (i, a) => SqlType.Timestamp);
        Def("DATEADD", 3, 3, a => a[2].Type.IsDateTime ? a[2].Type : SqlType.Timestamp, (c, a, f) =>
        {
            var part = ((ConstNode)a[0]).Value.AsString;
            var n = a[1].Eval(c);
            var d = a[2].Eval(c);
            if (n.IsNull || d.IsNull) return Value.Null;
            long amount;
            if (part == "MILLISECOND" || part == "SECOND") amount = (long)Math.Truncate(Conv.ToDouble(n));
            else amount = Conv.ToInt64(Conv.CastExact(n.Tag == VTag.Int ? n : Value.Int((long)Math.Truncate(Conv.ToDouble(n))), 0, TypeKind.BigInt));
            return Coercion.ToType(DateAdd(part, amount, d), f.Type);
        }, (i, a) => i == 1 ? SqlType.BigInt : SqlType.Timestamp);
        Def("DATEDIFF", 3, 3, a => ((ConstNode)a[0]).Value.AsString == "MILLISECOND"
            ? new SqlType(TypeKind.BigInt, scale: 1, precision: 18, subType: 1) : SqlType.BigInt, (c, a, f) =>
        {
            var part = ((ConstNode)a[0]).Value.AsString;
            var x = a[1].Eval(c);
            var y = a[2].Eval(c);
            if (x.IsNull || y.IsNull) return Value.Null;
            return DateDiff(part, x, y);
        }, (i, a) => a[3 - i]?.Type.IsDateTime == true ? a[3 - i]!.Type : SqlType.Timestamp);

        // ---- uuid
        Def("GEN_UUID", 0, 0, a => new SqlType(TypeKind.Char, 16, charset: Charset.Octets),
            (c, a, f) => Value.Bytes(UuidBytes(Guid.NewGuid())), deterministic: false);
        Def("UUID_TO_CHAR", 1, 1, a => SqlType.Char(36, Charset.Ascii), (c, a, f) =>
        {
            var v = a[0].Eval(c);
            if (v.IsNull) return v;
            var b = v.Tag == VTag.Bytes ? v.AsBytes : Conv.Utf8.GetBytes(Conv.ToStr(v));
            return S(UuidToChar(b));
        }, (i, a) => new SqlType(TypeKind.Char, 16, charset: Charset.Octets));
        Def("CHAR_TO_UUID", 1, 1, a => new SqlType(TypeKind.Char, 16, charset: Charset.Octets), (c, a, f) =>
        {
            var s = Str(c, a[0]);
            return s == null ? Value.Null : Value.Bytes(CharToUuid(s));
        }, (i, a) => SqlType.VarChar(36, Charset.Ascii));

        // ---- context
        Def("RDB$GET_CONTEXT", 2, 2, a => SqlType.VarChar(255), (c, a, f) =>
        {
            var ns = Str(c, a[0]);
            var name = Str(c, a[1]);
            if (ns == null || name == null) return Value.Null;
            return GetContext(c, ns, name);
        }, (i, a) => SqlType.VarChar(80), deterministic: false);
        Def("RDB$SET_CONTEXT", 3, 3, a => SqlType.Integer, (c, a, f) =>
        {
            var ns = Str(c, a[0]);
            var name = Str(c, a[1]);
            var val = a[2].Eval(c);
            if (ns == null || name == null) throw Err.Random("Invalid namespace or variable name");
            var dict = ns switch
            {
                "USER_SESSION" => c.Att.SessionContext,
                "USER_TRANSACTION" => c.Tx.Context,
                "SYSTEM" => throw Err.Random("Cannot change read-only variable " + name),
                _ => throw Err.Random("Invalid namespace name " + ns + " passed to RDB$SET_CONTEXT"),
            };
            var existed = dict.ContainsKey(name);
            if (val.IsNull) dict.Remove(name);
            else dict[name] = Conv.ToStr(val);
            return Value.Int(existed ? 1 : 0);
        }, (i, a) => i == 2 ? SqlType.VarChar(255) : SqlType.VarChar(80), deterministic: false);
        Def("MAXVALUE", 1, 255, a => CommonType(a), (c, a, f) => MinMax(c, a, f, true), (i, a) => a.FirstOrDefault(x => x != null)?.Type);
        Def("MINVALUE", 1, 255, a => CommonType(a), (c, a, f) => MinMax(c, a, f, false), (i, a) => a.FirstOrDefault(x => x != null)?.Type);
    }

    static Value GetContext(Ctx c, string ns, string name)
    {
        switch (ns)
        {
            case "USER_SESSION":
                return c.Att.SessionContext.TryGetValue(name, out var s) ? S(s) : Value.Null;
            case "USER_TRANSACTION":
                return c.Tx.Context.TryGetValue(name, out var t) ? S(t) : Value.Null;
            case "SYSTEM":
                return name switch
                {
                    "ENGINE_VERSION" => S("3.0.11"),
                    "DB_NAME" => S(c.Db.Name),
                    "NETWORK_PROTOCOL" => S("TCPv4"),
                    "CLIENT_ADDRESS" => S("127.0.0.1"),
                    "CURRENT_USER" => S(c.Att.User),
                    "CURRENT_ROLE" => S(c.Att.Role),
                    "SESSION_ID" => S(c.Att.Id.ToString(CultureInfo.InvariantCulture)),
                    "TRANSACTION_ID" => S(c.Tx.Id.ToString(CultureInfo.InvariantCulture)),
                    "ISOLATION_LEVEL" => S(c.Tx.Snapshot ? "SNAPSHOT" : "READ COMMITTED"),
                    "LOCK_TIMEOUT" => S("-1"),
                    "READ_ONLY" => S(c.Tx.ReadOnly ? "TRUE" : "FALSE"),
                    "CLIENT_PID" => S("0"),
                    "CLIENT_PROCESS" => S("fakebird"),
                    "GLOBAL_CN" => S(c.Db.CommitSeq.ToString(CultureInfo.InvariantCulture)),
                    _ => throw Err.ContextVarNotFound(name, ns),
                };
            case "DDL_TRIGGER":
                return Value.Null;
        }
        throw Err.Random("Invalid namespace name " + ns + " passed to RDB$GET_CONTEXT");
    }

    static SqlType PadType(XNode[] a)
    {
        if (a[1] is ConstNode { Value.IsNull: false } k) return SqlType.VarChar((int)Math.Max(1, Math.Min(Conv.ToInt64(k.Value), 8191)));
        return SqlType.VarChar(8191);
    }

    static Value Pad(Ctx c, XNode[] a, bool left)
    {
        var s = Str(c, a[0]);
        var n = Long(c, a[1]);
        if (s == null || n == null) return Value.Null;
        var pad = " ";
        if (a.Length > 2)
        {
            pad = Str(c, a[2])!;
            if (pad == null) return Value.Null;
        }
        if (n < 0) throw Err.Random("Invalid length parameter to LPAD/RPAD. Negative integers are not allowed.");
        return S(PadOrCut(s, (int)n.Value, pad, left));
    }

    static Value Round(Ctx c, XNode[] a, FuncNode f, MidpointRounding mode, bool toInteger)
    {
        var v = a[0].Eval(c);
        if (v.IsNull) return v;
        var digits = 0L;
        if (a.Length > 1)
        {
            var d = Long(c, a[1]);
            if (d == null) return Value.Null;
            digits = d.Value;
        }
        if (v.Tag == VTag.String) v = Conv.ParseNumber(v.AsString);
        if (v.Tag == VTag.Int)
        {
            if (digits >= v.Scale) return toInteger ? Value.Int(v.L) : v;
            var drop = v.Scale - digits;
            if (drop > 18) return toInteger ? Value.Int(0) : Value.Int(0, v.Scale);
            var p = Value.Pow10[drop];
            var q = Math.DivRem(v.L, p, out var r);
            switch (mode)
            {
                case MidpointRounding.AwayFromZero:
                    if (Math.Abs(r) * 2 >= p) q += v.L < 0 ? -1 : 1;
                    break;
                case MidpointRounding.ToPositiveInfinity:
                    if (r > 0) q++;
                    break;
                case MidpointRounding.ToNegativeInfinity:
                    if (r < 0) q--;
                    break;
            }
            if (toInteger) return Value.Int(q);
            return Value.Int(q * p, v.Scale);
        }
        var x = Conv.ToDouble(v);
        double res;
        if (toInteger) res = mode == MidpointRounding.ToPositiveInfinity ? Math.Ceiling(x) : Math.Floor(x);
        else
        {
            var m = Math.Pow(10, digits);
            res = mode switch
            {
                MidpointRounding.ToZero => Math.Truncate(x * m) / m,
                _ => Math.Round(x * m, MidpointRounding.AwayFromZero) / m,
            };
        }
        return D(res);
    }

    static SqlType BinType(XNode[] a)
        => a.All(x => x.Type.Kind is TypeKind.SmallInt or TypeKind.Integer) ? SqlType.Integer : SqlType.BigInt;

    static Value Bin(Ctx c, XNode[] a, Func<long, long, long> op)
    {
        long? acc = null;
        foreach (var n in a)
        {
            var x = Long(c, n);
            if (x == null) return Value.Null;
            acc = acc == null ? x : op(acc.Value, x.Value);
        }
        return Value.Int(acc!.Value);
    }

    static Value MinMax(Ctx c, XNode[] a, FuncNode f, bool max)
    {
        Value best = Value.Null;
        foreach (var n in a)
        {
            var v = n.Eval(c);
            if (v.IsNull) return Value.Null;
            if (best.IsNull || (max ? Conv.Compare(v, best, f.Type.Collation) > 0 : Conv.Compare(v, best, f.Type.Collation) < 0)) best = v;
        }
        return Coercion.ToType(best, f.Type);
    }

    /// <summary>Common result type for CASE/COALESCE/UNION branches (Firebird "data type of results").</summary>
    public static SqlType CommonType(IEnumerable<XNode> nodes) => CommonType(nodes.Select(n => n.Type));

    public static SqlType CommonType(IEnumerable<SqlType> types)
    {
        SqlType? result = null;
        foreach (var t in types)
        {
            if (t.Kind == TypeKind.Null) continue;
            if (result == null)
            {
                result = t;
                continue;
            }
            result = Merge(result, t);
        }
        return result ?? SqlType.Null;
    }

    static SqlType Merge(SqlType a, SqlType b)
    {
        if (a.Equals(b)) return a;
        if (a.Kind == TypeKind.Blob || b.Kind == TypeKind.Blob)
        {
            if (a.Kind == TypeKind.Blob && b.Kind == TypeKind.Blob) return a.SubType == 1 || b.SubType == 1 ? SqlType.BlobText : SqlType.BlobBinary;
            var blob = a.Kind == TypeKind.Blob ? a : b;
            return blob;
        }
        if (a.IsString || b.IsString)
        {
            int Len(SqlType t) => t.IsString ? t.Length : t.Kind switch
            {
                TypeKind.SmallInt => 6, TypeKind.Integer => 11, TypeKind.BigInt => 20,
                TypeKind.Float or TypeKind.Double => 23, TypeKind.Date => 10, TypeKind.Time => 13,
                TypeKind.Timestamp => 24, TypeKind.Boolean => 5, _ => 32,
            } + (t.Scale > 0 ? 1 : 0);
            var cs = a.IsString ? a.Charset : b.Charset;
            if (a.IsString && b.IsString && a.Charset != b.Charset) cs = a.Charset == Charset.Octets ? b.Charset : a.Charset;
            var col = a.IsString && a.Collation != Collation.Binary ? a.Collation : b.IsString ? b.Collation : Collation.Binary;
            var len = Math.Max(Len(a), Len(b));
            if (a.Kind == TypeKind.Char && b.Kind == TypeKind.Char) return SqlType.Char(len, cs, col);
            return SqlType.VarChar(len, cs, col);
        }
        if (a.IsApprox || b.IsApprox)
        {
            if (a.IsNumeric && b.IsNumeric) return SqlType.Double;
        }
        if (a.IsExact && b.IsExact)
        {
            var scale = Math.Max(a.Scale, b.Scale);
            var kind = (TypeKind)Math.Max((int)a.Kind, (int)b.Kind);
            return SqlType.Exact(scale > 0 ? TypeKind.BigInt : kind, scale);
        }
        if (a.IsDateTime && b.IsDateTime)
        {
            if (a.Kind == TypeKind.Timestamp || b.Kind == TypeKind.Timestamp) return SqlType.Timestamp;
            if (a.Kind == b.Kind) return a;
            throw Err.Random("Data type of the results is incompatible");
        }
        if (a.Kind == TypeKind.Boolean && b.Kind == TypeKind.Boolean) return a;
        throw Err.Dsql(-104, "Datatypes are not comparable in expression");
    }
}
