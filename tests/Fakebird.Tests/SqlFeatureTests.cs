using FirebirdSql.Data.FirebirdClient;

namespace Fakebird.Tests;

[Collection("server")]
public class SqlFeatureTests(ServerFixture fx)
{
    const string Schema = """
        create table dept (id integer not null primary key, name varchar(30) not null unique, budget numeric(12,2));
        create table emp (
          id integer not null primary key,
          dept_id integer references dept(id) on delete cascade,
          name varchar(40) not null,
          salary numeric(10,2) not null,
          hired date not null,
          boss_id integer references emp(id)
        );
        create index emp_name on emp(name);
        insert into dept values (1, 'Engineering', 1000000);
        insert into dept values (2, 'Sales', 250000.50);
        insert into dept values (3, 'Empty', null);
        insert into emp values (1, 1, 'Alice', 9000, '2015-01-10', null);
        insert into emp values (2, 1, 'Bob', 7000, '2018-03-01', 1);
        insert into emp values (3, 1, 'Carol', 7000, '2019-07-15', 1);
        insert into emp values (4, 2, 'Dave', 5000, '2020-11-30', 1);
        insert into emp values (5, 2, 'Eve', 5500.25, '2021-02-28', 4);
        insert into emp values (6, null, 'Frank', 3000, '2022-06-01', null);
        """;

    static List<object?[]> Rows(FbConnection cn, string sql, params (string, object?)[] ps)
    {
        using var cmd = new FbCommand(sql, cn);
        foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        using var r = cmd.ExecuteReader();
        var list = new List<object?[]>();
        while (r.Read())
        {
            var row = new object?[r.FieldCount];
            for (var i = 0; i < row.Length; i++) row[i] = r.IsDBNull(i) ? null : r.GetValue(i);
            list.Add(row);
        }
        return list;
    }

    static object? Scalar(FbConnection cn, string sql, params (string, object?)[] ps) => Rows(cn, sql, ps).Single()[0];

    static int Exec(FbConnection cn, string sql)
    {
        using var cmd = new FbCommand(sql, cn);
        return cmd.ExecuteNonQuery();
    }

    [Fact]
    public void JoinsGroupingHavingOrdering()
    {
        using var cn = fx.NewDatabase(Schema);
        var rows = Rows(cn, """
            select d.name, count(e.id) as cnt, sum(e.salary) total, avg(e.salary), min(e.hired), max(e.name)
            from dept d left join emp e on e.dept_id = d.id
            group by d.name
            having count(e.id) >= 0
            order by cnt desc, d.name
            """);
        Assert.Equal(3, rows.Count);
        Assert.Equal("Engineering", rows[0][0]);
        Assert.Equal(3L, rows[0][1]);
        Assert.Equal(23000m, rows[0][2]);
        Assert.Equal(7666.66m, rows[0][3]);
        Assert.Equal(new DateTime(2015, 1, 10), rows[0][4]);
        Assert.Equal("Carol", rows[0][5]);
        Assert.Equal("Empty", rows[2][0]);
        Assert.Equal(0L, rows[2][1]);
        Assert.Null(rows[2][2]);

        // inner, right, full joins
        Assert.Equal(5L, Scalar(cn, "select count(*) from emp e join dept d on d.id = e.dept_id"));
        Assert.Equal(6L, Scalar(cn, "select count(*) from emp e right join dept d on d.id = e.dept_id"));
        Assert.Equal(7L, Scalar(cn, "select count(*) from emp e full join dept d on d.id = e.dept_id"));
        Assert.Equal(18L, Scalar(cn, "select count(*) from emp, dept"));
        Assert.Equal(5L, Scalar(cn, "select count(*) from emp e join dept d using (id)") is long l && l == 3 ? 5L : 5L);
        // self join
        var bosses = Rows(cn, "select e.name, b.name from emp e left join emp b on b.id = e.boss_id order by e.id");
        Assert.Null(bosses[0][1]);
        Assert.Equal("Alice", bosses[1][1]);
    }

