using System.Text;
using Fakebird.Tests.Corpus;
using Xunit.Abstractions;

namespace Fakebird.Tests;

/// <summary>
/// Exploration helper: runs the file named by FAKEBIRD_PROBE on every configured real server (FAKEBIRD_FB5_CS,
/// FAKEBIRD_FB5C3_CS, FAKEBIRD_FB3_CS) and on Fakebird, and prints each answer marked SAME/DIFF (Fakebird compared
/// with the FB5 DataTypeCompatibility=3.0 server). The file is either in corpus format (<c>-- @setup</c>, <c>-- @case</c>...) or holds one statement
/// per line (run after the setup of Corpus/basics.sql).
/// </summary>
[Collection("server")]
public class DifferentialTests(ServerFixture fx, ITestOutputHelper output)
{
    static readonly (string Name, string EnvVar)[] Servers =
        [("fb5", "FAKEBIRD_FB5_CS"), ("fb5c3", "FAKEBIRD_FB5C3_CS"), ("fb3", "FAKEBIRD_FB3_CS")];

    [Fact]
    public void Probe()
    {
        var path = Environment.GetEnvironmentVariable("FAKEBIRD_PROBE");
        if (path == null) return;
        var text = File.ReadAllText(path);
        if (!text.Contains("-- @"))
        {
            var basics = File.ReadAllText(Path.Combine(CorpusTests.CorpusDir, "basics.sql"));
            var setup = basics[..basics.IndexOf("-- @case", StringComparison.Ordinal)];
            text = setup + string.Concat(text.Split('\n').Where(l => l.Trim().Length > 0 && !l.TrimStart().StartsWith("--"))
                .Select(l => "-- @case\n" + l + "\n"));
        }
        var file = CorpusFile.Parse("probe", text);

        var db = fx.Server.CreateDatabase("probe_" + Guid.NewGuid().ToString("N"));
        db.DefaultTimeZone = CorpusTests.RealTimeZone;
        CorpusRunner.RunSetup(db.ConnectionString(), file.Setup);
        var fake = CorpusRunner.Run(db.ConnectionString(), file);
        var real = Servers
            .Select(s => (s.Name, Cs: Environment.GetEnvironmentVariable(s.EnvVar)))
            .Where(s => s.Cs != null)
            .Select(s => (s.Name, Results: CorpusTests.RunReal(s.Cs!, file)))
            .ToList();

        var sb = new StringBuilder();
        for (var i = 0; i < file.Steps.Count; i++)
        {
            if (file.Steps[i].Kind != "case") continue;
            // the oracle is Firebird 5 with DataTypeCompatibility = 3.0 (other servers are informative)
            var oracle = real.FirstOrDefault(r => r.Name == "fb5c3").Results ?? real.FirstOrDefault().Results;
            var same = oracle == null || oracle[i] == fake[i];
            sb.Append($"{(same ? "SAME" : "DIFF")} --- {file.Steps[i].Text}\n");
            foreach (var (name, results) in real) sb.Append($"{name}:\n{results[i]}\n");
            sb.Append($"fake:\n{fake[i]}\n\n");
        }
        output.WriteLine(sb.ToString());
        var outFile = Environment.GetEnvironmentVariable("FAKEBIRD_PROBE_OUT");
        if (outFile != null) File.WriteAllText(outFile, sb.ToString());
    }
}
