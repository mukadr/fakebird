using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FirebirdSql.Data.FirebirdClient;

namespace Fakebird.Tests.Corpus;

/// <summary>One step of a corpus file: a SQL case or a directive (connection / transaction control).</summary>
public sealed record CorpusStep(string Kind, string Text, string? Params = null, int Line = 0)
{
    public string Header => Kind == "case" ? Text.Split('\n')[0].Trim() : $"@{Kind} {Text}".Trim();
}

/// <summary>
/// A corpus file (<c>Corpus/*.sql</c>): an optional <c>-- @setup</c> script followed by cases. Directives:
/// <c>-- @case [params: [json array]]</c>, <c>-- @conn NAME</c>, <c>-- @tx options</c>, <c>-- @commit</c>,
/// <c>-- @rollback</c>, <c>-- @mode native|compat3</c>.
/// </summary>
public sealed partial class CorpusFile
{
    public string Name { get; init; } = "";
    public string Setup { get; set; } = "";
    public List<CorpusStep> Steps { get; } = [];
    public HashSet<string> Modes { get; } = [];

    [GeneratedRegex(@"^--\s*@(\w+)\s*(.*)$")]
    private static partial Regex Directive();

    public static CorpusFile Parse(string name, string text)
    {
        var lines = text.Replace("\r", "").Split('\n');
        var setup = new StringBuilder();
        var file = new CorpusFile { Name = name };
        string? section = null;
        string? caseParams = null;
        var caseLine = 0;
        var body = new StringBuilder();

        void Flush()
        {
            if (section == "case")
            {
                var caseLines = body.ToString().TrimEnd().Split('\n').ToList();
                while (caseLines.Count > 0 && (caseLines[^1].Trim().Length == 0 || caseLines[^1].TrimStart().StartsWith("--")))
                    caseLines.RemoveAt(caseLines.Count - 1);
                var sql = string.Join("\n", caseLines).Trim();
                if (sql.EndsWith(';')) sql = sql[..^1].TrimEnd();
                if (sql.Length == 0) throw new InvalidOperationException($"{name}:{caseLine}: empty case");
                file.Steps.Add(new CorpusStep("case", sql, caseParams, caseLine));
            }
            body.Clear();
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var m = Directive().Match(lines[i].Trim());
            if (!m.Success)
            {
                if (section == "setup") setup.AppendLine(lines[i]);
                else if (section == "case") body.AppendLine(lines[i]);
                continue;
            }
            Flush();
            var kind = m.Groups[1].Value.ToLowerInvariant();
            var arg = m.Groups[2].Value.Trim();
            switch (kind)
            {
                case "setup":
                    section = "setup";
                    break;
                case "case":
                    section = "case";
                    caseLine = i + 1;
                    caseParams = arg.StartsWith("params:", StringComparison.OrdinalIgnoreCase) ? arg[7..].Trim() : null;
                    break;
                case "mode":
                    foreach (var mode in arg.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)) file.Modes.Add(mode);
                    section = null;
                    break;
                case "conn" or "tx" or "commit" or "rollback":
                    file.Steps.Add(new CorpusStep(kind, arg, Line: i + 1));
                    section = null;
                    break;
                default:
                    throw new InvalidOperationException($"{name}:{i + 1}: unknown directive @{kind}");
            }
        }
        Flush();
        file.Setup = setup.ToString();
        return file;
    }
}

