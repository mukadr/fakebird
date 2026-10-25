using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Fakebird.Sql;
using Fakebird.Types;

namespace Fakebird.Exec;

/// <summary>LIKE / STARTING WITH / CONTAINING / SIMILAR TO.</summary>
public sealed class LikeNode : XNode
{
    public readonly LikeKind Kind;
    public readonly XNode X, Pattern;
    public readonly XNode? Escape;
    public readonly bool Not;
    public readonly Collation Collation;
    Regex? _cachedRegex;
    string? _cachedPattern;
    string? _cachedEscape;

    public LikeNode(LikeKind kind, XNode x, XNode pattern, XNode? escape, bool not, Collation collation)
    {
        Kind = kind;
        X = x;
        Pattern = pattern;
        Escape = escape;
        Not = not;
        Collation = collation;
        Type = SqlType.Boolean;
    }

    public override Value Eval(Ctx c)
    {
        var v = X.Eval(c);
        if (v.IsNull) return Value.Null;
        var p = Pattern.Eval(c);
        if (p.IsNull) return Value.Null;
        string? esc = null;
        if (Escape != null && Kind is LikeKind.Like or LikeKind.SimilarTo)
        {
            var e = Escape.Eval(c);
            if (e.IsNull) return Value.Null;
            esc = Conv.ToStr(e);
        }
        if (v.Tag == VTag.Bytes)
            return Value.Bool(Binary(v.AsBytes, p.Tag == VTag.Bytes ? p.AsBytes : Conv.Utf8.GetBytes(Conv.ToStr(p)), esc) != Not);
        var s = Conv.ToStr(v);
        var pat = Conv.ToStr(p);
        bool r;
        switch (Kind)
        {
            case LikeKind.StartingWith:
                r = Folding.Fold(s, Collation).StartsWith(Folding.Fold(pat, Collation), StringComparison.Ordinal);
                break;
            case LikeKind.Containing:
            {
                // CONTAINING is case-insensitive even for binary collations
                var col = Collation is Collation.Binary or Collation.Unicode or Collation.Win1252 ? Collation.CaseInsensitive : Collation;
                r = Folding.Fold(s, col).Contains(Folding.Fold(pat, col), StringComparison.Ordinal);
                break;
            }
            case LikeKind.Like:
            {
                int? escCp = null;
                if (esc != null)
                {
                    var runes = Folding.CodePoints(esc);
                    if (runes.Length != 1) throw new StatusBuilder().Gds(FbMessages.escape_invalid).Build();
                    escCp = runes[0];
                }
                r = Like(Folding.CodePoints(Folding.Fold(s, Collation)), Folding.CodePoints(Folding.Fold(pat, Collation)), escCp);
                break;
            }
            default:
                r = SimilarRegex(Folding.Fold(pat, Collation), esc).IsMatch(Folding.Fold(s, Collation));
                break;
        }
        return Value.Bool(r != Not);
    }

    /// <summary>OCTETS: STARTING and CONTAINING compare bytes; LIKE has no wildcards in this character set.</summary>
    bool Binary(byte[] s, byte[] p, string? esc)
    {
        switch (Kind)
        {
            case LikeKind.StartingWith: return s.AsSpan().StartsWith(p);
            case LikeKind.Containing: return s.AsSpan().IndexOf(p) >= 0;
            case LikeKind.Like: return s.AsSpan().SequenceEqual(p);
            default:
                return SimilarRegex(System.Text.Encoding.Latin1.GetString(p), esc).IsMatch(System.Text.Encoding.Latin1.GetString(s));
        }
    }

