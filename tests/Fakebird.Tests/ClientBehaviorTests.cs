using System.Data;
using FirebirdSql.Data.FirebirdClient;

namespace Fakebird.Tests;

[Collection("server")]
public class ClientBehaviorTests(ServerFixture fx)
{
    static object? Scalar(FbConnection cn, string sql, FbTransaction? tx = null)
    {
        using var cmd = new FbCommand(sql, cn, tx);
        var v = cmd.ExecuteScalar();
        return v is DBNull ? null : v;
    }

    static void Exec(FbConnection cn, string sql, FbTransaction? tx = null)
    {
        using var cmd = new FbCommand(sql, cn, tx);
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public void Savepoints()
    {
        using var cn = fx.NewDatabase("create table t (id integer primary key);");
        using var tx = cn.BeginTransaction();
        Exec(cn, "insert into t values (1)", tx);
        tx.Save("sp1");
        Exec(cn, "insert into t values (2)", tx);
        tx.Rollback("sp1");
        Exec(cn, "insert into t values (3)", tx);
        tx.Commit();
        Assert.Equal(4L, Scalar(cn, "select sum(id) from t"));
    }

    [Fact]
    public void FailedStatementIsAtomicButTransactionContinues()
    {
        using var cn = fx.NewDatabase("create table t (id integer primary key, v integer check (v < 10));");
        using var tx = cn.BeginTransaction();
        Exec(cn, "insert into t values (1, 1)", tx);
        Exec(cn, "insert into t values (2, 2)", tx);
        Assert.Throws<FbException>(() => Exec(cn, "update t set v = v + 8", tx)); // second row violates
        Assert.Equal(3L, Scalar(cn, "select sum(v) from t", tx)); // first row's update undone
        tx.Commit();
    }

    [Fact]
    public void BinaryBlobsAndBooleans()
    {
        using var cn = fx.NewDatabase("create table b (id integer primary key, data blob sub_type binary, flag boolean);");
        var bytes = Enumerable.Range(0, 200_000).Select(i => (byte)(i * 7)).ToArray();
        using (var cmd = new FbCommand("insert into b values (1, @d, @f)", cn))
        {
            cmd.Parameters.AddWithValue("@d", bytes);
            cmd.Parameters.AddWithValue("@f", false);
            cmd.ExecuteNonQuery();
        }
        using (var cmd = new FbCommand("select data, flag, octet_length(data) from b where flag = @f", cn))
        {
            cmd.Parameters.AddWithValue("@f", false);
            using var r = cmd.ExecuteReader();
            Assert.True(r.Read());
            Assert.Equal(bytes, (byte[])r.GetValue(0));
            Assert.False(r.GetBoolean(1));
            Assert.Equal(200_000L, r.GetInt64(2));
        }
    }

    [Fact]
    public void CharPaddingCollationsAndNullOrdering()
    {
        using var cn = fx.NewDatabase("""
            create table c (id integer, code char(5), name varchar(20) collate unicode_ci, n integer);
            insert into c values (1, 'ab', 'Alpha', null);
            insert into c values (2, 'abc', 'beta', 2);
            insert into c values (3, null, 'GAMMA', 1);
            """);
        Assert.Equal("ab   ", Scalar(cn, "select code from c where id = 1"));
        Assert.Equal(1, Scalar(cn, "select id from c where code = 'ab'"));
        Assert.Equal(1, Scalar(cn, "select id from c where code = 'ab  '"));
        Assert.Equal(7, Scalar(cn, "select char_length(code || 'xy') from c where id = 1"));
        Assert.Equal(3, Scalar(cn, "select id from c where name = 'gamma'"));
        Assert.Equal(2, Scalar(cn, "select id from c where name like 'B%'"));
        using var cmd = new FbCommand("select id from c order by n", cn);
        using var r = cmd.ExecuteReader();
        var ids = new List<int>();
        while (r.Read()) ids.Add(r.GetInt32(0));
        Assert.Equal([1, 3, 2], ids); // NULLs first in ascending order
        Assert.Equal(2, Scalar(cn, "select first 1 id from c order by n desc"));
        Assert.Equal(1, Scalar(cn, "select first 1 id from c order by n desc nulls first"));
    }

    [Fact]
    public void LargeResultAndTwoOpenReaders()
    {
        using var cn = fx.NewDatabase("""
            create table big (id integer primary key, txt varchar(30));
            set term ^ ;
            execute block as
            declare i integer = 0;
            begin
              while (i < 5000) do begin insert into big values (:i, 'row ' || :i); i = i + 1; end
            end^
            set term ; ^
            """);
        using var c1 = new FbCommand("select id, txt from big order by id", cn);
        using var c2 = new FbCommand("select id from big order by id desc", cn);
        using var r1 = c1.ExecuteReader();
        using var r2 = c2.ExecuteReader();
        var n = 0;
        while (r1.Read())
        {
            Assert.True(r2.Read());
            Assert.Equal(n, r1.GetInt32(0));
            Assert.Equal(4999 - n, r2.GetInt32(0));
            Assert.Equal("row " + n, r1.GetString(1));
            n++;
        }
        Assert.Equal(5000, n);
    }

    [Fact]
    public void DateTimeRoundTrips()
    {
        using var cn = fx.NewDatabase("create table d (ts timestamp, dt date, tm time);");
        var ts = new DateTime(2024, 12, 31, 23, 59, 58, 123);
        using (var cmd = new FbCommand("insert into d values (@ts, @dt, @tm)", cn))
        {
            cmd.Parameters.AddWithValue("@ts", ts);
            cmd.Parameters.AddWithValue("@dt", new DateTime(2000, 2, 29));
            cmd.Parameters.AddWithValue("@tm", new TimeSpan(0, 13, 14, 15, 500));
            cmd.ExecuteNonQuery();
        }
        using (var cmd = new FbCommand("select ts, dt, tm, ts + 1, dt - date '2000-01-01', cast(ts as date), extract(millisecond from ts) from d", cn))
        using (var r = cmd.ExecuteReader())
        {
            Assert.True(r.Read());
            Assert.Equal(ts, r.GetDateTime(0));
            Assert.Equal(new DateTime(2000, 2, 29), r.GetDateTime(1));
            Assert.Equal(new TimeSpan(0, 13, 14, 15, 500), r.GetValue(2));
            Assert.Equal(ts.AddDays(1), r.GetDateTime(3));
            Assert.Equal(59L, r.GetInt64(4));
            Assert.Equal(new DateTime(2024, 12, 31), r.GetDateTime(5));
            Assert.Equal(123.0m, r.GetDecimal(6));
        }
    }

    [Fact]
    public void StoredProcedureCommandTypeAndNullParameters()
    {
        using var cn = fx.NewDatabase("""
            create table t (id integer primary key, v varchar(10));
            set term ^ ;
            create procedure list_t (minid integer) returns (id integer, v varchar(10)) as
            begin
              for select id, v from t where id >= coalesce(:minid, 0) into :id, :v do suspend;
            end^
            set term ; ^
            insert into t values (1, null);
            insert into t values (2, 'two');
            """);
        using (var cmd = new FbCommand("select count(*) from t where v is not distinct from @v", cn))
        {
            cmd.Parameters.Add("@v", FbDbType.VarChar).Value = DBNull.Value;
            Assert.Equal(1L, cmd.ExecuteScalar());
        }
        using (var cmd = new FbCommand("select id from list_t(@m) order by id", cn))
        {
            cmd.Parameters.AddWithValue("@m", DBNull.Value);
            using var r = cmd.ExecuteReader();
            Assert.True(r.Read());
            Assert.Equal(1, r.GetInt32(0));
        }
    }

    [Fact]
    public void ClientCreateAndDropDatabase()
    {
        var name = "created_" + Guid.NewGuid().ToString("N");
        var cs = fx.Server.ConnectionString(name);
        FbConnection.CreateDatabase(cs);
        using (var cn = new FbConnection(cs))
        {
            cn.Open();
            Exec(cn, "create table x (a integer)");
            Exec(cn, "insert into x values (5)");
            Assert.Equal(5, Scalar(cn, "select a from x"));
        }
        FbConnection.ClearPool(new FbConnection(cs));
        FbConnection.DropDatabase(cs);
        Assert.ThrowsAny<Exception>(() => fx.Server.GetDatabase(name));
    }

    [Fact]
    public void GlobalTemporaryTables()
    {
        using var cn = fx.NewDatabase("create global temporary table gtt (id integer) on commit delete rows;");
        using var other = new FbConnection(cn.ConnectionString);
        other.Open();
        using (var tx = cn.BeginTransaction())
        {
            Exec(cn, "insert into gtt values (1)", tx);
            Assert.Equal(1L, Scalar(cn, "select count(*) from gtt", tx));
            Assert.Equal(0L, Scalar(other, "select count(*) from gtt"));
            tx.Commit();
        }
        Assert.Equal(0L, Scalar(cn, "select count(*) from gtt"));
    }

    [Fact]
    public void UnsupportedFeaturesFailLoudly()
    {
        using var cn = fx.NewDatabase();
        var ex = Assert.Throws<FbException>(() => Exec(cn, "create package p as begin end"));
        Assert.Contains("Fakebird: unsupported", ex.Message);
        ex = Assert.Throws<FbException>(() => Exec(cn, "select * from no_such_table"));
        Assert.Contains("Table unknown", ex.Message);
        ex = Assert.Throws<FbException>(() => Exec(cn, "selec 1 from rdb$database"));
        Assert.Contains("Token unknown", ex.Message);
    }
}