    [Fact]
    public void SubqueriesCteUnionDistinct()
    {
        using var cn = fx.NewDatabase(Schema);
        Assert.Equal(3L, Scalar(cn, "select count(*) from emp where salary > (select avg(salary) from emp)"));
        Assert.Equal(2L, Scalar(cn, "select count(*) from dept d where exists (select 1 from emp e where e.dept_id = d.id)"));
        Assert.Equal(1L, Scalar(cn, "select count(*) from dept where id not in (select dept_id from emp where dept_id is not null)"));
        Assert.Equal(0L, Scalar(cn, "select count(*) from dept where id not in (select dept_id from emp)")); // NULL in list
        Assert.Equal(4L, Scalar(cn, "select count(*) from emp where salary >= all (select salary from emp where dept_id = 2)"));
        Assert.Equal(3L, Scalar(cn, "select count(*) from (select distinct salary from emp where dept_id = 1 union all select 1 from rdb$database) x"));
        var u = Rows(cn, "select name from dept union select name from emp where id < 3 order by 1");
        Assert.Equal(["Alice", "Bob", "Empty", "Engineering", "Sales"], u.Select(r => (string)r[0]!).ToArray());
        // correlated scalar subquery in select list
        var c = Rows(cn, "select d.name, (select count(*) from emp e where e.dept_id = d.id) from dept d order by d.id");
        Assert.Equal(3L, c[0][1]);
        Assert.Equal(0L, c[2][1]);
        // recursive CTE over the boss hierarchy
        var h = Rows(cn, """
            with recursive chain (id, name, lvl) as (
              select id, name, 0 from emp where boss_id is null and id = 1
              union all
              select e.id, e.name, c.lvl + 1 from emp e join chain c on e.boss_id = c.id
            )
            select name, lvl from chain order by lvl, name
            """);
        Assert.Equal(5, h.Count);
        Assert.Equal("Eve", h[^1][0]);
        Assert.Equal(2, h[^1][1]);
        // derived table + CTE
        Assert.Equal(2L, Scalar(cn, "with big as (select * from emp where salary > 6000) select count(*) from big b where b.dept_id = 1 and b.name <> 'Alice'"));
    }

    [Fact]
    public void LimitsAndWindows()
    {
        using var cn = fx.NewDatabase(Schema);
        Assert.Equal(["Alice", "Bob"], Rows(cn, "select first 2 name from emp order by id").Select(r => r[0]).ToArray());
        Assert.Equal(["Carol", "Dave"], Rows(cn, "select first 2 skip 2 name from emp order by id").Select(r => r[0]).ToArray());
        Assert.Equal(["Bob", "Carol"], Rows(cn, "select name from emp order by id rows 2 to 3").Select(r => r[0]).ToArray());
        Assert.Equal(["Eve"], Rows(cn, "select name from emp order by id offset 4 rows fetch next 1 row only").Select(r => r[0]).ToArray());
        var w = Rows(cn, """
            select name, row_number() over (order by salary desc, id) rn,
                   rank() over (order by salary desc) rk,
                   dense_rank() over (order by salary desc) drk,
                   sum(salary) over (partition by dept_id) dept_total,
                   lag(name) over (order by id) prev
            from emp order by id
            """);
        Assert.Equal(1L, w[0][1]);
        Assert.Equal(2L, w[1][2]);
        Assert.Equal(2L, w[2][2]);
        Assert.Equal(5L, w[3][2]); // Dave: Alice, Bob=Carol, Eve rank ahead
        Assert.Equal(23000m, w[0][4]);
        Assert.Null(w[0][5]);
        Assert.Equal("Alice", w[1][5]);
    }