    /// <summary>SQL LIKE over code points with '%' and '_' wildcards; the escape character must precede a wildcard or itself.</summary>
    public static bool Like(int[] s, int[] p, int? esc)
    {
        // validate the pattern first, like Firebird's LikeEvaluator
        if (esc != null)
            for (var i = 0; i < p.Length; i++)
                if (p[i] == esc)
                {
                    if (i + 1 >= p.Length || (p[i + 1] != '%' && p[i + 1] != '_' && p[i + 1] != esc))
                        throw new StatusBuilder().Gds(FbMessages.escape_invalid).Build();
                    i++;
                }
        int si = 0, pi = 0, starP = -1, starS = 0;
        while (si < s.Length)
        {
            if (pi < p.Length)
            {
                var pc = p[pi];
                if (esc != null && pc == esc)
                {
                    if (s[si] == p[pi + 1])
                    {
                        si++;
                        pi += 2;
                        continue;
                    }
                }
                else if (pc == '%')
                {
                    starP = ++pi;
                    starS = si;
                    continue;
                }
                else if (pc == '_' || pc == s[si])
                {
                    si++;
                    pi++;
                    continue;
                }
            }
            if (starP < 0) return false;
            pi = starP;
            si = ++starS;
        }
        while (pi < p.Length && p[pi] == '%') pi++;
        return pi == p.Length;
    }

    public static bool Like(string s, string p, char? esc)
        => Like(Folding.CodePoints(s), Folding.CodePoints(p), esc);

    Regex SimilarRegex(string pattern, string? escape)
    {
        if (_cachedRegex != null && _cachedPattern == pattern && _cachedEscape == escape) return _cachedRegex;
        var ci = Collation is Collation.CaseInsensitive or Collation.CaseAccentInsensitive;
        _cachedRegex = SimilarTo.Compile(pattern, escape, ci);
        _cachedPattern = pattern;
        _cachedEscape = escape;
        return _cachedRegex;
    }
}

/// <summary>Collation-dependent canonical forms used by LIKE, STARTING, CONTAINING, POSITION and REPLACE.</summary>
public static class Folding
{
    /// <summary>Case folding for _CI collations and case + accent folding for _CI_AI ones (one code point each).</summary>
    public static string Fold(string s, Collation c)
    {
        switch (c)
        {
            case Collation.CaseInsensitive:
                return CaseMap.Upper(s);
            case Collation.CaseAccentInsensitive:
            {
                var sb = new StringBuilder(s.Length);
                foreach (var r in s.EnumerateRunes())
                {
                    var d = r.ToString().Normalize(NormalizationForm.FormD);
                    var kept = false;
                    foreach (var dr in d.EnumerateRunes())
                    {
                        if (kept && Rune.GetUnicodeCategory(dr) == UnicodeCategory.NonSpacingMark) continue;
                        if (!kept) { sb.Append(CaseMap.Upper(dr.ToString())); kept = true; }
                    }
                }
                return sb.ToString();
            }
            default:
                return s;
        }
    }

    public static int[] CodePoints(string s)
    {
        var list = new List<int>(s.Length);
        foreach (var r in s.EnumerateRunes()) list.Add(r.Value);
        return list.ToArray();
    }
}

/// <summary>Unicode simple case mapping (ICU's u_toupper / u_tolower per code point), as Firebird's UPPER/LOWER.</summary>
public static class CaseMap
{
    // cached: CultureInfo.GetCultureInfo takes a global lock on every call
    static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    public static string Upper(string s)
    {
        if (Ascii.IsValid(s))
        {
            var first = s.AsSpan().IndexOfAnyInRange('a', 'z');
            if (first < 0) return s;
            return string.Create(s.Length, (s, first), static (span, st) =>
            {
                st.s.AsSpan(0, st.first).CopyTo(span);
                Ascii.ToUpper(st.s.AsSpan(st.first), span[st.first..], out _);
            });
        }
        StringBuilder? sb = null;
        var i = 0;
        foreach (var r in s.EnumerateRunes())
        {
            var u = Rune.ToUpper(r, En);
            if (r.Value == 'ı') u = new Rune('I'); // dotless i: simple mapping to I
            if (u != r && sb == null) sb = new StringBuilder(s, 0, i, s.Length);
            sb?.Append(u.ToString());
            i += r.Utf16SequenceLength;
        }
        return sb?.ToString() ?? s;
    }

