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
    readonly bool _ci;
    Regex? _cachedRegex;
    string? _cachedPattern;

    public LikeNode(LikeKind kind, XNode x, XNode pattern, XNode? escape, bool not, Collation collation)
    {
        Kind = kind;
        X = x;
        Pattern = pattern;
        Escape = escape;
        Not = not;
        Collation = collation;
        _ci = collation != Collation.Binary;
        Type = SqlType.Boolean;
    }

    public override Value Eval(Ctx c)
    {
        var v = X.Eval(c);
        if (v.IsNull) return Value.Null;
        var p = Pattern.Eval(c);
        if (p.IsNull) return Value.Null;
        var s = Conv.ToStr(v);
        var pat = Conv.ToStr(p);
        bool r;
        switch (Kind)
        {
            case LikeKind.StartingWith:
                r = _ci ? s.StartsWith(pat, StringComparison.OrdinalIgnoreCase) : s.StartsWith(pat, StringComparison.Ordinal);
                break;
            case LikeKind.Containing:
                r = s.Contains(pat, StringComparison.OrdinalIgnoreCase);
                break;
            case LikeKind.Like:
            {
                char? esc = null;
                if (Escape != null)
                {
                    var e = Escape.Eval(c);
                    if (e.IsNull) return Value.Null;
                    var es = Conv.ToStr(e);
                    if (es.Length != 1) throw Err.Random("Invalid ESCAPE sequence");
                    esc = es[0];
                }
                if (_ci)
                {
                    s = s.ToUpperInvariant();
                    pat = pat.ToUpperInvariant();
                }
                r = Like(s, pat, esc);
                break;
            }
            default:
                r = SimilarRegex(c, pat).IsMatch(s);
                break;
        }
        return Value.Bool(r != Not);
    }

    /// <summary>SQL LIKE with '%' and '_' wildcards (iterative with single backtrack point).</summary>
    public static bool Like(string s, string p, char? esc)
    {
        int si = 0, pi = 0, starP = -1, starS = 0;
        while (si < s.Length)
        {
            if (pi < p.Length)
            {
                var pc = p[pi];
                if (esc != null && pc == esc)
                {
                    if (pi + 1 >= p.Length) throw Err.Random("Invalid ESCAPE sequence");
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
        while (pi < p.Length && p[pi] == '%' && (esc == null || p[pi] != esc)) pi++;
        return pi == p.Length;
    }

    Regex SimilarRegex(Ctx c, string pattern)
    {
        if (_cachedRegex != null && _cachedPattern == pattern) return _cachedRegex;
        char? esc = null;
        if (Escape != null)
        {
            var e = Escape.Eval(c);
            if (!e.IsNull) esc = Conv.ToStr(e)[0];
        }
        var sb = new StringBuilder("^(?:");
        for (var i = 0; i < pattern.Length; i++)
        {
            var ch = pattern[i];
            if (esc != null && ch == esc && i + 1 < pattern.Length)
            {
                sb.Append(Regex.Escape(pattern[++i].ToString()));
                continue;
            }
            switch (ch)
            {
                case '%': sb.Append(".*"); break;
                case '_': sb.Append('.'); break;
                case '|':
                case '*':
                case '+':
                case '?':
                case '(':
                case ')':
                case '{':
                case '}':
                    sb.Append(ch);
                    break;
                case '[':
                {
                    var end = pattern.IndexOf(']', i + 1);
                    if (end < 0) throw Err.Random("Invalid SIMILAR TO pattern");
                    var cls = pattern[(i + 1)..end];
                    cls = cls.Replace("[:ALPHA:]", "a-zA-Z").Replace("[:DIGIT:]", "0-9").Replace("[:ALNUM:]", "a-zA-Z0-9")
                        .Replace("[:UPPER:]", "A-Z").Replace("[:LOWER:]", "a-z").Replace("[:SPACE:]", " ").Replace("[:WHITESPACE:]", "\\s");
                    if (cls.Contains("^") && !cls.StartsWith('^'))
                    {
                        var parts = cls.Split('^', 2);
                        sb.Append("(?:(?![").Append(parts[1]).Append("])[").Append(parts[0]).Append("])");
                    }
                    else sb.Append('[').Append(cls).Append(']');
                    i = end;
                    break;
                }
                default:
                    sb.Append(Regex.Escape(ch.ToString()));
                    break;
            }
        }
        sb.Append(")$");
        var opts = RegexOptions.Singleline | RegexOptions.CultureInvariant | (_ci ? RegexOptions.IgnoreCase : 0);
        _cachedRegex = new Regex(sb.ToString(), opts);
        _cachedPattern = pattern;
        return _cachedRegex;
    }
}
