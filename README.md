# Fakebird

An in-memory **fake Firebird 5 server** for fast integration tests in .NET.

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

If your application builds its own connection string and has no port setting, put the port in the
database name, as the client allows: `DataSource=127.0.0.1;Database=127.0.0.1/<port>:<name>`.

## Built for isolated tests

Fakebird exists to turn integration tests into unit-test-like tests: every test gets its own
database (a clone or a `Restore`), so no test ever sees another test's data. Tests need no cleanup
code, can run fully in parallel (one database per test class or per test), and the engine favours
simplicity and speed over the machinery a shared production server needs.

## Why it is fast

* No disk I/O; rows are immutable `Value[]` arrays, values are unboxed structs.
* Statements are compiled once into node trees and cached per database (`SQL text → plan`).
* Equality predicates use hash indexes (PK/UK/FK/user indexes); joins use index lookups or hash joins.
* **Copy-on-write snapshots**: a database created from a snapshot shares row storage and index
  maps; a table is copied only when a test first writes to it. Read-only tables are never copied.

## What is supported

Firebird 5, dialect 3 semantics, as a server configured with `DataTypeCompatibility = 3.0` shows them
(the Firebird 4+ types INT128, DECFLOAT and TIME ZONE are not available yet). Every behaviour below is
checked against a real Firebird 5 by the corpus (see "Checking against a real Firebird"):

* **Types**: SMALLINT, INTEGER, BIGINT, NUMERIC/DECIMAL (exact scaled integers), FLOAT, DOUBLE
  PRECISION, CHAR (blank padded), VARCHAR (length checked), DATE, TIME, TIMESTAMP, BOOLEAN,
  BLOB SUB_TYPE TEXT/BINARY, CHAR(16) CHARACTER SET OCTETS (GUIDs), collations `*_CI` / `*_CI_AI`.
* **DDL**: tables (incl. GTT), constraints (PK, UNIQUE, FK with CASCADE/SET NULL/SET DEFAULT/NO
  ACTION, CHECK, NOT NULL), defaults, identity and computed columns, ALTER TABLE (add/drop/alter
  column, constraints), indexes, views, domains, sequences/generators, exceptions, procedures,
  functions, triggers, CREATE OR ALTER / RECREATE / DROP. GRANT/REVOKE/COMMENT are accepted as no-ops.
  COMPUTED BY columns are evaluated when read (they follow later changes to the rows and tables they
  read, including subqueries over their own table).
* **Queries**: joins (inner/left/right/full/cross/natural/USING), derived tables, CTEs (incl.
  recursive), UNION [ALL], subqueries (scalar, IN, EXISTS, SINGULAR, ANY/ALL), GROUP BY/HAVING
  (by column, expression, position or select-list alias),
  aggregates (COUNT/SUM/AVG/MIN/MAX/LIST with DISTINCT), window functions, ORDER BY with NULLS
  FIRST/LAST, FIRST/SKIP, ROWS, OFFSET/FETCH, selectable procedures in FROM, RDB$ system tables,
  RDB$DB_KEY, STARTING/STARTS [WITH], `SELECT ... WITH LOCK` (locks the returned rows like Firebird's
  dummy update, so concurrent writers wait or conflict).
* **DML**: INSERT (VALUES/SELECT/DEFAULT VALUES), UPDATE, DELETE, UPDATE OR INSERT, MERGE,
  RETURNING, EXECUTE PROCEDURE, EXECUTE BLOCK. Parameters in the select list of INSERT ... SELECT take
  the types of the target columns.
* **PSQL**: variables, cursors, IF/WHILE/FOR SELECT/FOR EXECUTE STATEMENT, LEAVE/CONTINUE/EXIT,
  SUSPEND, exceptions with WHEN handlers (EXCEPTION/SQLCODE/GDSCODE/SQLSTATE/ANY, with undo of the
  block), EXECUTE STATEMENT (with expressions and named parameters `(name := value)`), IN AUTONOMOUS
  TRANSACTION, sub-procedures/functions, NEW/OLD,
  INSERTING/UPDATING/DELETING, ROW_COUNT, context variables, RDB$GET_CONTEXT/RDB$SET_CONTEXT.