/// <summary>Runs a corpus file against one server (real or fake) and describes every case's observable result.</summary>
public static partial class CorpusRunner
{
    public static void RunSetup(string cs, string script)
    {
        using var cn = new FbConnection(cs);
        cn.Open();
        foreach (var stmt in ScriptRunner.Split(script))
        {
            using var cmd = new FbCommand(stmt, cn);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>Returns one description per step (directives describe to their outcome, usually empty).</summary>
    public static List<string> Run(string cs, CorpusFile file)
    {
        var conns = new Dictionary<string, (FbConnection Cn, FbTransaction? Tx)>();
        var current = "A";
        var results = new List<string>();
        try
        {
            FbTransaction Tx(string? options = null)
            {
                if (!conns.TryGetValue(current, out var c))
                {
                    var cn = new FbConnection(cs);
                    cn.Open();
                    c = (cn, null);
                }
                if (c.Tx == null || options != null)
                    c.Tx = options == null ? c.Cn.BeginTransaction() : c.Cn.BeginTransaction(TxOptions(options));
                conns[current] = c;
                return c.Tx;
            }

            void End(bool commit)
            {
                if (!conns.TryGetValue(current, out var c) || c.Tx == null) return;
                if (commit) c.Tx.Commit(); else c.Tx.Rollback();
                c.Tx.Dispose();
                conns[current] = (c.Cn, null);
            }

            foreach (var step in file.Steps)
            {
                try
                {
                    switch (step.Kind)
                    {
                        case "conn":
                            current = step.Text.Length == 0 ? "A" : step.Text;
                            results.Add("");
                            break;
                        case "tx":
                            End(commit: false);
                            Tx(step.Text);
                            results.Add("");
                            break;
                        case "commit" or "rollback":
                            End(step.Kind == "commit");
                            results.Add("");
                            break;
                        default:
                            var tx = Tx();
                            results.Add(Describe(conns[current].Cn, tx, step.Text, step.Params));
                            break;
                    }
                }
                catch (FbException e)
                {
                    results.Add(DescribeError(e));
                }
            }
        }
        finally
        {
            foreach (var (cn, tx) in conns.Values)
            {
                try { tx?.Rollback(); } catch { /* the connection may already be broken */ }
                cn.Dispose();
            }
        }
        return results;
    }

    static FbTransactionOptions TxOptions(string text)
    {
        var words = text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        FbTransactionBehavior b = 0;
        TimeSpan? timeout = null;
        for (var i = 0; i < words.Length; i++)
        {
            b |= words[i] switch
            {
                "snapshot" or "concurrency" => FbTransactionBehavior.Concurrency,
                "consistency" or "table_stability" => FbTransactionBehavior.Consistency,
                "read_committed" => FbTransactionBehavior.ReadCommitted | FbTransactionBehavior.RecVersion,
                "no_rec_version" => FbTransactionBehavior.ReadCommitted | FbTransactionBehavior.NoRecVersion,
                "wait" => FbTransactionBehavior.Wait,
                "nowait" => FbTransactionBehavior.NoWait,
                "read_only" => FbTransactionBehavior.Read,
                "timeout" => FbTransactionBehavior.Wait,
                _ => throw new InvalidOperationException("unknown transaction option " + words[i]),
            };
            if (words[i] == "timeout") timeout = TimeSpan.FromSeconds(int.Parse(words[++i], CultureInfo.InvariantCulture));
        }
        if ((b & FbTransactionBehavior.Read) == 0) b |= FbTransactionBehavior.Write;
        if ((b & (FbTransactionBehavior.Concurrency | FbTransactionBehavior.Consistency | FbTransactionBehavior.ReadCommitted)) == 0)
            b |= FbTransactionBehavior.ReadCommitted | FbTransactionBehavior.RecVersion;
        return new FbTransactionOptions { TransactionBehavior = b, WaitTimeout = timeout };
    }

    static object Param(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Null => DBNull.Value,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => e.GetString()!,
        JsonValueKind.Number when e.TryGetInt64(out var l) => l,
        JsonValueKind.Number => e.GetDecimal(),
        _ => throw new InvalidOperationException("unsupported parameter " + e),
    };

    static string Format(object? v) => v switch
    {
        null => "NULL",
        byte[] bytes => Convert.ToHexString(bytes),
        DateTime d => d.ToString("yyyy-MM-dd HH:mm:ss.ffff", CultureInfo.InvariantCulture),
        TimeSpan t => t.ToString(@"hh\:mm\:ss\.ffff", CultureInfo.InvariantCulture),
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        float f => f.ToString("R", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => v.ToString() ?? "",
    };

    static readonly string[] SchemaColumns =
        ["ColumnSize", "NumericPrecision", "NumericScale", "AllowDBNull", "BaseTableName", "BaseColumnName", "IsKey", "IsUnique"];

    static string Describe(FbConnection cn, FbTransaction tx, string sql, string? parameters)
    {
        var sb = new StringBuilder();
        try
        {
            using var cmd = new FbCommand(sql, cn, tx);
            if (parameters != null)
                foreach (var p in JsonDocument.Parse(parameters).RootElement.EnumerateArray())
                    cmd.Parameters.Add(new FbParameter { Value = Param(p) });
            using var r = cmd.ExecuteReader();
            if (r.FieldCount > 0)
            {
                var schema = r.GetSchemaTable();
                for (var i = 0; i < r.FieldCount; i++)
                {
                    sb.Append(r.GetName(i)).Append(':').Append(r.GetDataTypeName(i));
                    var row = schema.Rows[i];
                    sb.Append(" (");
                    sb.Append(string.Join(",", SchemaColumns.Select(c => row[c] is DBNull ? "" : Format(row[c]).Trim())));
                    sb.AppendLine(")");
                }
            }
            var rows = 0;
            while (r.Read())
            {
                rows++;
                for (var i = 0; i < r.FieldCount; i++)
                    sb.Append(r.IsDBNull(i) ? "NULL" : Format(r.GetValue(i))).Append(" | ");
                sb.AppendLine();
            }
            r.Close();
            sb.Append("affected ").Append(r.RecordsAffected);
        }
        catch (FbException e)
        {
            sb.Append(DescribeError(e));
        }
        catch (Exception e) when (e is not InvalidOperationException { Message: "unsupported parameter" })
        {
            // the client could not handle what the server sent (e.g. a value out of its described type's range)
            sb.Append("CLIENT EXCEPTION ").Append(e.GetType().Name).Append(": ").Append(e.Message);
        }
        return sb.ToString().Replace("\r", "");
    }

    [GeneratedRegex(@"^\s*(At (block|procedure|trigger|function|sub-procedure|sub-function)\b.*|line: \d+, col: \d+.*)$", RegexOptions.Multiline)]
    private static partial Regex Location();

    static string DescribeError(FbException e)
    {
        var codes = StatusVector(e);
        var msg = Location().Replace(e.Message.Replace("\r", ""), "");
        msg = string.Join("\n", msg.Split('\n').Select(l => l.TrimEnd()).Where(l => l.Length > 0));
        return $"ERROR {codes} {e.SQLSTATE}\n{msg}";
    }

    /// <summary>The typed status vector the client received (gds codes, 'strings', #numbers, @sqlstate).</summary>
    static string StatusVector(FbException e)
    {
        if (e.InnerException is not { } isc || isc.GetType().GetProperty("Errors")?.GetValue(isc) is not System.Collections.IEnumerable errors)
            return string.Join(",", e.Errors.Select(x => x.Number));
        var parts = new List<string>();
        foreach (var err in errors)
        {
            var t = err.GetType();
            var type = (int)t.GetProperty("Type")!.GetValue(err)!;
            var code = (int)t.GetProperty("ErrorCode")!.GetValue(err)!;
            var str = t.GetProperty("StrParam")!.GetValue(err) as string;
            parts.Add(type switch
            {
                1 => code.ToString(CultureInfo.InvariantCulture),  // isc_arg_gds
                4 => "#" + code.ToString(CultureInfo.InvariantCulture), // isc_arg_number
                19 => "@" + str,                                    // isc_arg_sql_state
                18 => "warning:" + code.ToString(CultureInfo.InvariantCulture),
                _ => "'" + str + "'",
            });
        }
        return string.Join(" ", parts);
    }

    /// <summary>Formats the descriptions of a file as a golden text: a "--- header" line per step that has output.</summary>
    public static string Golden(CorpusFile file, List<string> results)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < file.Steps.Count; i++)
        {
            if (file.Steps[i].Kind != "case" && results[i].Length == 0) continue;
            sb.Append("--- ").Append(file.Steps[i].Header).Append('\n');
            sb.Append(results[i].TrimEnd('\n')).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>Splits a golden text back into per-step blocks keyed by position.</summary>
    public static List<string> Blocks(string golden)
    {
        var blocks = new List<string>();
        StringBuilder? cur = null;
        foreach (var line in golden.Replace("\r", "").Split('\n'))
        {
            if (line.StartsWith("--- ", StringComparison.Ordinal))
            {
                if (cur != null) blocks.Add(cur.ToString());
                cur = new StringBuilder();
            }
            cur?.Append(line).Append('\n');
        }
        if (cur != null) blocks.Add(cur.ToString());
        return blocks;
    }
}
