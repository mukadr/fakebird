using System.Text;
using FirebirdSql.Data.FirebirdClient;
using Xunit.Abstractions;

namespace Fakebird.Tests.Corpus;

/// <summary>
/// Runs every <c>Corpus/*.sql</c> file against Fakebird and compares the result with the golden output recorded
/// from a real Firebird 5 (<c>Corpus/*.{mode}.out</c>). When the mode's server is configured (e.g.
/// FAKEBIRD_FB5C3_CS), the real server is also checked against the golden file; FAKEBIRD_RECORD=1 rewrites
/// the golden files from the real server instead.
/// </summary>
[Collection("server")]
public class CorpusTests(ServerFixture fx, ITestOutputHelper output)
{
    /// <summary>Corpus mode: golden file suffix and the environment variable with the real server's connection string.</summary>
    public sealed record Mode(string Name, string EnvVar);

    public static readonly Mode[] Modes =
    [
        new("fb5c3", "FAKEBIRD_FB5C3_CS"), // Firebird 5 with DataTypeCompatibility = 3.0
    ];

    static readonly bool Record = Environment.GetEnvironmentVariable("FAKEBIRD_RECORD") == "1";

    public static string CorpusDir
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Fakebird.Tests.csproj"))) dir = dir.Parent;
            return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("Fakebird.Tests.csproj"), "Corpus");
        }
    }

    public static IEnumerable<object[]> Files() =>
        from f in Directory.GetFiles(CorpusDir, "*.sql").Order()
        from m in Modes
        select new object[] { Path.GetFileNameWithoutExtension(f), m.Name };

    [Theory]
    [MemberData(nameof(Files))]
    public void MatchesFirebird(string area, string modeName)
    {
        var mode = Modes.Single(m => m.Name == modeName);
        var file = CorpusFile.Parse(area, File.ReadAllText(Path.Combine(CorpusDir, area + ".sql")));
        if (file.Modes.Count > 0 && !file.Modes.Contains(mode.Name)) return;
        var goldenPath = Path.Combine(CorpusDir, $"{area}.{mode.Name}.out");

        var realCs = Environment.GetEnvironmentVariable(mode.EnvVar);
        if (realCs != null)
        {
            var real = CorpusRunner.Golden(file, RunReal(realCs, file));
            if (Record || !File.Exists(goldenPath))
                File.WriteAllText(goldenPath, real);
            else
                Compare("golden", File.ReadAllText(goldenPath), "real", real, $"{area}.{mode.Name}.out is stale (re-record with FAKEBIRD_RECORD=1)");
        }

        Assert.True(File.Exists(goldenPath), $"{goldenPath} missing: record it with {mode.EnvVar} set");
        var db = fx.Server.CreateDatabase($"corpus_{area}_{Guid.NewGuid():N}");
        CorpusRunner.RunSetup(db.ConnectionString(), file.Setup);
        var fake = CorpusRunner.Golden(file, CorpusRunner.Run(db.ConnectionString(), file));
        fx.Server.DropDatabase(db.Name);
        Compare("real", File.ReadAllText(goldenPath), "fake", fake, $"Fakebird differs from Firebird ({mode.Name}) in {area}.sql");
    }

    public static List<string> RunReal(string realCs, CorpusFile file)
    {
        var cs = realCs.Replace("{db}", $"corpus_{Guid.NewGuid().ToString("N")[..10]}");
        FbConnection.CreateDatabase(cs, pageSize: 8192, forcedWrites: false, overwrite: true);
        try
        {
            CorpusRunner.RunSetup(cs, file.Setup);
            return CorpusRunner.Run(cs, file);
        }
        finally
        {
            FbConnection.ClearAllPools();
            FbConnection.DropDatabase(cs);
        }
    }

    void Compare(string expectedName, string expected, string actualName, string actual, string title)
    {
        var e = CorpusRunner.Blocks(expected);
        var a = CorpusRunner.Blocks(actual);
        var diffs = new StringBuilder();
        var n = 0;
        for (var i = 0; i < Math.Max(e.Count, a.Count); i++)
        {
            var x = i < e.Count ? e[i] : "(missing)\n";
            var y = i < a.Count ? a[i] : "(missing)\n";
            if (x == y) continue;
            n++;
            diffs.Append($"#{i}\n{expectedName}:\n{x}{actualName}:\n{y}\n");
        }
        if (n == 0) return;
        output.WriteLine(diffs.ToString());
        Assert.Fail($"{title}: {n} of {Math.Max(e.Count, a.Count)} cases differ\n{diffs}");
    }
}