* **Built-in functions**: string, numeric, date/time, UUID, bit, LIST, COALESCE/NULLIF/IIF/DECODE...
* **Transactions**: read committed (default) and snapshot isolation, no dirty reads, statement
  atomicity, savepoints, WAIT/NOWAIT lock conflicts with deadlock detection and lock timeouts. A NOWAIT
  conflict is reported like Firebird: deadlock / update conflicts with concurrent update. READ COMMITTED
  is Firebird 4+'s READ CONSISTENCY: a statement that waited for a concurrent writer that then commits is
  restarted instead of failing with an update conflict.
* **Errors** carry Firebird's status vectors: the same GDS codes and arguments (unique/FK/check/not-null
  violations, string truncation, conversion errors, update conflicts, user exceptions, "unsuccessful
  metadata update / CREATE TABLE T failed / ..."), `At line L, column C` positions, and PSQL stack
  traces (`At procedure 'P' line: 3, col: 5`), so the client builds the same messages and SQLSTATEs, and
  `catch (FbException)` logic and PSQL `WHEN SQLCODE/GDSCODE/SQLSTATE` handlers behave the same.
* **Conversions** are ported from Firebird's: text to numbers (hex `0x..` literals, digits accumulated
  in the target's size, half-away rounding), doubles to text (`%g` with as many digits as fit the
  target), dates and times from text (all of Firebird's formats, two-digit years, month names, time
  zone suffixes), and the text of conversion errors.
* **Arithmetic** is Firebird 5's: the result type is planned at prepare (BIGINT at the larger scale for
  + and -, the sum of the scales for * and /, INT128 when a BIGINT is multiplied or divided and for
  SUM(BIGINT)) and evaluated with the same algorithms (int64 division scaling, overflow checks and
  their error codes). INT128 results are described as BIGINT (DataTypeCompatibility 3.0) and fail at
  fetch when they do not fit; literals that fit in 32 bits are evaluated as INTEGER like Firebird's BLR.