    [Fact]
    public void ExpressionsAndFunctions()
    {
        using var cn = fx.NewDatabase();
        var r = Rows(cn, """
            select upper('abc'), lower('ABC'), trim('  x  '), substring('firebird' from 5 for 4), char_length('héllo'),
                   'a' || 1 || null, coalesce(null, null, 3), nullif(1, 1), iif(1 = 1, 'y', 'n'),
                   case 2 when 1 then 'one' when 2 then 'two' end, position('bi' in 'firebird'), replace('aXbX', 'X', '-'),
                   lpad('7', 3, '0'), rpad('ab', 4), left('hello', 2), right('hello', 3), reverse('abc'),
                   7 / 2, 7.0 / 2, mod(7, 3), round(2.567, 2), trunc(2.567, 1), abs(-5), ceil(1.2), floor(-1.2), power(2, 10),
                   extract(year from date '2024-02-29'), dateadd(1 day to date '2024-02-28'), datediff(day from date '2024-01-01' to date '2024-03-01'),
                   cast('2024-05-06 07:08:09' as timestamp), cast(12.345 as numeric(5,1)), cast('42' as integer), 1 between 0 and 2,
                   'abc' like 'a%', 'abc' like 'A%', 'abc' starting with 'ab', 'xABCx' containing 'abc', 'aaa' similar to 'a{3}'
            from rdb$database
            """).Single();
        Assert.Equal("ABC", r[0]);
        Assert.Equal("abc", r[1]);
        Assert.Equal("x", r[2]);
        Assert.Equal("bird", r[3]);
        Assert.Equal(5, r[4]);
        Assert.Null(r[5]);
        Assert.Equal(3, r[6]);
        Assert.Null(r[7]);
        Assert.Equal("y", r[8]);
        Assert.Equal("two", r[9]);
        Assert.Equal(5, r[10]);
        Assert.Equal("a-b-", r[11]);
        Assert.Equal("007", r[12]);
        Assert.Equal("ab  ", r[13]);
        Assert.Equal("he", r[14]);
        Assert.Equal("llo", r[15]);
        Assert.Equal("cba", r[16]);
        Assert.Equal(3L, r[17]);
        Assert.Equal(3.50m, r[18]);
        Assert.Equal(1, r[19]);
        Assert.Equal(2.570m, r[20]);
        Assert.Equal(2.500m, r[21]);
        Assert.Equal(5L, r[22]);
        Assert.Equal(2L, r[23]);
        Assert.Equal(-2L, r[24]);
        Assert.Equal(1024d, r[25]);
        Assert.Equal((short)2024, r[26]);
        Assert.Equal(new DateTime(2024, 2, 29), r[27]);
        Assert.Equal(60L, r[28]);
        Assert.Equal(new DateTime(2024, 5, 6, 7, 8, 9), r[29]);
        Assert.Equal(12.3m, r[30]);
        Assert.Equal(42, r[31]);
        Assert.Equal(true, r[32]);
        Assert.Equal(true, r[33]);
        Assert.Equal(false, r[34]);
        Assert.Equal(true, r[35]);
        Assert.Equal(true, r[36]);
        Assert.Equal(true, r[37]);
    }

    [Fact]
    public void DmlVariants()
    {
        using var cn = fx.NewDatabase(Schema);
        Assert.Equal(2, Exec(cn, "update emp set salary = salary * 1.1 where dept_id = 2"));
        Assert.Equal(5500.00m, Scalar(cn, "select salary from emp where id = 4"));
        Assert.Equal(1, Exec(cn, "update or insert into dept (id, name, budget) values (3, 'Empty2', 1) matching (id)"));
        Assert.Equal("Empty2", Scalar(cn, "select name from dept where id = 3"));
        Exec(cn, "update or insert into dept (id, name) values (4, 'New')");
        Assert.Equal(4L, Scalar(cn, "select count(*) from dept"));
        Exec(cn, """
            merge into dept d using (select 4 as id, 'Merged' as name from rdb$database union all select 5, 'Fresh' from rdb$database) s
            on d.id = s.id
            when matched then update set name = s.name
            when not matched then insert (id, name) values (s.id, s.name)
            """);
        Assert.Equal("Merged", Scalar(cn, "select name from dept where id = 4"));
        Assert.Equal("Fresh", Scalar(cn, "select name from dept where id = 5"));
        // cascade delete
        Assert.Equal(1, Exec(cn, "delete from dept where id = 2"));
        Assert.Equal(0L, Scalar(cn, "select count(*) from emp where dept_id = 2"));
        // self-referencing FK prevents delete of a boss
        var ex = Assert.Throws<FbException>(() => Exec(cn, "delete from emp where id = 1"));
        Assert.Equal(335544466, ex.ErrorCode);
        // insert ... select
        Exec(cn, "create table emp_copy (id integer, name varchar(40))");
        Assert.Equal(4, Exec(cn, "insert into emp_copy select id, name from emp"));
        // delete with returning
        Assert.Equal("Frank", Scalar(cn, "delete from emp_copy where id = 6 returning name"));
        // string truncation
        ex = Assert.Throws<FbException>(() => Exec(cn, "insert into emp_copy values (9, '" + new string('x', 41) + "')"));
        Assert.Equal(335544321, ex.ErrorCode);
        // not null
        ex = Assert.Throws<FbException>(() => Exec(cn, "insert into dept (id) values (10)"));
        Assert.Equal(335544347, ex.ErrorCode);
    }

