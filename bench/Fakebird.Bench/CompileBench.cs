using System.Diagnostics;
using Fakebird.Compile;
using Fakebird.Engine;

static class CompileBench
{
    public static void Run()
    {
        var db = new Database("c");
        Fakebird.ScriptRunner.Run(db, """
            create table "Blogs" ("Id" integer not null primary key, "Name" varchar(100), "Rating" numeric(5,2), "Created" timestamp);
            create table "Posts" ("Id" bigint not null primary key, "BlogId" integer references "Blogs"("Id"), "Title" varchar(200), "Body" blob sub_type text);
            """);
        var sql = """
            SELECT "b"."Id", "b"."Name", (SELECT COUNT(*) FROM "Posts" AS "p" WHERE "b"."Id" = "p"."BlogId") AS "Count"
            FROM "Blogs" AS "b"
            WHERE "b"."Rating" > CAST(1 AS NUMERIC(5,2)) AND "b"."Name" LIKE _UTF8'%x%'
            ORDER BY "b"."Name"
            ROWS (? + 1) TO (? + ?)
            """;
        Executor.Init();
        for (var i = 0; i < 2000; i++) { db.PlanCache.Clear(); lock (db.Sync) Executor.Prepare(db, sql); }
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 20000; i++) { db.PlanCache.Clear(); lock (db.Sync) Executor.Prepare(db, sql); }
        Console.WriteLine($"compile EF-style query: {sw.Elapsed.TotalMicroseconds / 20000:F1} µs");
    }
}
