using System.Diagnostics;
using Fakebird;
using Fakebird.Engine;

/// <summary>
/// `restore file1.sql [file2.sql ...]`: builds a template from the scripts and measures the time and the bytes
/// allocated by a <see cref="FakebirdServer.Restore"/> of it, the cost paid before every test.
/// </summary>
static class RestoreBench
{
    public static void Run(string[] files)
    {
        using var server = FakebirdServer.Start();
        var template = server.CreateDatabase("template");
        var db = server.Engine("template");
        foreach (var file in files)
            ScriptRunner.RunAll(db, File.ReadAllText(file), (_, _) => { });
        var snap = template.Snapshot();
        Console.WriteLine($"{db.Tables.Count} tables, {db.Indexes.Count} indexes, {db.Constraints.Count} constraints");
        server.CreateDatabase("bench", snap);

        const int n = 2_000;
        for (var i = 0; i < 100; i++) server.Restore("bench", snap);
        GC.Collect();
        var bytes = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < n; i++) server.Restore("bench", snap);
        sw.Stop();
        bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
        Console.WriteLine($"restore: {sw.Elapsed.TotalMicroseconds / n:F1} us, {bytes / n / 1024.0:F1} KB allocated");
    }
}
