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
    /// <summary>Result nullability; by default nullable when any argument is (Firebird's initResult).</summary>
    public Func<XNode[], bool>? Nullable;
}

public static class Builtins
{
    static readonly Dictionary<string, BuiltinDef> Defs = new(StringComparer.Ordinal);
    static readonly SqlType LongVarchar = SqlType.VarChar(8191);

    public static BuiltinDef? Find(string name) => Defs.TryGetValue(name, out var d) ? d : null;

    static void Def(string name, int min, int max, Func<XNode[], SqlType> type, Func<Ctx, XNode[], FuncNode, Value> impl,
        Func<int, XNode?[], SqlType?>? hint = null, bool deterministic = true, Func<XNode[], bool>? nullable = null)
        => Defs[name] = new BuiltinDef
        {
            Name = name, MinArgs = min, MaxArgs = max, ResultType = type, Impl = impl, ParamHint = hint,
            Deterministic = deterministic, Nullable = nullable,
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

    /// <summary>VARCHAR as long as the value converted to text, in the value's charset (ASCII for non-text);
    /// blobs stay blobs and a NULL literal gives a NULL string (Firebird's makeVarying / makeNullString).</summary>
    static SqlType StrResult(XNode a, int? len = null)
    {
        var t = a.Type;
        if (t.Kind == TypeKind.Blob) return t;
        if (t.Kind == TypeKind.Null) return SqlType.Null;
        var cs = t.IsString ? t.Charset : Charset.Ascii;
        var l = Math.Max(0, Math.Min(len ?? TypeRules.StringLength(t), MaxChars(cs)));
        return SqlType.VarChar(l, cs, t.IsString ? t.Collation : Collation.Binary);
    }

    /// <summary>Longest string in characters (MAX_STR_SIZE 32765 bytes / bytes per character).</summary>
    static int MaxChars(Charset cs) => 32765 / (cs == Charset.Utf8 ? 4 : cs == Charset.UnicodeFss ? 3 : 1);

    static FbError BadSubstringLength(long n)
        => new StatusBuilder().Gds(FbMessages.bad_substring_length).Num((int)n).Build();

    /// <summary>Length in bytes of text stored in the type's character set.</summary>
    static long ByteLength(string s, SqlType t) => t.Charset switch
    {
        Charset.Utf8 or Charset.UnicodeFss => Conv.Utf8.GetByteCount(s),
        Charset.Octets => Conv.Utf8.GetByteCount(s),
        _ => Conv.CharLength(s),
    };

    static long? ConstLong(XNode n) => n is ConstNode { Value.IsNull: false } k && (k.Type.IsExact || k.Type.IsApprox) ? Conv.ToInt64(k.Value) : null;

    static SqlType HintString(int i, XNode?[] a) => LongVarchar;
    static SqlType? HintInt(int i, XNode?[] a) => SqlType.Integer;

    static Value S(string s) => Value.Str(s);
    static Value D(double d)
    {
        if (double.IsInfinity(d)) throw Err.FloatOverflow();
        if (double.IsNaN(d)) throw Err.Random("Floating-point invalid operation");
        return Value.Dbl(d);
    }

    /// <summary>expression evaluation not supported / Argument for F must be ... (isc_sysf_*).</summary>
    static FbError Sysf(int code, string function)
        => new StatusBuilder().Gds(Err.isc_expression_eval_err).Gds(code).Str(function).Build();

    static Value Checked(Ctx c, XNode[] a, string name, Func<double, bool> valid, int code, Func<double, double> f)
    {
        var x = Dbl(c, a[0]);
        if (x == null) return Value.Null;
        if (!valid(x.Value)) throw Sysf(code, name);
        return D(f(x.Value));
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

    public static SqlType ExtractType(string part) => part switch
    {
        "SECOND" => SqlType.ExactExpr(TypeKind.Integer, 4),
        "MILLISECOND" => SqlType.ExactExpr(TypeKind.Integer, 1),
        _ => SqlType.SmallInt,
    };

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
        Def("UPPER", 1, 1, a => a[0].Type.IsTextual || a[0].Type.Kind == TypeKind.Null ? a[0].Type : StrResult(a[0]), (c, a, f) =>
        {
            var v = a[0].Eval(c);
            if (v.IsNull) return v;
            if (v.Tag == VTag.Bytes) return v; // OCTETS have no case
            return S(CaseMap.Upper(Conv.ToStr(v)));
        }, HintString);
        Def("LOWER", 1, 1, a => a[0].Type.IsTextual || a[0].Type.Kind == TypeKind.Null ? a[0].Type : StrResult(a[0]), (c, a, f) =>
        {
            var v = a[0].Eval(c);
            if (v.IsNull) return v;
            if (v.Tag == VTag.Bytes) return v;
            return S(CaseMap.Lower(Conv.ToStr(v)));
        }, HintString);
        Def("TRIM", 3, 3, a => a[2].Type.Kind == TypeKind.Null ? SqlType.VarChar(1, Charset.Ascii) : StrResult(a[2]), (c, a, f) =>
        {
            var spec = ((ConstNode)a[0]).Value.AsString;
            var src = a[2].Eval(c);
            if (src.IsNull) return Value.Null;
            var charsV = a[1].Eval(c);
            if (charsV.IsNull) return Value.Null;
            if (src.Tag == VTag.Bytes)
            {
                // OCTETS: the space character is NUL
                var b = src.AsBytes;
                var tb = a[1] is ConstNode { Value.Tag: VTag.String, Value.AsString: " " } && f.Args[1].Type.Kind == TypeKind.VarChar && charsV.AsString == " "
                    ? [(byte)0] : charsV.Tag == VTag.Bytes ? charsV.AsBytes : Conv.Utf8.GetBytes(Conv.ToStr(charsV));
                if (tb.Length == 0) return src;
                int lo = 0, hi = b.Length;
                if (spec is "BOTH" or "LEADING")
                    while (hi - lo >= tb.Length && b.AsSpan(lo, tb.Length).SequenceEqual(tb)) lo += tb.Length;
                if (spec is "BOTH" or "TRAILING")
                    while (hi - lo >= tb.Length && b.AsSpan(hi - tb.Length, tb.Length).SequenceEqual(tb)) hi -= tb.Length;
                return Value.Bytes(b[lo..hi]);
            }
            var s = Conv.ToStr(src);
            var chars = Conv.ToStr(charsV);
            if (chars.Length == 0) return S(s);
            if (spec is "BOTH" or "LEADING")
                while (s.StartsWith(chars, StringComparison.Ordinal)) s = s[chars.Length..];
            if (spec is "BOTH" or "TRAILING")
                while (s.EndsWith(chars, StringComparison.Ordinal)) s = s[..^chars.Length];
            return S(s);
        }, HintString, nullable: a => !a[2].NotNull || !a[1].NotNull);
        Def("SUBSTRING", 2, 3, a => a.Length > 2 && ConstLong(a[2]) is { } n && a[0].Type.Kind != TypeKind.Blob
            ? (n < 0 ? throw BadSubstringLength(n) : StrResult(a[0], (int)Math.Min(n, TypeRules.StringLength(a[0].Type)))) : StrResult(a[0]), (c, a, f) =>
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
                if (len < 0) throw BadSubstringLength(len.Value);
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
            // LEFT and RIGHT are SUBSTRINGs: a negative length reports SUBSTRING
            if (n < 0) throw BadSubstringLength(n.Value);
            return S(Substr(s, 1, n));
        }, (i, a) => i == 0 ? LongVarchar : SqlType.Integer, nullable: a => a[0].Type.Kind != TypeKind.Blob && a.Any(x => !x.NotNull));
        Def("RIGHT", 2, 2, a => StrResult(a[0]), (c, a, f) =>
        {
            var s = Str(c, a[0]);
            var n = Long(c, a[1]);
            if (s == null || n == null) return Value.Null;
            if (n < 0) throw BadSubstringLength(n.Value);
            var len = Conv.CharLength(s);
            if (n >= len) return S(s);
            return S(Substr(s, len - n.Value + 1, n));
        }, (i, a) => i == 0 ? LongVarchar : SqlType.Integer, nullable: a => a[0].Type.Kind != TypeKind.Blob && a.Any(x => !x.NotNull));
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
            return Value.Int(ByteLength(Conv.ToStr(v), a[0].Type));
        }, HintString);
        Def("BIT_LENGTH", 1, 1, a => a[0].Type.Kind == TypeKind.Blob ? SqlType.BigInt : SqlType.Integer, (c, a, f) =>
        {
            var v = a[0].Eval(c);
            if (v.IsNull) return v;
            if (v.Tag == VTag.Bytes) return Value.Int(v.AsBytes.Length * 8L);
            return Value.Int(ByteLength(Conv.ToStr(v), a[0].Type) * 8L);
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
                if (start <= 0)
                    throw new StatusBuilder().Gds(Err.isc_expression_eval_err).Gds(FbMessages.sysf_argnmustbe_positive).Num(3).Str("POSITION").Build();
            }
            var col = a[1].Type.Collation;
            sub = Folding.Fold(sub, col);
            s = Folding.Fold(s, col);
            if (sub.Length == 0) return Value.Int(start <= Conv.CharLength(s) + 1 ? start : 0);
            var from = Substr(s, start, null);
            var idx = from.IndexOf(sub, StringComparison.Ordinal);
            if (idx < 0) return Value.Int(0);
            return Value.Int(Conv.CharLength(from[..idx]) + start);
        }, (i, a) => i == 2 ? SqlType.Integer : LongVarchar);
        Def("REPLACE", 3, 3, ReplaceType, (c, a, f) =>
        {
            var s = Str(c, a[0]);
            var find = Str(c, a[1]);
            var repl = Str(c, a[2]);
            if (s == null || find == null || repl == null) return Value.Null;
            if (find.Length == 0) return S(s);
            var col = a[0].Type.Collation;
            if (col == Collation.Binary || col == Collation.Unicode) return S(s.Replace(find, repl, StringComparison.Ordinal));
            // case/accent-insensitive collations find the matches on the folded text
            var folded = Folding.Fold(s, col);
            var ff = Folding.Fold(find, col);
            var sb = new StringBuilder();
            var pos = 0;
            for (var at = folded.IndexOf(ff, StringComparison.Ordinal); at >= 0; at = folded.IndexOf(ff, pos, StringComparison.Ordinal))
            {
                sb.Append(s, pos, at - pos).Append(repl);
                pos = at + ff.Length;
            }
            return S(sb.Append(s, pos, s.Length - pos).ToString());
        }, HintString);
        Def("REVERSE", 1, 1, a => StrResult(a[0]), (c, a, f) =>
        {
            var s = Str(c, a[0]);
            if (s == null) return Value.Null;
            var runes = s.EnumerateRunes().ToArray();
            Array.Reverse(runes);
            return S(string.Concat(runes.Select(r => r.ToString())));
        }, HintString);
        Def("LPAD", 2, 3, a => PadType(a), (c, a, f) => Pad(c, a, true), (i, a) => i == 1 ? SqlType.Integer : LongVarchar);
        Def("RPAD", 2, 3, a => PadType(a), (c, a, f) => Pad(c, a, false), (i, a) => i == 1 ? SqlType.Integer : LongVarchar);
        Def("OVERLAY", 3, 4, a => a.Any(x => x.Type.Kind == TypeKind.Null) ? SqlType.Null
            : a[0].Type.Kind == TypeKind.Blob ? a[0].Type : a[1].Type.Kind == TypeKind.Blob ? a[1].Type
            : StrResult(a[0], TypeRules.StringLength(a[0].Type) + TypeRules.StringLength(a[1].Type)), (c, a, f) =>
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
        Def("ASCII_CHAR", 1, 1, a => a[0].Type.Kind == TypeKind.Null ? SqlType.Null : SqlType.Char(1, Charset.None), (c, a, f) =>
        {
            var n = Long(c, a[0]);
            if (n == null) return Value.Null;
            if (n is < 0 or > 255) throw Err.NumericOverflow();
            return S(((char)n.Value).ToString());
        }, HintInt);
        Def("SUBSTRING_SIMILAR", 3, 3, a => StrResult(a[0]), (c, a, f) =>
        {
            var v = Str(c, a[0]);
            var p = Str(c, a[1]);
            var e = Str(c, a[2]);
            if (v == null || p == null || e == null) return Value.Null;
            var col = a[0].Type.Collation;
            var m = SimilarTo.CompileSubstring(Folding.Fold(p, col), e, col is Collation.CaseInsensitive or Collation.CaseAccentInsensitive)
                .Match(Folding.Fold(v, col));
            if (!m.Success) return Value.Null;
            var g = m.Groups[1];
            // positions in the folded text map 1:1 to the original (folding keeps one code unit per code unit)
            return S(v.Substring(g.Index, g.Length));
        }, HintString, nullable: _ => true);
        Def("UNICODE_VAL", 1, 1, a => SqlType.Integer, (c, a, f) =>
        {
            var s = Str(c, a[0]);
            if (s == null) return Value.Null;
            return Value.Int(s.Length == 0 ? 0 : char.ConvertToUtf32(s, 0));
        }, HintString);
        Def("UNICODE_CHAR", 1, 1, a => a[0].Type.Kind == TypeKind.Null ? SqlType.Null : SqlType.Char(1, Charset.Utf8), (c, a, f) =>
        {
            var n = Long(c, a[0]);
            if (n == null) return Value.Null;
            if (n is < 0 or > 0x10FFFF || n is >= 0xD800 and <= 0xDFFF) throw Err.NumericOverflow();
            return S(char.ConvertFromUtf32((int)n.Value));
        }, HintInt);
        Def("ASCII_VAL", 1, 1, a => SqlType.SmallInt, (c, a, f) =>
        {
            var s = Str(c, a[0]);
            if (s == null) return Value.Null;
            if (s.Length > 0 && s[0] > 127) throw new StatusBuilder().Gds(Err.isc_arith_except).Gds(FbMessages.transliteration_failed).Build();
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
        Def("ABS", 1, 1, a => a[0].Type.Kind switch
        {
            TypeKind.Null => SqlType.Integer,
            TypeKind.SmallInt => SqlType.ExactExpr(TypeKind.Integer, a[0].Type.Scale),
            TypeKind.Integer => SqlType.ExactExpr(TypeKind.BigInt, a[0].Type.Scale),
            TypeKind.BigInt or TypeKind.Float or TypeKind.Double => a[0].Type,
            _ => SqlType.Double,
        }, (c, a, f) =>
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
        Def("CEIL", 1, 1, CeilFloorType, (c, a, f) => Round(c, a, f, MidpointRounding.ToPositiveInfinity, true), (i, a) => SqlType.Double);
        Defs["CEILING"] = Defs["CEIL"];
        Def("FLOOR", 1, 1, CeilFloorType, (c, a, f) => Round(c, a, f, MidpointRounding.ToNegativeInfinity, true), (i, a) => SqlType.Double);
        Def("ROUND", 1, 2, a => RoundType(a, round: true), (c, a, f) => Round(c, a, f, MidpointRounding.AwayFromZero, false), (i, a) => i == 0 ? SqlType.Double : SqlType.Integer);
        Def("TRUNC", 1, 2, a => RoundType(a, round: false), (c, a, f) => Round(c, a, f, MidpointRounding.ToZero, false), (i, a) => i == 0 ? SqlType.Double : SqlType.Integer);
        Def("MOD", 2, 2, a => a.Any(x => x.Type.Kind == TypeKind.Null) ? SqlType.Integer
            : a[0].Type.IsExact ? TypeRules.Exact(a[0].Type.Kind, 0, a[0].Type.SubType) : SqlType.BigInt, (c, a, f) =>
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
            if (x == 0 && y < 0) throw Sysf(FbMessages.sysf_invalid_zeropowneg, "POWER");
            if (x < 0 && (!a[1].Type.IsExact || Math.Floor(y.Value) != y)) throw Sysf(FbMessages.sysf_invalid_negpowfp, "POWER");
            return D(Math.Pow(x.Value, y.Value));
        }, (i, a) => SqlType.Double);
        Def("SQRT", 1, 1, DoubleT, (c, a, f) =>
        {
            var x = Dbl(c, a[0]);
            if (x == null) return Value.Null;
            if (x < 0) throw Sysf(FbMessages.sysf_argmustbe_nonneg, "SQRT");
            return D(Math.Sqrt(x.Value));
        }, (i, a) => SqlType.Double);
        Def("EXP", 1, 1, DoubleT, (c, a, f) => Unary(c, a, Math.Exp), (i, a) => SqlType.Double);
        Def("LN", 1, 1, DoubleT, (c, a, f) =>
        {
            var x = Dbl(c, a[0]);
            if (x == null) return Value.Null;
            if (x <= 0) throw Sysf(FbMessages.sysf_argmustbe_positive, "LN");
            return D(Math.Log(x.Value));
        }, (i, a) => SqlType.Double);
        Def("LOG10", 1, 1, DoubleT, (c, a, f) =>
        {
            var x = Dbl(c, a[0]);
            if (x == null) return Value.Null;
            if (x <= 0) throw Sysf(FbMessages.sysf_argmustbe_positive, "LOG10");
            return D(Math.Log10(x.Value));
        }, (i, a) => SqlType.Double);
        Def("LOG", 2, 2, DoubleT, (c, a, f) =>
        {
            var b = Dbl(c, a[0]);
            var x = Dbl(c, a[1]);
            if (b == null || x == null) return Value.Null;
            if (b <= 0) throw Sysf(FbMessages.sysf_basemustbe_positive, "LOG");
            if (x <= 0) throw Sysf(FbMessages.sysf_argmustbe_positive, "LOG");
            return D(Math.Log(x.Value) / Math.Log(b.Value));
        }, (i, a) => SqlType.Double);
        Def("PI", 0, 0, DoubleT, (c, a, f) => Value.Dbl(Math.PI));
        Def("RAND", 0, 0, DoubleT, (c, a, f) => Value.Dbl(Random.Shared.NextDouble()), deterministic: false);
        Def("SIN", 1, 1, DoubleT, (c, a, f) => Unary(c, a, Math.Sin), (i, a) => SqlType.Double);
        Def("COS", 1, 1, DoubleT, (c, a, f) => Unary(c, a, Math.Cos), (i, a) => SqlType.Double);
        Def("TAN", 1, 1, DoubleT, (c, a, f) => Unary(c, a, Math.Tan), (i, a) => SqlType.Double);
        Def("COT", 1, 1, DoubleT, (c, a, f) => Checked(c, a, "COT", x => x != 0, FbMessages.sysf_argmustbe_nonzero, x => 1 / Math.Tan(x)), (i, a) => SqlType.Double);
        Def("ASIN", 1, 1, DoubleT, (c, a, f) => Checked(c, a, "ASIN", x => x is >= -1 and <= 1, FbMessages.sysf_argmustbe_range_inc1_1, Math.Asin), (i, a) => SqlType.Double);
        Def("ACOS", 1, 1, DoubleT, (c, a, f) => Checked(c, a, "ACOS", x => x is >= -1 and <= 1, FbMessages.sysf_argmustbe_range_inc1_1, Math.Acos), (i, a) => SqlType.Double);
        Def("ATAN", 1, 1, DoubleT, (c, a, f) => Unary(c, a, Math.Atan), (i, a) => SqlType.Double);
        Def("SINH", 1, 1, DoubleT, (c, a, f) => Unary(c, a, Math.Sinh), (i, a) => SqlType.Double);
        Def("COSH", 1, 1, DoubleT, (c, a, f) => Unary(c, a, Math.Cosh), (i, a) => SqlType.Double);
        Def("TANH", 1, 1, DoubleT, (c, a, f) => Unary(c, a, Math.Tanh), (i, a) => SqlType.Double);
        // Firebird's own formulas for the inverse hyperbolic functions
        Def("ASINH", 1, 1, DoubleT, (c, a, f) => Unary(c, a, v => Math.Log(v + Math.Sqrt(v * v + 1))), (i, a) => SqlType.Double);
        Def("ACOSH", 1, 1, DoubleT, (c, a, f) => Checked(c, a, "ACOSH", x => x >= 1, FbMessages.sysf_argmustbe_gteq_one,
            v => Math.Log(v + Math.Sqrt(v - 1) * Math.Sqrt(v + 1))), (i, a) => SqlType.Double);
        Def("ATANH", 1, 1, DoubleT, (c, a, f) => Checked(c, a, "ATANH", x => x is > -1 and < 1, FbMessages.sysf_argmustbe_range_exc1_1,
            v => Math.Log((1 + v) / (1 - v)) / 2), (i, a) => SqlType.Double);
        Def("ATAN2", 2, 2, DoubleT, (c, a, f) =>
        {
            var y = Dbl(c, a[0]);
            var x = Dbl(c, a[1]);
            if (x == null || y == null) return Value.Null;
            return D(Math.Atan2(y.Value, x.Value));
        }, (i, a) => SqlType.Double);

        // ---- bit operations
        Def("BIN_AND", 1, 255, a => BinType(a, "BIN_AND"), (c, a, f) => Bin(c, a, (x, y) => x & y), (i, a) => SqlType.BigInt);
        Def("BIN_OR", 1, 255, a => BinType(a, "BIN_OR"), (c, a, f) => Bin(c, a, (x, y) => x | y), (i, a) => SqlType.BigInt);
        Def("BIN_XOR", 1, 255, a => BinType(a, "BIN_XOR"), (c, a, f) => Bin(c, a, (x, y) => x ^ y), (i, a) => SqlType.BigInt);
        Def("BIN_NOT", 1, 1, a => BinType(a, "BIN_NOT"), (c, a, f) =>
        {
            var x = Long(c, a[0]);
            return x == null ? Value.Null : Value.Int(~x.Value);
        }, (i, a) => SqlType.BigInt);
        Def("BIN_SHL", 2, 2, a => BinType(a, "BIN_SHL") is var _ && a[0].Type.Kind == TypeKind.Int128 ? SqlType.Int128 : SqlType.BigInt, (c, a, f) =>
        {
            var x = Long(c, a[0]);
            var y = Long(c, a[1]);
            return x == null || y == null ? Value.Null : Value.Int(x.Value << (int)y.Value);
        }, (i, a) => SqlType.BigInt);
        Def("BIN_SHR", 2, 2, a => BinType(a, "BIN_SHR") is var _ && a[0].Type.Kind == TypeKind.Int128 ? SqlType.Int128 : SqlType.BigInt, (c, a, f) =>
        {
            var x = Long(c, a[0]);
            var y = Long(c, a[1]);
            return x == null || y == null ? Value.Null : Value.Int(x.Value >> (int)y.Value);
        }, (i, a) => SqlType.BigInt);

        // ---- date / time
        Def("EXTRACT", 2, 2, a =>
        {
            DateFuncs.CheckExtract(((ConstNode)a[0]).Value.AsString, a[1].Type);
            return ExtractType(((ConstNode)a[0]).Value.AsString);
        }, (c, a, f) => DateFuncs.Extract(((ConstNode)a[0]).Value.AsString, a[1].Eval(c)), (i, a) => SqlType.Timestamp);
        Def("FIRST_DAY", 2, 2, a => a[1].Type.Kind == TypeKind.Timestamp ? SqlType.Timestamp : SqlType.Date, (c, a, f) =>
        {
            var v = a[1].Eval(c);
            return v.IsNull ? v : DateFuncs.FirstLastDay(((ConstNode)a[0]).Value.AsString, false, v);
        }, (i, a) => SqlType.Date);
        Def("LAST_DAY", 2, 2, a => a[1].Type.Kind == TypeKind.Timestamp ? SqlType.Timestamp : SqlType.Date, (c, a, f) =>
        {
            var v = a[1].Eval(c);
            return v.IsNull ? v : DateFuncs.FirstLastDay(((ConstNode)a[0]).Value.AsString, true, v);
        }, (i, a) => SqlType.Date);
        Def("DATEADD", 3, 3, a => a[2].Type.IsDateTime ? a[2].Type : SqlType.Timestamp, (c, a, f) =>
        {
            var part = ((ConstNode)a[0]).Value.AsString;
            var n = a[1].Eval(c);
            var d = a[2].Eval(c);
            if (n.IsNull || d.IsNull) return Value.Null;
            if (d.Tag == VTag.String) d = Conv.Cast(d, f.Type);
            return DateFuncs.DateAdd(part, n, d);
        }, (i, a) => i == 1 ? SqlType.BigInt : SqlType.Timestamp);
        Def("DATEDIFF", 3, 3, a => ((ConstNode)a[0]).Value.AsString == "MILLISECOND"
            ? SqlType.ExactExpr(TypeKind.BigInt, 1) : SqlType.BigInt, (c, a, f) =>
        {
            var part = ((ConstNode)a[0]).Value.AsString;
            var x = a[1].Eval(c);
            var y = a[2].Eval(c);
            if (x.IsNull || y.IsNull) return Value.Null;
            if (x.Tag == VTag.String) x = Conv.Cast(x, a[1].Type.IsDateTime ? a[1].Type : SqlType.Timestamp);
            if (y.Tag == VTag.String) y = Conv.Cast(y, a[2].Type.IsDateTime ? a[2].Type : SqlType.Timestamp);
            return DateFuncs.DateDiff(part, x, y);
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
        Def("RDB$GET_CONTEXT", 2, 2, a => SqlType.VarChar(255, Charset.None), (c, a, f) =>
        {
            var ns = Str(c, a[0]);
            var name = Str(c, a[1]);
            if (ns == null || name == null) return Value.Null;
            return GetContext(c, ns, name);
        }, (i, a) => SqlType.VarChar(80), deterministic: false, nullable: _ => true);
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
                _ => throw Err.CtxNamespace(ns, "RDB$SET_CONTEXT"),
            };
            var existed = dict.ContainsKey(name);
            if (val.IsNull) dict.Remove(name);
            else dict[name] = Conv.ToStr(val);
            return Value.Int(existed ? 1 : 0);
        }, (i, a) => i == 2 ? SqlType.VarChar(255) : SqlType.VarChar(80), deterministic: false, nullable: _ => false);
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
                    "ENGINE_VERSION" => S("5.0.4"),
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
        throw Err.CtxNamespace(ns, "RDB$GET_CONTEXT");
    }

    static SqlType PadType(XNode[] a)
    {
        if (a.Any(x => x.Type.Kind == TypeKind.Null)) return SqlType.Null;
        var v = a[0].Type;
        if (v.Kind == TypeKind.Blob) return v;
        if (a.Length > 2 && a[2].Type.Kind == TypeKind.Blob) return a[2].Type;
        var cs = v.IsString ? v.Charset : Charset.Ascii;
        var col = v.IsString ? v.Collation : Collation.Binary;
        var len = a[1] is ConstNode { Literal: { IsNull: false } lit } ? (int)Math.Max(0, Math.Min(Conv.ToInt64(lit), MaxChars(cs))) : MaxChars(cs);
        return SqlType.VarChar(len, cs, col);
    }

    /// <summary>makeReplace: room for every occurrence of the searched string being replaced.</summary>
    static SqlType ReplaceType(XNode[] a)
    {
        if (a.Any(x => x.Type.Kind == TypeKind.Null)) return SqlType.Null;
        var blob = a.FirstOrDefault(x => x.Type.Kind == TypeKind.Blob);
        if (blob != null) return blob.Type;
        var text = a.FirstOrDefault(x => x.Type.IsString)?.Type;
        var cs = text?.Charset ?? Charset.Ascii;
        int L(XNode x) => TypeRules.StringLength(x.Type);
        var searched = L(a[0]);
        var find = L(a[1]);
        var len = find == 0 ? searched : Math.Max(searched, searched + searched / find * (L(a[2]) - find));
        return SqlType.VarChar(Math.Min(len, MaxChars(cs)), cs, text?.Collation ?? Collation.Binary);
    }

    static Value Pad(Ctx c, XNode[] a, bool left)
    {
        var v = a[0].Eval(c);
        var n = Long(c, a[1]);
        if (v.IsNull || n == null) return Value.Null;
        var pv = a.Length > 2 ? a[2].Eval(c) : Value.Null;
        if (a.Length > 2 && pv.IsNull) return Value.Null;
        if (n < 0)
            throw new StatusBuilder().Gds(Err.isc_expression_eval_err).Gds(FbMessages.sysf_argnmustbe_nonneg).Num(2)
                .Str(left ? "LPAD" : "RPAD").Build();
        if (v.Tag == VTag.Bytes)
        {
            // binary strings pad with their space character, NUL
            var bytes = v.AsBytes;
            var padBytes = a.Length > 2 ? (pv.Tag == VTag.Bytes ? pv.AsBytes : Conv.Utf8.GetBytes(Conv.ToStr(pv))) : [(byte)0];
            var len = (int)n.Value;
            if (bytes.Length >= len) return Value.Bytes(left ? bytes[..len] : bytes[..len]);
            if (padBytes.Length == 0) return v;
            var fill = new byte[len - bytes.Length];
            for (var i = 0; i < fill.Length; i++) fill[i] = padBytes[i % padBytes.Length];
            return Value.Bytes(left ? [.. fill, .. bytes] : [.. bytes, .. fill]);
        }
        var pad = a.Length > 2 ? Conv.ToStr(pv) : " ";
        return S(PadOrCut(Conv.ToStr(v), (int)n.Value, pad, left));
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
            Int128 res128 = toInteger ? q : (Int128)q * p;
            // the result keeps the argument's type: a value that no longer fits is out of range
            var max = f.Type.Kind switch { TypeKind.SmallInt => short.MaxValue, TypeKind.Integer => int.MaxValue, _ => long.MaxValue };
            if (res128 > max || res128 < -max - 1) throw Err.NumericOverflow();
            return Value.Int((long)res128, toInteger ? 0 : v.Scale);
        }
        var x = Conv.ToDouble(v);
        double res;
        if (toInteger) res = mode == MidpointRounding.ToPositiveInfinity ? Math.Ceiling(x) : Math.Floor(x);
        else if (mode == MidpointRounding.ToZero)
        {
            // evlTrunc: modf of the value, then of the fraction scaled by 10^digits
            if (digits < 0)
            {
                var p = 1L;
                for (var k = digits; k < 0; k++) p *= 10;
                res = Math.Truncate(x / p) * p;
            }
            else
            {
                var whole = Math.Truncate(x);
                var r = x - whole;
                if (digits != 0)
                {
                    var p = 1L;
                    for (var k = 0; k < digits; k++) p *= 10;
                    whole += Math.Truncate(r * p) / p;
                }
                res = whole;
            }
        }
        else
        {
            // evlRound: a scaled int64 (MOV_get_int64 rounding: +0.5 + epsilon), shown again as a double
            var n = Cvt.DoubleToExact(x, (int)digits, TypeKind.BigInt, v.IsFloat);
            res = digits >= 0 ? n / Cvt.PowerOfTen((int)digits) : n * Cvt.PowerOfTen((int)-digits);
        }
        return D(res);
    }

    /// <summary>makeBin: integral arguments only; INTEGER unless an argument is wider.</summary>
    static SqlType BinType(XNode[] a, string name)
    {
        foreach (var x in a)
            if (x.Type.Kind != TypeKind.Null && (!x.Type.IsExact || x.Type.Scale != 0))
                throw Sysf(FbMessages.sysf_argmustbe_exact, name);
        if (a.Any(x => x.Type.Kind == TypeKind.Int128)) return SqlType.Int128;
        return a.All(x => x.Type.Kind is TypeKind.SmallInt or TypeKind.Integer or TypeKind.Null) ? SqlType.Integer : SqlType.BigInt;
    }

    static SqlType CeilFloorType(XNode[] a) => a[0].Type.Kind switch
    {
        TypeKind.Null or TypeKind.SmallInt => SqlType.Integer,
        TypeKind.Integer or TypeKind.BigInt => SqlType.BigInt,
        _ => SqlType.Double,
    };

    /// <summary>makeRound / makeTrunc: exact (and for ROUND, approximate) values keep their type; one argument drops the scale.</summary>
    static SqlType RoundType(XNode[] a, bool round)
    {
        var t = a[0].Type;
        if (a.Any(x => x.Type.Kind == TypeKind.Null)) return SqlType.Integer;
        if (t.IsExact) return a.Length == 1 ? TypeRules.Exact(t.Kind, 0, t.SubType) : t;
        if (t.IsApprox) return round ? t : SqlType.Double;
        if (round)
            throw new StatusBuilder().Gds(Err.isc_expression_eval_err).Gds(FbMessages.sysf_argmustbe_exact_or_fp).Str("ROUND").Build();
        return SqlType.Double;
    }

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
        var result = Coercion.ToType(best, f.Type);
        if (f.Type.IsString && result.Tag == VTag.String)
        {
            // evlMaxMinValue redoes makeFromList on the runtime descriptors: a multibyte literal's descriptor holds
            // its byte length, which converts to bytes / max-bytes-per-char characters
            var len = a.Max(n => n is ConstNode { Literal: not null, Type.IsString: true } k
                ? Conv.Utf8.GetByteCount(k.Value.AsString) / (k.Type.Charset == Charset.Utf8 ? 4 : k.Type.Charset == Charset.UnicodeFss ? 3 : 1)
                : TypeRules.StringLength(n.Type));
            var text = result.AsString;
            var chars = text.EnumerateRunes().Count();
            if (chars > len && text.EnumerateRunes().Skip(len).Any(r => r.Value != ' '))
                throw Err.StringTruncation(len, chars);
        }
        return result;
    }

    /// <summary>Common result type for CASE/COALESCE/UNION branches (Firebird's makeFromList).</summary>
    public static SqlType CommonType(IEnumerable<XNode> nodes, string name = "") => TypeRules.FromList(nodes, name).Type;

    public static SqlType CommonType(IEnumerable<SqlType> types, string name = "")
        => TypeRules.FromList(types.Select(t => ((SqlType?)t, false)).ToList(), name).Type;
}