* **Strings** follow Firebird's character sets and ICU collations: `UNICODE`, `UNICODE_CI` and
  `UNICODE_CI_AI` compare and sort like ICU (ORDER BY uses the full-strength sort key, equality the
  collation's strength); LIKE, STARTING, CONTAINING, SIMILAR TO, POSITION and REPLACE honour case and
  accent insensitivity; lengths count code points; SIMILAR TO patterns are validated and translated like
  Firebird's (including `SUBSTRING ... SIMILAR`); character set introducers relabel the literal's bytes;
  OCTETS strings pad and trim with NUL and have no case.
* **Dates and times**: DATEADD (amounts rounded, Firebird's month-end rule, TIME wrap-around),
  DATEDIFF (calendar-field years/months, truncated time units, milliseconds with one decimal), EXTRACT
  (ISO week, part/type checks when preparing), FIRST_DAY/LAST_DAY, date/time arithmetic with range
  checks (`value exceeds the range for valid dates`) and Firebird's rounding of the numeric operand.
* **Rounding** matches Firebird everywhere: casts and assignments round half away from zero on the
  first dropped digit (doubles with Firebird's epsilon), ROUND returns scaled integers even for doubles
  (`ROUND(1.005e0, 2)` is 1.01), TRUNC of doubles uses `modf`, divisions truncate.
* **Predicates** follow Firebird's three-valued logic, with its quirks: ANY/ALL/IN subqueries are never
  UNKNOWN (NOT of one means "is FALSE", and NOT is pushed through AND/OR like Firebird's DSQL does),
  comparing a BOOLEAN with a number or date fails at run time with a conversion error of the other
  operand (the lower-priority operand of a comparison is the one converted), `''` in CASE/COALESCE gives
  CHAR(0), and MAXVALUE/MINVALUE of string literals hit Firebird's truncation error.
* **Aggregates and grouping** follow Firebird's sort: groups and DISTINCT rows come out in key order
  without ORDER BY; among rows with equal keys ('abc' and 'ABC' under UNICODE_CI, 'abc' and 'abc ') the
  shown value, the order in which MIN/MAX/LIST see the rows, and the DISTINCT survivor follow the bytes of
  Firebird's sort records; SUM/AVG DISTINCT add the values in sorted order; SUM(NULL) is CHAR(1); the
  aggregate errors (nested, in WHERE/GROUP BY, ungrouped columns per clause) carry Firebird's codes.
* **Ordering and limits**: rows with equal ORDER BY keys come out in Firebird's sort-record order (e.g.
  INTEGER ties by their little-endian bytes, so -1 after 5); NULLs first ascending; UNION orders by
  positions only and keeps the last of duplicate rows; FIRST/SKIP/ROWS/OFFSET/FETCH follow Firebird's
  grammar (FIRST is a column name without a value, OFFSET/FETCH take literals or parameters) and
  runtime (NULL means 0 rows, negative values fail at execute, FIRST/SKIP cannot mix with ROWS/OFFSET).
* **Parameters** are typed from their context (assignment target, other side of a comparison, CAST,
  function arguments); in the select list an operator on an untyped parameter fails like Firebird
  (`Data type unknown`, `Invalid data type for multiplication`).
* **Describe information** follows Firebird 5's rules: result types of expressions (NUMERIC vs DECIMAL,
  storage size and scale of arithmetic, CASE/COALESCE/UNION common types, built-in functions), NULLABLE
  flags, and the names, relations and field names clients see.
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
* Statement results are materialized at execute time (errors of a SELECT are still reported on
  the fetch, like Firebird: an error discards the rows of its fetch batch; WHERE conditions that depend on no table, such as an uncorrelated
  singleton subquery, fail at execute, also like Firebird).
* NO ACTION foreign keys are verified at the end of each statement.
* All text is treated as Unicode; columns are described to the client as UTF8.

## Checking against a real Firebird

The corpus in `tests/Fakebird.Tests/Corpus/*.sql` is run against Fakebird and compared with the output
of a real Firebird 5 (configured with `DataTypeCompatibility = 3.0`) recorded in `*.fb5c3.out`: column
names, client types, describe information (size, precision, scale, nullability, base table and column),
values, rows affected, and errors (the whole status vector and the message). The comparison runs in
every `dotnet test`, no server needed. A corpus file looks like this:

```sql
-- @setup                        (run first, each statement committed)
create table t (id integer primary key, s varchar(10));
-- @case                         (one statement per case; PSQL is fine)
select * from t
-- @case params: [1, "abc"]      (positional parameters as a JSON array)
insert into t values (?, ?)
-- @conn B                       (switch connection; cases run on A by default)
-- @tx snapshot nowait           (restart the current connection's transaction with options:
--                                read_committed, snapshot, consistency, wait, nowait, timeout N, read_only)
-- @commit                       (or @rollback)
```

To check the golden files against a real server, or to record new ones, run Firebird 5 in docker:

```
docker run -d --name fakebird-fb5c3 -p 127.0.0.1:3053:3050 -e FIREBIRD_ROOT_PASSWORD=masterkey \
  -e FIREBIRD_CONF_DataTypeCompatibility=3.0 firebirdsql/firebird:5
export FAKEBIRD_FB5C3_CS='DataSource=127.0.0.1;Port=3053;Database=/var/lib/firebird/data/{db}.fdb;User=SYSDBA;Password=masterkey;Charset=UTF8'
dotnet test tests/Fakebird.Tests --filter CorpusTests                      # real == golden == Fakebird
FAKEBIRD_RECORD=1 dotnet test tests/Fakebird.Tests --filter CorpusTests    # rewrite the golden files
```

A missing golden file is recorded automatically when the server is configured.

To explore, put statements in a file (corpus format, or one statement per line run after the setup of
`basics.sql`) and set `FAKEBIRD_PROBE=/path/to/file`: the `Probe` test prints the answers of every
configured server and of Fakebird, marked SAME or DIFF. Besides `FAKEBIRD_FB5C3_CS` it uses
`FAKEBIRD_FB5_CS` (native Firebird 5, e.g. `firebirdsql/firebird:5` without the setting, on port
3052) and `FAKEBIRD_FB3_CS` (e.g. `jacobalberty/firebird:v3`) for comparison.

Error codes, SQLCODEs, SQLSTATEs and message texts come from Firebird's own message files:
`tools/genmsg.py <firebird>/src/include/firebird/impl/msg > src/Fakebird/FbMessages.g.cs` (that
generated file stays under Firebird's Initial Developer's Public License, see its header).

## Loading a real-world schema

`dotnet run --project bench/Fakebird.Bench -- script create.sql patch.sql ...` runs the scripts
in-process, continues past errors and prints the distinct failures with a sample statement each:
the quickest way to find what a new schema needs.

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
tests/Fakebird.Tests/Corpus   SQL corpus with the golden output of a real Firebird 5
tools/genmsg.py        generates FbMessages.g.cs from Firebird's message files
samples/               ASP.NET Core API + WebApplicationFactory tests using Fakebird
bench/                 throughput checks through the real client
```