    public static string Lower(string s)
    {
        if (Ascii.IsValid(s))
        {
            var first = s.AsSpan().IndexOfAnyInRange('A', 'Z');
            if (first < 0) return s;
            return string.Create(s.Length, (s, first), static (span, st) =>
            {
                st.s.AsSpan(0, st.first).CopyTo(span);
                Ascii.ToLower(st.s.AsSpan(st.first), span[st.first..], out _);
            });
        }
        StringBuilder? sb = null;
        var i = 0;
        foreach (var r in s.EnumerateRunes())
        {
            var l = Rune.ToLower(r, En);
            if (r.Value == 'İ') l = new Rune('i'); // dotted capital I: simple mapping to i
            if (l != r && sb == null) sb = new StringBuilder(s, 0, i, s.Length);
            sb?.Append(l.ToString());
            i += r.Utf16SequenceLength;
        }
        return sb?.ToString() ?? s;
    }
}

/// <summary>
/// SIMILAR TO patterns translated to .NET regular expressions like Firebird's SimilarToCompiler (which produces
/// RE2 syntax), with the same validation and errors.
/// </summary>
public static class SimilarTo
{
    static FbError Invalid() => new StatusBuilder().Gds(FbMessages.invalid_similar_pattern).Build();
    static FbError EscapeInvalid() => new StatusBuilder().Gds(FbMessages.escape_invalid).Build();

    public static bool IsSpecialChar(int c) => c is '^' or '-' or '_' or '%' or '[' or ']' or '(' or ')' or '{' or '}' or '|' or '?' or '+' or '*';

    static bool IsRe2Special(int c) => c is '\\' or '$' or '.' or '^' or '-' or '_' or '[' or ']' or '(' or ')' or '{' or '}' or '|' or '?' or '+' or '*';

    const int NotEmpty = 1;

    // RE2's POSIX classes are ASCII only
    static readonly (string Name, string Include, string Exclude)[] Classes =
    [
        ("alnum", "0-9A-Za-z", @"\u0000-/:-@\[-`{-￿"),
        ("alpha", "A-Za-z", @"\u0000-@\[-`{-￿"),
        ("digit", "0-9", @"\u0000-/:-￿"),
        ("lower", "a-z", @"\u0000-`{-￿"),
        ("space", " ", @"\u0000-\u001F!-￿"),
        ("upper", "A-Z", @"\u0000-@\[-￿"),
        ("whitespace", @"\t\n\v\f\r ", @"\u0000-\u0008\u000E-\u001F!-￿"),
    ];

    sealed class Compiler(int[] pattern, int? escape, bool preferFewer = false)
    {
        readonly StringBuilder _re = new();
        int _pos;

        public string Result => _re.ToString();
        bool Has => _pos < pattern.Length;
        int Get() => Has ? pattern[_pos++] : throw Invalid();
        int Peek() => Has ? pattern[_pos] : throw Invalid();

        static string Lit(int c) => c is '\t' or '\n' or '\r' ? char.ConvertFromUtf32(c) : char.ConvertFromUtf32(c);

        void AppendChar(int c)
        {
            if (IsRe2Special(c)) _re.Append('\\');
            else if (c is '\t' or '\n' or '\r' or ' ' or '#') { _re.Append('\\'); }
            _re.Append(c == '\t' ? "t" : c == '\n' ? "n" : c == '\r' ? "r" : Lit(c));
        }

        public void Run()
        {
            ParseExpr(out _);
            if (Has) throw Invalid();
        }

        void ParseExpr(out int flagsOut)
        {
            flagsOut = 0;
            while (true)
            {
                ParseTerm(out var f);
                flagsOut &= ~(~f & NotEmpty);
                flagsOut |= f;
                if (!Has || pattern[_pos] != '|') break;
                _pos++;
                _re.Append('|');
            }
        }

        void ParseTerm(out int flagsOut)
        {
            flagsOut = 0;
            var first = true;
            while (Has)
            {
                var c = Peek();
                if (c == '|' || c == ')') break;
                ParseFactor(out var f);
                flagsOut |= f & NotEmpty;
                if (first)
                {
                    flagsOut |= f;
                    first = false;
                }
            }
        }

        static bool IsRep(int c) => c is '*' or '+' or '?' or '{';