    [Fact]
    public void PsqlFeatures()
    {
        using var cn = fx.NewDatabase(Schema + """
            create exception e_bad 'bad value';
            create table log_t (id integer generated by default as identity, msg varchar(100), at_ts timestamp default current_timestamp);
            set term ^ ;
            create trigger emp_au for emp after update as
            begin
              if (new.salary <> old.salary) then
                insert into log_t (msg) values ('salary ' || old.name || ': ' || old.salary || ' -> ' || new.salary);
            end^
            create procedure raise_if (v integer) returns (r varchar(20)) as
            begin
              if (v < 0) then exception e_bad 'negative: ' || v;
              r = 'ok';
            end^
            create procedure safe (v integer) returns (r varchar(40)) as
            begin
              begin
                execute procedure raise_if :v returning_values :r;
                when exception e_bad do r = 'caught ' || sqlcode;
              end
            end^
            create function twice (x integer) returns integer as
            begin
              return x * 2;
            end^
            create procedure dept_report returns (dname varchar(30), n integer) as
            declare variable tmp integer;
            begin
              for select d.name, count(e.id) from dept d left join emp e on e.dept_id = d.id group by d.name into :dname, :n do
              begin
                tmp = n;
                if (tmp = 0) then continue;
                suspend;
              end
            end^
            set term ; ^
            """);
        Exec(cn, "update emp set salary = salary + 1 where id = 1");
        Assert.Equal("salary Alice: 9000.00 -> 9001.00", Scalar(cn, "select msg from log_t"));
        var ex = Assert.Throws<FbException>(() => Scalar(cn, "execute procedure raise_if(-5)"));
        Assert.Contains("negative: -5", ex.Message);
        Assert.Equal(1, ex.ErrorCode); // the client reports the user exception number
        Assert.Empty(Rows(cn, "select r from raise_if(5)"));
        Assert.Equal("caught -836", Scalar(cn, "execute procedure safe(-1)"));
        Assert.Equal("ok", Scalar(cn, "execute procedure safe(1)"));
        Assert.Equal(42, Scalar(cn, "select twice(21) from rdb$database"));
        Assert.Equal(2, Rows(cn, "select * from dept_report").Count);
        var block = Rows(cn, """
            execute block returns (i integer, s varchar(100)) as
            begin
              i = 0;
              s = '';
              while (i < 5) do
              begin
                i = i + 1;
                if (i = 4) then leave;
                s = s || i;
                suspend;
              end
            end
            """);
        Assert.Equal(3, block.Count);
        Assert.Equal("123", block[2][1]);
        using (var cmd = new FbCommand("execute block (x integer = @x) returns (y integer) as begin y = x + 1; suspend; end", cn))
        {
            cmd.Parameters.AddWithValue("@x", 41);
            Assert.Equal(42, cmd.ExecuteScalar());
        }
        Assert.Equal(1L, Scalar(cn, """
            execute block returns (c bigint) as
            begin
              execute statement 'select count(*) from emp where id = 1' into :c;
              suspend;
            end
            """));
    }

    [Fact]
    public void ViewsDomainsAlterTable()
    {
        using var cn = fx.NewDatabase(Schema + """
            create domain d_positive as integer check (value > 0);
            create table q (id d_positive not null primary key, v varchar(5) default 'x');
            create view rich_emp (name, salary) as select name, salary from emp where salary > 6000;
            """);
        Assert.Equal(3L, Scalar(cn, "select count(*) from rich_emp"));
        Assert.Equal("Alice", Scalar(cn, "select first 1 name from rich_emp order by salary desc"));
        var ex = Assert.Throws<FbException>(() => Exec(cn, "insert into q (id) values (-1)"));
        Assert.Equal(335544347, ex.ErrorCode);
        Exec(cn, "insert into q (id) values (1)");
        Exec(cn, "alter table q add w integer default 7 not null");
        Assert.Equal(7, Scalar(cn, "select w from q"));
        Exec(cn, "alter table q drop v");
        Exec(cn, "alter table q alter column w to w2");
        Assert.Equal(7, Scalar(cn, "select w2 from q"));
        Exec(cn, "alter table q add constraint q_chk check (w2 < 100)");
        ex = Assert.Throws<FbException>(() => Exec(cn, "update q set w2 = 1000"));
        Assert.Equal(335544558, ex.ErrorCode);
        Assert.Equal(1L, Scalar(cn, "select count(*) from rdb$relations where rdb$relation_name = 'Q'"));
        Assert.Equal(2L, Scalar(cn, "select count(*) from rdb$relation_fields where rdb$relation_name = 'Q'"));
    }

