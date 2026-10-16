using Fakebird;
using Fakebird.Engine;

/// <summary>
/// `script file1.sql [file2.sql ...]`: runs the files in order on one in-memory database, continuing after
/// errors, and prints the distinct failures (grouped by message) with a sample statement and the count.
/// </summary>
static class ScriptCheck
{
    public static void Run(string[] files)
    {
        var db = new Database("check");
        var failures = new List<(string File, string Stmt, FbError Error)>();
        foreach (var file in files)
        {
            var started = DateTime.UtcNow;
            var n = ScriptRunner.RunAll(db, File.ReadAllText(file), (stmt, e) => failures.Add((file, stmt, e)));
            Console.WriteLine($"{Path.GetFileName(file)}: {n} statements, {(DateTime.UtcNow - started).TotalMilliseconds:N0} ms");
        }
        Console.WriteLine($"{failures.Count} failures");
        foreach (var g in failures.GroupBy(f => f.Error.Message).OrderByDescending(g => g.Count()))
        {
            var first = g.First();
            Console.WriteLine($"\n=== {g.Count()}x  {first.Error.Message.ReplaceLineEndings(" | ")}");
            var lines = first.Stmt.Split('\n');
            var head = lines.Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0 && !l.StartsWith("/*") && !l.StartsWith("--")) ?? "";
            Console.WriteLine($"--- {Path.GetFileName(first.File)}: {head}");
            var m = System.Text.RegularExpressions.Regex.Match(first.Error.Message, @"line (\d+), column (\d+)");
            if (m.Success && int.Parse(m.Groups[1].Value) - 1 < lines.Length)
                Console.WriteLine($"    line {m.Groups[1].Value}: {lines[int.Parse(m.Groups[1].Value) - 1].TrimEnd()}");
        }
    }
}