        void ParseFactor(out int flagsOut)
        {
            ParsePrimary(out var f);
            if (!Has || !IsRep(Peek()))
            {
                flagsOut = f;
                return;
            }
            var op = Peek();
            if ((f & NotEmpty) == 0 && op != '?') throw Invalid();
            switch (op)
            {
                case '*': _re.Append(preferFewer ? "*?" : "*"); flagsOut = 0; _pos++; break;
                case '+': _re.Append(preferFewer ? "+?" : "+"); flagsOut = NotEmpty; _pos++; break;
                case '?': _re.Append(preferFewer ? "??" : "?"); flagsOut = 0; _pos++; break;
                default:
                {
                    var start = _pos++;
                    var comma = false;
                    var s1 = new StringBuilder();
                    while (true)
                    {
                        if (!Has) throw Invalid();
                        var c = Get();
                        if (c == '}')
                        {
                            if (s1.Length == 0) throw Invalid();
                            break;
                        }
                        if (c == ',')
                        {
                            if (comma) throw Invalid();
                            comma = true;
                        }
                        else if (c is >= '0' and <= '9')
                        {
                            if (!comma) s1.Append((char)c);
                        }
                        else throw Invalid();
                    }
                    flagsOut = int.Parse(s1.ToString(), CultureInfo.InvariantCulture) == 0 ? 0 : NotEmpty;
                    for (var i = start; i < _pos; i++) _re.Append(char.ConvertFromUtf32(pattern[i]));
                    if (preferFewer) _re.Append('?');
                    break;
                }
            }
            if (Has && IsRep(Peek())) throw Invalid();
        }

        int EscapedChar()
        {
            if (!Has) throw EscapeInvalid();
            var c = Get();
            if (!(c == escape || IsSpecialChar(c))) throw EscapeInvalid();
            return c;
        }

        void ParsePrimary(out int flagsOut)
        {
            flagsOut = 0;
            var save = _pos;
            var op = Get();
            if (op == '_')
            {
                flagsOut |= NotEmpty;
                // one code point (a surrogate pair is one character for Firebird)
                _re.Append(@"(?:[\uD800-\uDBFF][\uDC00-\uDFFF]|.)");
                return;
            }
            if (op == '%')
            {
                _re.Append(preferFewer ? ".*?" : ".*");
                return;
            }
            if (op == '[')
            {
                ParseClass();
                flagsOut |= NotEmpty;
                return;
            }
            if (op == '(')
            {
                _re.Append("(?:");
                ParseExpr(out var f);
                if (!Has || Get() != ')') throw Invalid();
                _re.Append(')');
                flagsOut |= f & NotEmpty;
                return;
            }
            _pos = save;
            var control = false;
            do
            {
                var charSave = _pos;
                op = Get();
                if (escape != null && op == escape)
                {
                    op = Get();
                    if (!IsSpecialChar(op) && op != escape) throw EscapeInvalid();
                }
                else if (IsSpecialChar(op))
                {
                    control = true;
                    _pos = charSave;
                }
                if (!control) AppendChar(op);
            } while (!control && Has);
            if (_pos == save) throw Invalid();
            flagsOut |= NotEmpty;
        }