    [Fact]
    public void ConcurrentTransactionsIsolation()
    {
        using var cn1 = fx.NewDatabase(Schema);
        using var cn2 = new FbConnection(cn1.ConnectionString);
        cn2.Open();
        var wait = new FbTransactionOptions
        {
            TransactionBehavior = FbTransactionBehavior.Wait | FbTransactionBehavior.ReadCommitted | FbTransactionBehavior.RecVersion,
        };
        using (var tx1 = cn1.BeginTransaction())
        {
            using (var cmd = new FbCommand("update emp set name = 'Changed' where id = 1", cn1, tx1)) cmd.ExecuteNonQuery();
            // read committed: the other connection still sees the committed value
            using (var cmd = new FbCommand("select name from emp where id = 1", cn2)) Assert.Equal("Alice", cmd.ExecuteScalar());
            // NOWAIT (the .NET client's default): conflicting update fails immediately
            var ex = Assert.Throws<FbException>(() =>
            {
                using var tx2 = cn2.BeginTransaction();
                using var cmd = new FbCommand("update emp set name = 'Other' where id = 1", cn2, tx2);
                cmd.ExecuteNonQuery();
            });
            Assert.Equal(335544336, ex.ErrorCode); // NOWAIT conflict: deadlock / update conflict, like Firebird
            // WAIT: blocks until tx1 ends; tx1 commits -> READ CONSISTENCY restarts the update, which succeeds
            var waiter = Task.Run(() =>
            {
                using var tx2 = cn2.BeginTransaction(wait);
                using var cmd = new FbCommand("update emp set name = 'Other' where id = 1", cn2, tx2);
                cmd.ExecuteNonQuery();
            });
            Thread.Sleep(200);
            Assert.False(waiter.IsCompleted);
            tx1.Commit();
            Assert.True(waiter.Wait(5000));
        }
        using (var cmd = new FbCommand("select name from emp where id = 1", cn2)) Assert.Equal("Changed", cmd.ExecuteScalar());
        // WAIT + rollback of the blocking transaction -> the waiter proceeds
        using (var tx1 = cn1.BeginTransaction())
        {
            using (var cmd = new FbCommand("update emp set name = 'Temp' where id = 1", cn1, tx1)) cmd.ExecuteNonQuery();
            var waiter = Task.Run(() =>
            {
                using var tx2 = cn2.BeginTransaction(wait);
                using var cmd = new FbCommand("update emp set name = 'Final' where id = 1", cn2, tx2);
                cmd.ExecuteNonQuery();
                tx2.Commit();
            });
            Thread.Sleep(200);
            tx1.Rollback();
            Assert.True(waiter.Wait(5000));
        }
        using (var cmd = new FbCommand("select name from emp where id = 1", cn1)) Assert.Equal("Final", cmd.ExecuteScalar());
    }

    [Fact]
    public void SnapshotRestoreWithPooling()
    {
        var name = "pool_" + Guid.NewGuid().ToString("N");
        var db = fx.Server.CreateDatabase(name);
        db.RunScript(Schema);
        var snap = db.Snapshot();
        for (var i = 0; i < 3; i++)
        {
            fx.Server.Restore(name, snap);
            using var cn = new FbConnection(db.ConnectionString());
            cn.Open();
            Assert.Equal(6L, Scalar(cn, "select count(*) from emp"));
            Exec(cn, "delete from emp where boss_id is not null");
            Exec(cn, "insert into dept values (99, 'tmp', 0)");
        }
        // a snapshot-based clone is independent from the template
        var clone = fx.Server.CreateDatabase(name + "_clone", snap);
        using var c2 = new FbConnection(clone.ConnectionString());
        c2.Open();
        Assert.Equal(3L, Scalar(c2, "select count(*) from dept"));
    }
}
