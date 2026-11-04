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
    public void SystemTableEqualityLookups()
    {
        const string exists = """
            select count(*) from rdb$relation_fields rf join rdb$relations r on r.rdb$relation_name = rf.rdb$relation_name
            where r.rdb$relation_name = upper(?) and rf.rdb$field_name = upper(?)
            """;
        Assert.Equal(0L, Scalar(exists, "t", "name"));
        Exec("create table t (id integer, name varchar(20))");
        // the catalog changed: new RDB$ rows, new lookups
        Assert.Equal(1L, Scalar(exists, "t", "name"));
        Assert.Equal(0L, Scalar(exists, "t", "other"));
        // CHAR(63) names compare without their blanks; a NULL key matches nothing
        Assert.Equal(1L, Scalar("select count(*) from rdb$relations where rdb$relation_name = 'T  '"));
        Assert.Equal(0L, Scalar("select count(*) from rdb$relations where rdb$relation_name = ?", (object?)null));
        Assert.Equal<object?>(["ID", "NAME"], Rows("select trim(rdb$field_name) from rdb$relation_fields where rdb$relation_name = 'T' order by rdb$field_position").Select(r => r[0]));
        // a clone shares the snapshot's RDB$ rows until its own DDL
        var clone = Database.FromSnapshot(_db.Snapshot(), "clone");
        var tx = new Attachment(clone).StartTransaction();
        long Count(string sql) { lock (clone.Sync) return (long)Executor.Execute(tx, sql, []).Rows!.Single()[0].ToClr()!; }
        const string cols = "select count(*) from rdb$relation_fields where rdb$relation_name = 'T'";
        Assert.Equal(2L, Count(cols));
        lock (clone.Sync) Executor.Execute(tx, "alter table t add extra integer", []);
        Assert.Equal(3L, Count(cols));
        Assert.Equal(2L, Scalar(cols));
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

    [Fact]
    public void SnapshotClonesShareDefinitionsUntilDdl()
    {
        Exec("create table p (id int primary key)");
        Exec("create table t (id int primary key, v varchar(10) default 'x', pid int references p(id), check (id > 0))");
        Exec("create index t_v on t(v)");
        Exec("insert into p values (1)");
        _tx.Commit(retaining: true);
        var snap = _db.Snapshot();
        var a = Database.FromSnapshot(snap, "a");
        var b = Database.FromSnapshot(snap, "b");
        Assert.Same(a.Tables["T"].Columns, b.Tables["T"].Columns);
        Assert.Same(a.Constraints[a.Tables["T"].ForeignKeys[0]], b.Constraints[b.Tables["T"].ForeignKeys[0]]);

        var ta = new Attachment(a).StartTransaction();
        void ExecA(string sql) { lock (a.Sync) Executor.Execute(ta, sql); }
        // compiled before the DDL: the clones share what the compiler knows about T's columns
        ExecA("select v, pid from t");
        ExecA("alter table t add w int");
        ExecA("alter table t alter v set default 'y'");
        ExecA("alter table t alter v to v2");
        ExecA("comment on column t.id is 'changed'");
        ExecA($"alter table t drop constraint {a.Tables["T"].ForeignKeys[0]}");
        ExecA("drop index t_v");
        ExecA("create index t_id_desc on t(id)");
        ExecA("create trigger t_bi for t before insert as begin new.w = 7; end");
        ExecA("insert into t (id) values (1)");
        lock (a.Sync) Assert.Equal(7L, Executor.Execute(ta, "select w from t where v2 = 'y'").Rows![0][0].ToClr());
        Assert.Contains("Column unknown", Assert.Throws<FbError>(() => ExecA("select v from t")).Message);
        ta.Commit();
        lock (a.Sync) Assert.Equal(["ID", "V2", "PID", "W"], a.Tables["T"].Columns.Select(c => c.Name));

        foreach (var db in new[] { b, Database.FromSnapshot(snap, "c") })
        {
            var tx = new Attachment(db).StartTransaction();
            lock (db.Sync)
            {
                var t = db.Tables["T"];
                Assert.Equal(["ID", "V", "PID"], t.Columns.Select(c => c.Name));
                Assert.Null(t.Columns[0].Description);
                Assert.Single(t.ForeignKeys);
                Assert.Contains("T_V", t.Indexes);
                Assert.DoesNotContain("T_ID_DESC", t.Indexes);
                Assert.Empty(t.Triggers);
                Executor.Execute(tx, "insert into t (id) values (1)");
                Assert.Equal("x", Executor.Execute(tx, "select v from t").Rows![0][0].AsString);
                Assert.Equal(335544466, Assert.Throws<FbError>(() => Executor.Execute(tx, "insert into t (id, pid) values (2, 99)")).GdsCode);
            }
        }
    }

    [Fact]
    public void SnapshotClonesHaveTheirOwnGttRows()
    {
        Exec("create global temporary table g (id int) on commit preserve rows");
        _tx.Commit(retaining: true);
        var snap = _db.Snapshot();
        var a = Database.FromSnapshot(snap, "a");
        var b = Database.FromSnapshot(snap, "b");
        Assert.NotSame(a.Tables["G"].Rows, b.Tables["G"].Rows);
        var ta = new Attachment(a).StartTransaction();
        lock (a.Sync) Executor.Execute(ta, "insert into g values (1)");
        ta.Commit();
        var tb = new Attachment(b).StartTransaction();
        lock (b.Sync) Assert.Empty(Executor.Execute(tb, "select id from g").Rows!);
        Assert.Empty(snap.Tables["G"].Rows);
    }

    [Fact]
    public void SnapshotsOfAnUnchangedCloneShareTheCatalog()
    {
        Exec("create table t (id int not null primary key, v int)");
        Exec("insert into t values (1, 1)");
        _tx.Commit(retaining: true);
        var snap = _db.Snapshot();
        var clone = Database.FromSnapshot(snap, "c");
        var tx = new Attachment(clone).StartTransaction();
        lock (clone.Sync) Executor.Execute(tx, "insert into t values (2, 2)");
        tx.Commit();
        var seeded = clone.Snapshot();
        Assert.Same(snap.Tables["T"].Columns, seeded.Tables["T"].Columns);
        Assert.Single(snap.Tables["T"].Rows);
        var c2 = Database.FromSnapshot(seeded, "c2");
        var tx2 = new Attachment(c2).StartTransaction();
        lock (c2.Sync) Assert.Equal(2, Executor.Execute(tx2, "select id from t where id in (1, 2)").Rows!.Count);
        lock (c2.Sync) Assert.Single(Executor.Execute(tx2, "select id from t where id = 2").Rows!);
        // after DDL the clone's snapshot has its own definitions
        lock (c2.Sync) Executor.Execute(tx2, "alter table t add w int");
        tx2.Commit();
        var altered = c2.Snapshot();
        Assert.Equal(3, altered.Tables["T"].Columns.Count);
        Assert.Equal(2, snap.Tables["T"].Columns.Count);
    }

    [Fact]
    public void SnapshotClonesHaveTheirOwnGttIndexes()
    {
        Exec("create global temporary table g (id int not null primary key) on commit preserve rows");
        _tx.Commit(retaining: true);
        var snap = _db.Snapshot();
        var a = Database.FromSnapshot(snap, "a");
        var ta = new Attachment(a).StartTransaction();
        lock (a.Sync) Executor.Execute(ta, "insert into g values (1)");
        ta.Commit();
        // the clones' rows never reach the snapshot's index (which every other clone shares)
        Assert.All(snap.Indexes.Values.Where(ix => ix.Table == "G"), ix => Assert.Empty(ix.Map));
        var b = Database.FromSnapshot(snap, "b");
        var tb = new Attachment(b).StartTransaction();
        lock (b.Sync) Executor.Execute(tb, "insert into g values (1)");
        lock (b.Sync) Assert.Single(Executor.Execute(tb, "select id from g where id = 1").Rows!);
    }

    [Fact]
    public void SystemRelationsFollowDdlOfEachClone()
    {
        const string fields = "select trim(rdb$field_name) from rdb$relation_fields where rdb$relation_name = 'T' order by rdb$field_position";
        Exec("create table t (id int)");
        Assert.Equal(["ID"], Rows(fields).Select(r => r[0]));
        Exec("alter table t add v int");
        Assert.Equal(["ID", "V"], Rows(fields).Select(r => r[0]));
        _tx.Commit(retaining: true);
        var snap = _db.Snapshot();
        var a = Database.FromSnapshot(snap, "a");
        var b = Database.FromSnapshot(snap, "b");
        List<string?> Fields(Database db)
        {
            var tx = new Attachment(db).StartTransaction();
            lock (db.Sync) return Executor.Execute(tx, fields).Rows!.Select(r => r[0].AsString).ToList();
        }
        Assert.Equal(["ID", "V"], Fields(a));
        Assert.Equal(["ID", "V"], Fields(b));
        var ta = new Attachment(a).StartTransaction();
        lock (a.Sync) Executor.Execute(ta, "alter table t add w int");
        Assert.Equal(["ID", "V", "W"], Fields(a));
        Assert.Equal(["ID", "V"], Fields(b));
        Assert.Equal(["ID", "V"], Fields(Database.FromSnapshot(snap, "c")));
    }
}
