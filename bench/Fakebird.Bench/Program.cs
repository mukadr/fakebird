using System.Diagnostics;
using Fakebird;
using FirebirdSql.Data.FirebirdClient;

// Quick throughput check of common integration-test operations through the real client.
if (args.Length > 0 && args[0] == "compile") { CompileBench.Run(); return; }
using var server = FakebirdServer.Start();
var template = server.CreateDatabase("template");
template.RunScript("""
    create table customer (id integer not null primary key, name varchar(100) not null, email varchar(200), created timestamp default current_timestamp);
    create table orders (id integer not null primary key, customer_id integer not null references customer(id), total numeric(12,2), status varchar(10));
    create index orders_status on orders(status);
    """);
var sw = Stopwatch.StartNew();
{
    using var cn = new FbConnection(template.ConnectionString());
    cn.Open();
    using var tx = cn.BeginTransaction();
    using var c1 = new FbCommand("insert into customer (id, name, email) values (@id, @name, @email)", cn, tx);
    c1.Parameters.Add("@id", FbDbType.Integer);
    c1.Parameters.Add("@name", FbDbType.VarChar);
    c1.Parameters.Add("@email", FbDbType.VarChar);
    for (var i = 1; i <= 10_000; i++)
    {
        c1.Parameters[0].Value = i;
        c1.Parameters[1].Value = "customer " + i;
        c1.Parameters[2].Value = $"c{i}@example.com";
        c1.ExecuteNonQuery();
    }
    using var c2 = new FbCommand("insert into orders values (@id, @cid, @total, @status)", cn, tx);
    c2.Parameters.Add("@id", FbDbType.Integer);
    c2.Parameters.Add("@cid", FbDbType.Integer);
    c2.Parameters.Add("@total", FbDbType.Decimal);
    c2.Parameters.Add("@status", FbDbType.VarChar);
    for (var i = 1; i <= 50_000; i++)
    {
        c2.Parameters[0].Value = i;
        c2.Parameters[1].Value = i % 10_000 + 1;
        c2.Parameters[2].Value = i * 0.5m;
        c2.Parameters[3].Value = i % 7 == 0 ? "open" : "closed";
        c2.ExecuteNonQuery();
    }
    tx.Commit();
}
Console.WriteLine($"seed 60k rows: {sw.ElapsedMilliseconds} ms");

var snap = template.Snapshot();
sw.Restart();
for (var i = 0; i < 1000; i++) server.CreateDatabase("t" + i, snap);
Console.WriteLine($"1000 COW clones of a 60k-row database: {sw.ElapsedMilliseconds} ms");

var db = server.GetDatabase("t1");
using (var cn = new FbConnection(db.ConnectionString()))
{
    cn.Open();
    sw.Restart();
    for (var i = 0; i < 20_000; i++)
    {
        using var cmd = new FbCommand("select c.name, count(o.id), sum(o.total) from customer c join orders o on o.customer_id = c.id where c.id = @id group by c.name", cn);
        cmd.Parameters.AddWithValue("@id", i % 10_000 + 1);
        using var r = cmd.ExecuteReader();
        while (r.Read()) { }
    }
    Console.WriteLine($"20k PK join+group queries: {sw.ElapsedMilliseconds} ms ({20_000 * 1000.0 / sw.ElapsedMilliseconds:F0}/s)");
    sw.Restart();
    for (var i = 0; i < 200; i++)
    {
        using var cmd = new FbCommand("select status, count(*), sum(total) from orders o join customer c on c.id = o.customer_id where c.name starting with 'customer 1' group by status", cn);
        using var r = cmd.ExecuteReader();
        while (r.Read()) { }
    }
    Console.WriteLine($"200 full-scan hash-join aggregations over 50k rows: {sw.ElapsedMilliseconds} ms");
    sw.Restart();
    for (var i = 0; i < 2_000; i++)
    {
        using var tx = cn.BeginTransaction();
        using var cmd = new FbCommand("update orders set status = 'x' where customer_id = @c", cn, tx);
        cmd.Parameters.AddWithValue("@c", i % 10_000 + 1);
        cmd.ExecuteNonQuery();
        tx.Commit();
    }
    Console.WriteLine($"2k update transactions (FK index lookup): {sw.ElapsedMilliseconds} ms");
}
sw.Restart();
for (var i = 0; i < 1000; i++)
{
    using var cn = new FbConnection(server.GetDatabase("t" + (i % 1000)).ConnectionString());
    cn.Open();
    using var cmd = new FbCommand("select count(*) from customer", cn);
    cmd.ExecuteScalar();
}
Console.WriteLine($"1000 open+query on distinct databases: {sw.ElapsedMilliseconds} ms");

sw.Restart();
for (var i = 0; i < 300; i++)
{
    using var cn = new FbConnection(server.GetDatabase("t" + (i % 1000)).ConnectionString("Pooling=false"));
    cn.Open();
}
Console.WriteLine($"300 opens (no pooling): {sw.ElapsedMilliseconds} ms");
sw.Restart();
for (var i = 300; i < 600; i++)
{
    using var cn = new FbConnection(server.GetDatabase("t" + (i % 1000)).ConnectionString());
    cn.Open();
}
Console.WriteLine($"300 opens (pooling, new pool each): {sw.ElapsedMilliseconds} ms");
using (var cn = new FbConnection(server.GetDatabase("t999").ConnectionString()))
{
    cn.Open();
    sw.Restart();
    for (var i = 0; i < 2000; i++)
    {
        using var cmd = new FbCommand("select count(*) from customer", cn);
        cmd.ExecuteScalar();
    }
    Console.WriteLine($"2000 count(*) over 10k rows: {sw.ElapsedMilliseconds} ms");
    sw.Restart();
    for (var i = 0; i < 2000; i++)
    {
        using var cmd = new FbCommand("select 1 from rdb$database", cn);
        cmd.ExecuteScalar();
    }
    Console.WriteLine($"2000 trivial queries: {sw.ElapsedMilliseconds} ms");
}
