using FirebirdSql.Data.FirebirdClient;
using Xunit.Abstractions;

namespace Fakebird.Tests;

/// <summary>SELECT ... WITH LOCK locks the returned rows like a dummy update (checked against Firebird 3).</summary>
[Collection("server")]
public class WithLockTests(ServerFixture fx, ITestOutputHelper output)
{
    static readonly string? RealCs = Environment.GetEnvironmentVariable("FAKEBIRD_REAL_CS");

    static FbTransactionOptions Options(bool wait) => new()
    {
        TransactionBehavior = (wait ? FbTransactionBehavior.Wait : FbTransactionBehavior.NoWait)
            | FbTransactionBehavior.ReadCommitted | FbTransactionBehavior.RecVersion,
    };

    static async Task<string> Scenario(string cs)
    {
        var log = new List<string>();
        await using var setup = new FbConnection(cs);
        await setup.OpenAsync();
        foreach (var sql in new[] { "recreate table lk (id integer primary key, n integer)", "insert into lk values (1, 0)", "insert into lk values (2, 0)" })
        {
            await using var cmd = new FbCommand(sql, setup);
            await cmd.ExecuteNonQueryAsync();
        }

        await using var a = new FbConnection(cs);
        await a.OpenAsync();
        await using var b = new FbConnection(cs);
        await b.OpenAsync();

        // A locks row 1 only (WHERE applies before the lock)
        await using var txA = await a.BeginTransactionAsync(Options(wait: false));
        await using (var sel = new FbCommand("select id from lk where id = 1 with lock", a, txA))
            log.Add("locked " + await sel.ExecuteScalarAsync());

        // B (NOWAIT) cannot update the locked row, can update the other one
        await using (var txB = await b.BeginTransactionAsync(Options(wait: false)))
        {
            try
            {
                await using var upd = new FbCommand("update lk set n = 1 where id = 1", b, txB);
                await upd.ExecuteNonQueryAsync();
                log.Add("nowait update: ok");
            }
            catch (FbException e) { log.Add("nowait update: " + e.ErrorCode); }
            await using (var upd2 = new FbCommand("update lk set n = 1 where id = 2", b, txB))
                log.Add("other row: " + await upd2.ExecuteNonQueryAsync());
            await txB.RollbackAsync();
        }

        // plain UPDATE vs UPDATE, NOWAIT: same error as for the locked row
        await using var c = new FbConnection(cs);
        await c.OpenAsync();
        await using (var txA2 = await c.BeginTransactionAsync(Options(wait: false)))
        await using (var txB2 = await b.BeginTransactionAsync(Options(wait: false)))
        {
            await using (var u1 = new FbCommand("update lk set n = 5 where id = 2", c, txA2)) await u1.ExecuteNonQueryAsync();
            try
            {
                await using var u2 = new FbCommand("update lk set n = 6 where id = 2", b, txB2);
                await u2.ExecuteNonQueryAsync();
                log.Add("nowait update vs update: ok");
            }
            catch (FbException e) { log.Add("nowait update vs update: " + e.ErrorCode); }
            await txB2.RollbackAsync();
            await txA2.RollbackAsync();
        }

        // B (WAIT) blocks until A commits, then proceeds
        await using var txW = await b.BeginTransactionAsync(Options(wait: true));
        var waiting = Task.Run(async () =>
        {
            await using var upd = new FbCommand("update lk set n = n + 1 where id = 1", b, txW);
            try { return "wait update: " + await upd.ExecuteNonQueryAsync(); }
            catch (FbException e) { return "wait update: " + e.ErrorCode; }
        });
        var done = await Task.WhenAny(waiting, Task.Delay(1000));
        log.Add(done == waiting ? "did not wait" : "waited");
        await txA.CommitAsync();
        log.Add(await waiting);
        await txW.RollbackAsync();
        return string.Join(" | ", log);
    }

    [Fact]
    public async Task WithLockBlocksConcurrentWriters()
    {
        var fake = fx.Server.CreateDatabase("lk_" + Guid.NewGuid().ToString("N"));
        var mine = await Scenario(fake.ConnectionString());
        output.WriteLine("fake: " + mine);
        if (RealCs != null)
        {
            var realCs = RealCs.Replace("{db}", "lk_" + Guid.NewGuid().ToString("N")[..8]);
            FbConnection.CreateDatabase(realCs, pageSize: 8192, forcedWrites: false, overwrite: true);
            try
            {
                var real = await Scenario(realCs);
                output.WriteLine("real: " + real);
                Assert.Equal(real, mine);
            }
            finally
            {
                FbConnection.ClearAllPools();
                FbConnection.DropDatabase(realCs);
            }
        }
        Assert.Equal("locked 1 | nowait update: 335544336 | other row: 1 | nowait update vs update: 335544336 | waited | wait update: 335544336", mine);
    }
}
