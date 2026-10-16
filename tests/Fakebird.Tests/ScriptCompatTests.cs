namespace Fakebird.Tests;

/// <summary>
/// Constructs found in real-world schema scripts (each one first failed there). Expected behavior was
/// checked against Firebird 3.0.
/// </summary>
[Collection("server")]
public class ScriptCompatTests(ServerFixture fx)
{
    FakeDatabase NewDb() => fx.Server.CreateDatabase("compat_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void DefaultThatDoesNotConvertFailsOnlyWhenUsed()
    {
        var db = NewDb();
        db.RunScript("""
            create domain tts as timestamp;
            create table x (id integer, d tts default 0, e timestamp default 'abc');
            insert into x (id, d, e) values (1, null, null);
            """);
        var e = Assert.Throws<FbError>(() => db.Execute("insert into x (id) values (2)"));
        Assert.Equal(335544334, e.GdsCode);
    }

    [Fact]
    public void GroupByExpressionsByPositionOrText()
    {
        var db = NewDb();
        db.RunScript("""
            create table t (a integer, b integer, c varchar(10));
            insert into t values (1, 10, null);
            insert into t values (1, 5, 'k');
            insert into t values (2, 7, null);
            """);
        var byPos = db.Execute("select 'V' tipo, a, coalesce(c, 'x') m, sum(b) s from t group by 1, 2, 3 order by 2, 3");
        Assert.Equal(3, byPos.Count);
        Assert.Equal(["V", 1L, "k", 5L], byPos[0]);
        Assert.Equal(["V", 1L, "x", 10L], byPos[1]);
        var byText = db.Execute("select upper(coalesce(c, 'x')), count(*) from t group by upper(coalesce(c, 'x')) order by 1");
        Assert.Equal(["K", 1L], byText[0]);
        Assert.Equal(["X", 2L], byText[1]);
        // aggregates over expressions without a canonical form (IS NULL inside IIF)
        Assert.Equal(17L, db.Execute("select sum(iif(c is null, b, 0)) from t").Single()[0]);
    }

    [Fact]
    public void DatePlusTimeIsTimestamp()
    {
        var db = NewDb();
        var r = db.Execute("select date '2024-02-29' + time '13:14:15', time '01:00:00' + date '2024-01-01' from rdb$database").Single();
        Assert.Equal(new DateTime(2024, 2, 29, 13, 14, 15), r[0]);
        Assert.Equal(new DateTime(2024, 1, 1, 1, 0, 0), r[1]);
    }

    [Fact]
    public void ExecuteStatementWithExpressionAndNamedParameters()
    {
        var db = NewDb();
        db.RunScript("""
            create table t (id integer, s varchar(10));
            set term ^;
            create procedure p (pid integer) returns (r varchar(10)) as
            begin
              execute statement 'insert into t (id, s) values (' || :pid || ', ''a'')';
              execute statement ('update t set s = :novo where id = :id and :novo is not null') (novo := 'b', id := :pid);
              execute statement 'select s from t where id = ' || :pid into :r;
              suspend;
            end^
            set term ;^
            """);
        Assert.Equal("b", db.Execute("select r from p(5)").Single()[0]);
    }

    [Fact]
    public void QualifiedColumnsInInsertLists()
    {
        var db = NewDb();
        db.RunScript("""
            create table t (id integer primary key, s varchar(10));
            insert into t (t.id, t.s) values (1, 'a');
            update or insert into t (t.id, s) values (1, 'b') matching (t.id);
            merge into t using rdb$database on t.id = 2 when not matched then insert (t.id, t.s) values (2, 'c');
            """);
        Assert.Equal([[1L, "b"], [2L, "c"]], db.Execute("select id, s from t order by id"));
    }

    [Fact]
    public void DbKeyFollowsInsertionOrder()
    {
        var db = NewDb();
        db.RunScript("""
            create table t (id integer);
            insert into t values (30);
            insert into t values (10);
            insert into t values (20);
            """);
        var rows = db.Execute("select id, row_number() over (order by t.rdb$db_key) from t order by rdb$db_key");
        Assert.Equal([[30L, 1L], [10L, 2L], [20L, 3L]], rows);
        Assert.Equal(8, ((byte[])db.Execute("select first 1 rdb$db_key from t").Single()[0]!).Length);
    }

    [Fact]
    public void RowMayReferenceItself()
    {
        var db = NewDb();
        db.RunScript("""
            create table c (id integer not null primary key, other integer);
            alter table c add constraint r1 foreign key (id) references c (id);
            alter table c add constraint r2 foreign key (other) references c (id);
            insert into c (id) values (1);
            insert into c (id, other) values (2, 2);
            """);
        var e = Assert.Throws<FbError>(() => db.Execute("insert into c (id, other) values (3, 9)"));
        Assert.Equal(335544466, e.GdsCode);
    }

    [Fact]
    public void AddNotNullColumnFillsExistingRowsWithDefault()
    {
        var db = NewDb();
        db.RunScript("""
            create domain tb as char(1) default 'F' not null check (value in ('F', 'T'));
            create domain td as char(1) default 'F';
            create table r (id integer);
            insert into r values (1);
            alter table r add a tb;
            alter table r add b char(1) default 'X' not null;
            alter table r add c char(1) default 'X';
            alter table r add d td;
            """);
        Assert.Equal([1L, "F", "X", null, null], db.Execute("select id, a, b, c, d from r").Single());
        var e = Assert.Throws<FbScriptException>(() => db.RunScript("alter table r add e integer not null"));
        Assert.Contains("NULLs present", e.Message);
    }
}