        void ParseClass()
        {
            var items = new List<(int Clazz, int First, int Last)>();
            var includeCount = 0;
            var exclude = false;
            var invalidInclude = false;
            do
            {
                if (!Has) throw Invalid();
                var c = Get();
                var charClass = false;
                if (escape != null && c == escape) c = EscapedChar();
                else if (c == '[') charClass = true;
                else if (c == '^')
                {
                    if (exclude) throw Invalid();
                    exclude = true;
                    continue;
                }
                (int Clazz, int First, int Last) item;
                var strip = false;
                if (charClass)
                {
                    if (!Has || Get() != ':') throw Invalid();
                    var nameStart = _pos;
                    while (Has && Get() != ':') { }
                    var name = string.Concat(pattern[nameStart..(_pos - 1)].Select(char.ConvertFromUtf32));
                    if (!Has || Get() != ']') throw Invalid();
                    var idx = Array.FindIndex(Classes, k => k.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (idx < 0) throw Invalid();
                    item = (idx, 0, 0);
                }
                else
                {
                    item = (-1, c, c);
                    if (Has && Peek() == '-')
                    {
                        Get();
                        var c2 = Get();
                        if (escape != null && c2 == escape) c2 = EscapedChar();
                        item = (-1, c, c2);
                        strip = c > c2;
                    }
                }
                if (strip)
                {
                    if (!exclude) invalidInclude = true;
                }
                else
                {
                    if (!exclude) includeCount++;
                    items.Add(item);
                }
                if (!Has) throw Invalid();
            } while (Peek() != ']');

            exclude = includeCount < items.Count;

            void AppendItem((int Clazz, int First, int Last) it, bool negated)
            {
                if (it.Clazz >= 0)
                {
                    _re.Append(negated ? Classes[it.Clazz].Exclude : Classes[it.Clazz].Include);
                    return;
                }
                if (negated)
                {
                    if (it.First > 0) _re.Append(@"\u0000-").Append(Hex(it.First - 1));
                    if (it.Last < 0xFFFF) _re.Append(Hex(it.Last + 1)).Append(@"-￿");
                    return;
                }
                _re.Append(Hex(it.First));
                if (it.Last != it.First) _re.Append('-').Append(Hex(it.Last));
            }

            if (exclude && includeCount > 1)
            {
                _re.Append("(?:");
                for (var i = 0; i < includeCount; i++)
                {
                    if (i != 0) _re.Append('|');
                    _re.Append("[^");
                    AppendItem(items[i], true);
                    for (var j = includeCount; j < items.Count; j++) AppendItem(items[j], false);
                    _re.Append(']');
                }
                _re.Append(')');
            }
            else if (items.Count > 0 && !(invalidInclude && includeCount == 0))
            {
                _re.Append('[');
                if (exclude) _re.Append('^');
                for (var i = 0; i < items.Count; i++) AppendItem(items[i], exclude && i < includeCount);
                _re.Append(']');
            }
            else if (invalidInclude) _re.Append(@"[^\u0000-￿]");
            else _re.Append('.');
            Get(); // ']'
        }

        static string Hex(int c) => c <= 0xFFFF ? $"\\u{c:X4}" : char.ConvertFromUtf32(c);
    }

    /// <summary>
    /// SUBSTRING ... SIMILAR (SubstringSimilarCompiler): the pattern has exactly two escape-quote markers; the parts
    /// before and after them match as little as possible and the middle one is the result (group 1).
    /// </summary>
    public static Regex CompileSubstring(string pattern, string escape, bool caseInsensitive)
    {
        var e = Folding.CodePoints(escape);
        if (e.Length != 1) throw EscapeInvalid();
        var esc = e[0];
        var p = Folding.CodePoints(pattern);
        var marks = new List<int>();
        for (var i = 0; i < p.Length; i++)
        {
            if (p[i] != esc) continue;
            if (i + 1 >= p.Length) throw Invalid();
            if (p[++i] == '"')
            {
                if (marks.Count >= 2) throw Invalid();
                marks.Add(i + 1);
            }
        }
        if (marks.Count != 2) throw Invalid();
        string Part(int from, int to, bool fewer)
        {
            var comp = new Compiler(p[from..to], esc, fewer);
            comp.Run();
            return comp.Result;
        }
        var re = "^(?:" + Part(0, marks[0] - 2, true) + ")(" + Part(marks[0], marks[1] - 2, false) + ")(?:" + Part(marks[1], p.Length, true) + @")\z";
        var opts = RegexOptions.Singleline | RegexOptions.CultureInvariant | (caseInsensitive ? RegexOptions.IgnoreCase : 0);
        try { return new Regex(re, opts); }
        catch (ArgumentException) { throw Invalid(); }
    }

    public static Regex Compile(string pattern, string? escape, bool caseInsensitive)
    {
        int? esc = null;
        if (escape != null)
        {
            var e = Folding.CodePoints(escape);
            if (e.Length != 1) throw EscapeInvalid();
            esc = e[0];
        }
        var comp = new Compiler(Folding.CodePoints(pattern), esc);
        comp.Run();
        var opts = RegexOptions.Singleline | RegexOptions.CultureInvariant | (caseInsensitive ? RegexOptions.IgnoreCase : 0);
        try
        {
            return new Regex("^(?:" + comp.Result + @")\z", opts);
        }
        catch (ArgumentException)
        {
            throw Invalid();
        }
    }
}
