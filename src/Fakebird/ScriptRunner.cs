using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Fakebird.Engine;
using Fakebird.Types;

namespace Fakebird;

/// <summary>isql-like script execution (SET TERM aware statement splitting).</summary>
public static partial class ScriptRunner
{
    [GeneratedRegex(@"^\s*SET\s+TERM\s+(\S+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex SetTerm();

    [GeneratedRegex(@"^\s*(SET\s+(SQL\s+DIALECT|NAMES|AUTODDL|ECHO|LIST|STATS|PLAN|COUNT|WARNINGS|BAIL|HEADING|BLOB|ROWCOUNT|TIME|WIDTH|SQLDA_DISPLAY|KEEP_TRAN_PARAMS|TRANSACTION)|CONNECT\s|CREATE\s+DATABASE|INPUT\s|OUTPUT\b|SHOW\s|QUIT\b|EXIT\b)", RegexOptions.IgnoreCase)]
    private static partial Regex ClientCommand();

    /// <summary>Splits a script into statements, honouring quotes, comments and SET TERM.</summary>
    public static List<string> Split(string script)
    {
        var result = new List<string>();
        var term = ";";
        var sb = new StringBuilder();
        var i = 0;
        while (i < script.Length)
        {
            var ch = script[i];
            if (ch == '\'' || ch == '"')
            {
                var end = i + 1;
                while (end < script.Length)
                {
                    if (script[end] == ch)
                    {
                        if (end + 1 < script.Length && script[end + 1] == ch) { end += 2; continue; }
                        break;
                    }
                    end++;
                }
                sb.Append(script, i, Math.Min(end + 1, script.Length) - i);
                i = end + 1;
                continue;
            }
            if (ch == '-' && i + 1 < script.Length && script[i + 1] == '-')
            {
                var end = script.IndexOf('\n', i);
                if (end < 0) end = script.Length;
                sb.Append(script, i, end - i);
                i = end;
                continue;
            }
            if (ch == '/' && i + 1 < script.Length && script[i + 1] == '*')
            {
                var end = script.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? script.Length : end + 2;
                sb.Append(script, i, end - i);
                i = end;
                continue;
            }
            if (string.CompareOrdinal(script, i, term, 0, term.Length) == 0)
            {
                var stmt = sb.ToString().Trim();
                sb.Clear();
                i += term.Length;
                var m = SetTerm().Match(StripComments(stmt));
                if (m.Success)
                {
                    term = m.Groups[1].Value;
                    continue;
                }
                if (StripComments(stmt).Trim().Length > 0) result.Add(stmt);
                continue;
            }
            sb.Append(ch);
            i++;
        }
        var last = sb.ToString().Trim();
        if (StripComments(last).Trim().Length > 0)
        {
            var m = SetTerm().Match(StripComments(last));
            if (!m.Success) result.Add(last);
        }
        return result;
    }

    static string StripComments(string s)
    {
        var sb = new StringBuilder();
        var i = 0;
        while (i < s.Length)
        {
            if (s[i] == '-' && i + 1 < s.Length && s[i + 1] == '-')
            {
                var e = s.IndexOf('\n', i);
                i = e < 0 ? s.Length : e;
                continue;
            }
            if (s[i] == '/' && i + 1 < s.Length && s[i + 1] == '*')
            {
                var e = s.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = e < 0 ? s.Length : e + 2;
                continue;
            }
            sb.Append(s[i++]);
        }
        return sb.ToString();
    }

    public static void Run(Database db, string script)
    {
        var att = new Attachment(db);
        try
        {
            foreach (var stmt in Split(script))
            {
                var bare = StripComments(stmt).Trim();
                if (ClientCommand().IsMatch(bare)) continue;
                var upper = bare.ToUpperInvariant();
                if (upper is "COMMIT" or "COMMIT WORK" or "ROLLBACK" or "ROLLBACK WORK") continue;
                lock (db.Sync)
                {
                    var tx = att.StartTransaction();
                    try
                    {
                        Executor.Execute(tx, stmt);
                        if (tx.Active) tx.Commit();
                    }
                    catch (FbError e)
                    {
                        if (tx.Active) tx.Rollback();
                        throw new FbScriptException(stmt, e);
                    }
                }
            }
        }
        finally
        {
            lock (db.Sync) att.Detach();
        }
    }

    public static List<object?[]> Execute(Database db, string sql, object?[] args)
    {
        lock (db.Sync)
        {
            var att = new Attachment(db);
            var tx = att.StartTransaction();
            try
            {
                var r = Executor.Execute(tx, sql, args.Select(ToValue).ToArray());
                if (tx.Active) tx.Commit();
                return (r.Rows ?? []).Select(row => row.Select(v => v.ToClr()).ToArray()).ToList();
            }
            finally
            {
                if (tx.Active) tx.Rollback();
                att.Detach();
            }
        }
    }

    internal static Value ToValue(object? o) => o switch
    {
        null or DBNull => Value.Null,
        bool b => Value.Bool(b),
        short s => Value.Int(s),
        int i => Value.Int(i),
        long l => Value.Int(l),
        float f => Value.Dbl(f),
        double d => Value.Dbl(d),
        decimal m => Conv.ParseNumber(m.ToString(CultureInfo.InvariantCulture)),
        string s => Value.Str(s),
        byte[] b => Value.Bytes(b),
        Guid g => Value.Bytes(g.ToByteArray(bigEndian: true)),
        DateTime dt => Value.Timestamp(Conv.DateTimeToTimestamp(dt)),
        DateOnly d => Value.Date(Conv.DateToDay(d)),
        TimeSpan ts => Value.Time(ts.Ticks / 1000),
        TimeOnly t => Value.Time(t.Ticks / 1000),
        _ => throw new ArgumentException("Unsupported parameter type " + o.GetType().Name),
    };
}

/// <summary>A script statement failed.</summary>
public sealed class FbScriptException(string statement, FbError inner)
    : Exception($"{inner.Message}{Environment.NewLine}Statement:{Environment.NewLine}{statement}", inner)
{
    public string Statement { get; } = statement;
    public FbError Error { get; } = inner;
}
