# Fakebird

An in-memory **fake Firebird 3 server** for fast integration tests in .NET.

Fakebird speaks the Firebird wire protocol on `127.0.0.1`, so your application keeps using the
**unchanged** `FirebirdSql.Data.FirebirdClient` — plain `FbConnection`/`FbCommand`, Dapper or
EF Core (`UseFirebird`). Only the connection string changes. All data lives in simple in-memory
structures: no disk, no journaling, no garbage collection of record versions.

```csharp
await using var server = FakebirdServer.Start();              // ephemeral port on 127.0.0.1

var template = server.CreateDatabase("template");
template.RunScript(File.ReadAllText("schema.sql"));           // isql-style script, SET TERM aware
var snapshot = template.Snapshot();                           // frozen committed state

// per test: an isolated copy-on-write clone (O(number of tables), microseconds)
var db = server.CreateDatabase($"test_{Guid.NewGuid():N}", snapshot);
var cs = db.ConnectionString();  // DataSource=127.0.0.1;Port=...;Database=test_...;User=SYSDBA;...

// or reuse one name and reset it between tests (pooled connections are re-attached transparently)
server.Restore("app", snapshot);
```

See `samples/SampleApi.Tests` for an ASP.NET Core `WebApplicationFactory` setup.

## Why it is fast

* No disk I/O; rows are immutable `Value[]` arrays, values are unboxed structs.
* Statements are compiled once into node trees and cached per database (`SQL text → plan`).
* Equality predicates use hash indexes (PK/UK/FK/user indexes); joins use index lookups or hash joins.
* **Copy-on-write snapshots**: a database created from a snapshot shares row storage and index
  maps; a table is copied only when a test first writes to it. Read-only tables are never copied.

## What is supported

Firebird 3, dialect 3 semantics:

* **Types**: SMALLINT, INTEGER, BIGINT, NUMERIC/DECIMAL (exact scaled integers), FLOAT, DOUBLE
  PRECISION, CHAR (blank padded), VARCHAR (length checked), DATE, TIME, TIMESTAMP, BOOLEAN,
  BLOB SUB_TYPE TEXT/BINARY, CHAR(16) CHARACTER SET OCTETS (GUIDs), collations `*_CI` / `*_CI_AI`.
* **DDL**: tables (incl. GTT), constraints (PK, UNIQUE, FK with CASCADE/SET NULL/SET DEFAULT/NO
  ACTION, CHECK, NOT NULL), defaults, identity and computed columns, ALTER TABLE (add/drop/alter
  column, constraints), indexes, views, domains, sequences/generators, exceptions, procedures,
  functions, triggers, CREATE OR ALTER / RECREATE / DROP. GRANT/REVOKE/COMMENT are accepted as no-ops.
* **Queries**: joins (inner/left/right/full/cross/natural/USING), derived tables, CTEs (incl.
  recursive), UNION [ALL], subqueries (scalar, IN, EXISTS, SINGULAR, ANY/ALL), GROUP BY/HAVING,
  aggregates (COUNT/SUM/AVG/MIN/MAX/LIST with DISTINCT), window functions, ORDER BY with NULLS
  FIRST/LAST, FIRST/SKIP, ROWS, OFFSET/FETCH, selectable procedures in FROM, RDB$ system tables.
* **DML**: INSERT (VALUES/SELECT/DEFAULT VALUES), UPDATE, DELETE, UPDATE OR INSERT, MERGE,
  RETURNING, EXECUTE PROCEDURE, EXECUTE BLOCK.
* **PSQL**: variables, cursors, IF/WHILE/FOR SELECT/FOR EXECUTE STATEMENT, LEAVE/CONTINUE/EXIT,
  SUSPEND, exceptions with WHEN handlers (EXCEPTION/SQLCODE/GDSCODE/SQLSTATE/ANY, with undo of the
  block), EXECUTE STATEMENT, IN AUTONOMOUS TRANSACTION, sub-procedures/functions, NEW/OLD,
  INSERTING/UPDATING/DELETING, ROW_COUNT, context variables, RDB$GET_CONTEXT/RDB$SET_CONTEXT.
* **Built-in functions**: string, numeric, date/time, UUID, bit, LIST, COALESCE/NULLIF/IIF/DECODE...
* **Transactions**: read committed (default) and snapshot isolation, no dirty reads, statement
  atomicity, savepoints, WAIT/NOWAIT lock conflicts with deadlock detection and lock timeouts.
* **Errors** carry real Firebird GDS codes and SQLSTATEs (unique/FK/check/not-null violations,
  string truncation, conversion errors, update conflicts, user exceptions), so `catch (FbException)`
  logic in your application behaves the same.
* **Client paths verified**: FbConnection/FbCommand/FbDataReader (parameters, blobs, GUIDs,
  booleans, output parameters, multiple open readers), FbTransaction (savepoints, isolation, wait
  options), FbConnection.CreateDatabase/DropDatabase, Dapper, EF Core (EnsureCreated, Migrate with
  the migration lock, queries, SaveChanges, transactions, raw SQL).

## Fail fast

Anything not implemented raises an error such as
`Fakebird: unsupported feature: PACKAGE` instead of silently doing something different.
Not supported: packages, database (ON CONNECT/TRANSACTION) triggers, DDL triggers, events,
the services API (backup/restore), external tables/functions, WHERE CURRENT OF, TIME ZONE /
INT128 / DECFLOAT types (Firebird 4), DML on views.

## Differences from a real server

* DDL is applied immediately (not transactional) and is never cached as a plan.
* Statement results are materialized at execute time.
* NO ACTION foreign keys are verified at the end of each statement.
* All text is treated as Unicode; columns are described to the client as UTF8.

## Debugging

Set `FAKEBIRD_LOG=1` (stderr) or `FAKEBIRD_LOG=/path/to/file` to log every failed statement with
its SQL text and error, or pass `FakebirdOptions.Log`.

## Layout

```
src/Fakebird/Sql       lexer, AST, recursive-descent parser (DSQL + PSQL)
src/Fakebird/Types     SqlType, Value, conversions and comparisons
src/Fakebird/Compile   binder/compiler: name resolution, type inference, join planning
src/Fakebird/Exec      expression nodes, relational operators, DML pipeline, PSQL runtime
src/Fakebird/Engine    catalog, storage (row versions, indexes, COW), transactions, DDL, RDB$ tables
src/Fakebird/Wire      Firebird wire protocol (versions 10-12) server
tests/Fakebird.Tests   engine, wire, SQL feature, client behaviour, Dapper and EF Core tests
samples/               ASP.NET Core API + WebApplicationFactory tests using Fakebird
bench/                 throughput checks through the real client
```
