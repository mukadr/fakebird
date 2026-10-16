using Fakebird.Engine;
using Fakebird.Types;

namespace Fakebird.Tests;

/// <summary>In-process engine tests (no wire protocol).</summary>
public class EngineTests
{
    readonly Database _db = new("test");
    readonly Transaction _tx;

    public EngineTests() => _tx = new Attachment(_db).StartTransaction();

    void Exec(string sql, params object?[] args)
    {
        lock (_db.Sync) Executor.Execute(_tx, sql, args.Select(ToValue).ToArray());
    }

    List<object?[]> Rows(string sql, params object?[] args)
    {
        lock (_db.Sync)
        {
            var r = Executor.Execute(_tx, sql, args.Select(ToValue).ToArray());
            return (r.Rows ?? []).Select(row => row.Select(v => v.ToClr()).ToArray()).ToList();
        }
    }

    object? Scalar(string sql, params object?[] args) => Rows(sql, args).Single()[0];

    static Value ToValue(object? o) => o switch
    {
        null => Value.Null,
        int i => Value.Int(i),
        long l => Value.Int(l),
        string s => Value.Str(s),
        bool b => Value.Bool(b),
        double d => Value.Dbl(d),
        decimal m => Conv.ParseNumber(m.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        _ => throw new ArgumentException(o.GetType().Name),
    };

    [Fact]
    public void SelectFromRdbDatabase()
    {
        Assert.Equal(1L, Scalar("select 1 from rdb$database"));
    }

    [Fact]
    public void CreateInsertSelect()
    {
        Exec("create table t (id integer not null primary key, name varchar(20), amount numeric(10,2) default 0)");
        Exec("insert into t (id, name) values (1, 'one')");
        Exec("insert into t (id, name, amount) values (?, ?, ?)", 2, "two", 12.5m);
        var rows = Rows("select id, name, amount from t order by id desc");
        Assert.Equal(2, rows.Count);
        Assert.Equal(2L, rows[0][0]);
        Assert.Equal("two", rows[0][1]);
        Assert.Equal(12.50m, rows[0][2]);
        Assert.Equal(0m, rows[1][2]);
    }

    [Fact]
    public void UniqueViolation()
    {
        Exec("create table t (id integer not null primary key)");
        Exec("insert into t values (1)");
        var e = Assert.Throws<FbError>(() => Exec("insert into t values (1)"));
        Assert.Equal(335544665, e.GdsCode);
    }

    [Fact]
    public void JoinsAndAggregates()
    {
        Exec("create table c (id int primary key, name varchar(10))");
        Exec("create table o (id int primary key, cid int references c(id), total numeric(9,2))");
        Exec("insert into c values (1, 'a')");
        Exec("insert into c values (2, 'b')");
        Exec("insert into c values (3, 'c')");
        Exec("insert into o values (10, 1, 5)");
        Exec("insert into o values (11, 1, 7.5)");
        Exec("insert into o values (12, 2, 1)");
        var rows = Rows("select c.name, count(o.id), sum(o.total) from c left join o on o.cid = c.id group by c.name order by 1");
        Assert.Equal(3, rows.Count);
        Assert.Equal(new object?[] { "a", 2L, 12.50m }, rows[0]);
        Assert.Equal(new object?[] { "c", 0L, null }, rows[2]);
        var e = Assert.Throws<FbError>(() => Exec("insert into o values (13, 99, 1)"));
        Assert.Equal(335544466, e.GdsCode);
    }

    [Fact]
    public void ProcedureAndTrigger()
    {
        Exec("create table t (id int not null primary key, v varchar(10))");
        Exec("create sequence s");
        Exec("""
            create trigger t_bi for t before insert as
            begin
              if (new.id is null) then new.id = next value for s;
            end
            """);
        Exec("insert into t (v) values ('x')");
        Exec("insert into t (v) values ('y')");
        Assert.Equal(2L, Scalar("select max(id) from t"));
        Exec("""
            create procedure p (n int) returns (i int, sq int) as
            begin
              i = 1;
              while (i <= n) do
              begin
                sq = i * i;
                suspend;
                i = i + 1;
              end
            end
            """);
        var rows = Rows("select i, sq from p(3)");
        Assert.Equal(3, rows.Count);
        Assert.Equal(9L, rows[2][1]);
    }

    [Fact]
    public void SnapshotCopyOnWrite()
    {
        Exec("create table t (id int primary key, v varchar(10))");
        Exec("insert into t values (1, 'a')");
        _tx.Commit(retaining: true);
        var snap = _db.Snapshot();
        var a = Database.FromSnapshot(snap, "a");
        var b = Database.FromSnapshot(snap, "b");
        Assert.Same(a.Tables["T"].Rows, b.Tables["T"].Rows);
        var ta = new Attachment(a).StartTransaction();
        lock (a.Sync) Executor.Execute(ta, "update t set v = 'changed'");
        lock (a.Sync) Executor.Execute(ta, "insert into t values (2, 'b')");
        ta.Commit();
        var tb = new Attachment(b).StartTransaction();
        lock (b.Sync)
        {
            var r = Executor.Execute(tb, "select v from t");
            Assert.Single(r.Rows!);
            Assert.Equal("a", r.Rows![0][0].AsString);
        }
        Assert.NotSame(a.Tables["T"].Rows, b.Tables["T"].Rows);
        var ts = new Attachment(Database.FromSnapshot(snap, "c")).StartTransaction();
        lock (ts.Db.Sync) Assert.Equal("a", Executor.Execute(ts, "select v from t").Rows![0][0].AsString);
    }
}
