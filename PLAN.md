# Fakebird — in-memory fake of Firebird 3 for fast integration tests

## Context
ASP.NET Core integration tests against a real Firebird server are slow (disk I/O, journaling,
MVCC garbage, server round trips, DDL setup per test). We want a drop-in fake: a C#/.NET 10
library that speaks enough of the **Firebird wire protocol** that the *unchanged*
`FirebirdSql.Data.FirebirdClient` (`FbConnection`, `FbCommand`, Dapper, EF Core
`UseFirebird`) connects to it over localhost, while all data lives in simple in-memory
structures. Only the connection string changes in tests.

Decisions (confirmed with user):
- Front end: Firebird wire protocol server (FbConnection/FbParameter are sealed and EF Core
  casts to them, so an in-proc ADO.NET provider can't intercept them).
- Semantics target: **Firebird 3, SQL dialect 3**.
- Test isolation: **snapshot & restore** (run schema/seed once, clone cheaply per test).
- Philosophy: simple, fast, **fail on the first error** — anything unsupported throws an
  `FbException`-visible error naming the construct and SQL position; no silent fallbacks.
- Directory `/home/mukadr/repos/fakebird` is empty; .NET SDK 10.0.401 is installed.

## Solution layout
```
Fakebird.slnx
src/Fakebird/                  single library (no deps), namespaces by area:
  Sql/        Lexer, Token, Parser (recursive descent), AST nodes
  Types/      SqlType, Value struct, conversions/coercions, comparison, collations
  Catalog/    Database, Table, Column, Domain, Index, Constraint, Sequence, View,
              Trigger, Procedure, Function, Exception, system relations (RDB$…)
  Storage/    Row storage, row versions, transaction/undo log, snapshot clone
  Compile/    Binder + type inference: AST -> executable plan (closures)
  Exec/       Operators (scan, index seek, filter, joins, aggregate, sort, window,
              union, CTE), DML executor, constraint/trigger firing
  Psql/       PSQL compiler/interpreter (procedures, triggers, EXECUTE BLOCK, functions)
  Wire/       TCP listener, XDR reader/writer, protocol handshake, auth, op handlers,
              statement/blob/transaction handles, status-vector errors
  FakebirdServer.cs   public API (Start, CreateDatabase, Snapshot, Restore, RunScript)
tests/Fakebird.Tests/          xUnit; uses real FirebirdSql.Data.FirebirdClient,
                               Dapper and FirebirdSql.EntityFrameworkCore.Firebird
bench/Fakebird.Bench/          (later) BenchmarkDotNet for hot paths
```

## Public API (test-facing)
```csharp
await using var server = FakebirdServer.Start();            // 127.0.0.1, ephemeral port
var template = server.CreateDatabase("template");
template.RunScript(File.ReadAllText("schema.sql"));         // handles SET TERM, ; splitting
var snap = template.Snapshot();                             // immutable snapshot
var db = server.CreateDatabase($"t{Guid.NewGuid():N}", snap); // per-test clone, or:
server.Restore("app", snap);                                // reset a fixed name between tests
string cs = server.ConnectionString(db.Name);               // DataSource=127.0.0.1;Port=..;Database=..;User=SYSDBA;Password=masterkey
```
Database name in the connection string selects the in-memory database; `op_create`
(FbConnection.CreateDatabase) creates an empty one, `op_drop_database` removes it.

## Wire protocol (Wire/)
Reference: FirebirdSql/NETProvider `src/FirebirdSql.Data.FirebirdClient/Client/Managed/`
(GdsConnection.Identify, Version10/11/12 GdsDatabase/GdsStatement, XdrReader/Writer,
IscCodes.cs). Verified facts from current client source:
- Client offers protocols 10,11,12,13,15,16 in `op_connect` (11+ = `0x8000|n`,
  `ptype_lazy_send`=5). **Server picks protocol 12** (`0x800C`) and replies plain
  `op_accept(version, arch=1, ptype=5)` — no compression flag (0x100 unset).
- With protocol 12 there is **no SRP / no wire crypt**: `op_attach` carries plaintext
  `isc_dpb_password` + `isc_dpb_user_name` in the DPB. Fake accepts any credentials (or
  optionally checks a configured password). Client only implements Srp/Srp256/Sspi for 13+,
  so protocol 12 avoids implementing SRP. (Fallback if ever needed: 13 + Srp256.)
- After attach, `Open()` sends one `op_info_database` with `isc_info_firebird_version`(103)
  → reply `"LI-V3.0.11.33703 Fakebird"` (must match `\w{2}-\w(\d+\.\d+\.\d+\.\d+)`).
  No implicit SQL on open.
- Row encoding (protocol 10–12): each column = XDR value + int32 `sqlind` (0 / -1).
- Lazy send: client may pipeline ops (allocate+prepare, execute+fetch) without waiting;
  the server processes the stream strictly sequentially and replies in order.

Ops to implement: op_connect, op_attach, op_create, op_detach, op_drop_database,
op_info_database, op_transaction (parse TPB: isolation, wait/nowait, read-only),
op_commit/op_rollback(+_retaining), op_info_transaction, op_allocate_statement,
op_prepare_statement (reply with requested info: stmt_type, select/bind XSQLDA describe
— sqltype/subtype/scale/length/field/relation/alias/owner), op_info_sql, op_execute,
op_execute2 (singleton/EXECUTE PROCEDURE/RETURNING → op_sql_response), op_exec_immediate
(DDL), op_fetch/op_fetch_response (batch of rows, status 100 = EOF), op_free_statement
(DSQL_close / DSQL_drop / unprepare), op_set_cursor, blobs (op_create_blob2,
op_open_blob2, op_get_segment, op_put_segment, op_batch_segments, op_close_blob,
op_cancel_blob, op_info_blob), op_ping, op_cancel. Anything else → error naming the op.
Errors → `op_response` with status vector (isc_arg_gds code + string args + sqlstate) so the
client throws a faithful `FbException`. Blob values live in a per-attachment blob table;
column values hold the materialized string/byte[], blob ids are issued lazily on fetch.
Listener: `Socket` accept loop, one task per connection, pooled buffers
(`ArrayPool`), buffered stream with manual big-endian XDR (`BinaryPrimitives`), TCP_NODELAY.

## Engine design for speed
- **Value representation**: `readonly struct Value { Kind; long I; double D; object? O }` —
  no boxing for ints/bigint/date/time/timestamp/bool. NUMERIC/DECIMAL stored as Firebird
  does: scaled `long` + scale from the column type (exact, fast arithmetic). Strings as
  `string`, OCTETS/binary BLOB as `byte[]`, text BLOB as `string`.
- **Rows**: `Value[]` per row. Table = `List<RowSlot>` with free-list; `RowSlot` holds
  `Committed` version and optional `Pending` version + owner transaction id.
- **Transactions (read committed, FB default via FirebirdClient)**: writes go to
  `Pending` owned by the tx; readers see own pending else committed (no dirty reads).
  Writing a row pending under another active tx → Firebird `update conflict` error
  (no waiting — fail fast). Commit = promote pendings; rollback = drop them (undo list
  per tx). Retaining variants supported. SNAPSHOT isolation: tx captures commit
  sequence number; rows carry commit seq of committed version; older version kept only
  while a snapshot tx is active (simple 1-back version). A single database lock (`Lock`)
  serializes statement execution — tests are I/O bound, not CPU contention bound.
- **Indexes**: PK/UNIQUE/FK and `CREATE INDEX` → `Dictionary<Key, …>` hash index for
  equality seeks + uniqueness; ordered `SortedList`-style index only built lazily when a
  range/ORDER BY on the index is used. Composite keys via struct key with precomputed hash.
- **Compilation**: AST is bound once into closures (`Func<ExecContext, Value>` for
  expressions, iterator operators for relations). Prepared plans cached per
  `(database schema version, SQL text)` — repeated test queries skip lex/parse/bind.
  Schema change bumps version and invalidates.
- **Snapshots with copy-on-write (COW)**: see the dedicated section below. Creating a
  per-test database from a snapshot is O(number of tables), not O(rows).
- Hand-written lexer over `ReadOnlySpan<char>`, keyword lookup via
  `FrozenDictionary` with case-insensitive comparer; no regex, no LINQ in hot paths.

## Copy-on-write snapshots
Goal: `CreateDatabase(name, snapshot)` / `Restore(name, snapshot)` must be close to free so every
test can start from the seeded template, even when the seed holds many thousands of rows.

- **Frozen snapshot**: `Database.Snapshot()` (under the db lock, no active transactions
  required — only committed data is taken) produces an immutable `DbSnapshot`: catalog
  definitions (shared, immutable ASTs/types) plus, per table, a frozen `TableData`
  (`RowSlot[]` holding committed `Value[]` rows + built indexes). Frozen data is never
  mutated afterwards; row arrays are already immutable by design (updates create new arrays).
- **Cheap clone**: a database created from a snapshot gets new catalog objects (dictionaries
  are copied, definition objects shared) and, for each table, a *reference* to the frozen
  `TableData` with an `IsShared` flag — no row or index copy.
- **Copy on first write, per table**: the first write (INSERT/UPDATE/DELETE, DDL touching
  the table, index rebuild) to a table whose data is shared calls `Table.EnsureOwned()`,
  which copies the slot list (new `RowSlot` objects pointing to the same immutable
  `Value[]` rows) and clones the hash indexes' buckets. Cost is paid only for tables the
  test actually modifies; read-only tables stay shared across all clones forever.
- **Reads** of shared tables go straight to the frozen data (visibility of a frozen slot is
  "committed at seq 0", visible to every transaction), so reads add no overhead.
- **Sequences/generators** are small value objects: copied eagerly on clone.
- **Compiled plan cache**: plans reference catalog objects of a specific database, so the
  plan cache is per database instance. To avoid recompiling the same SQL in every per-test
  clone, a later step can key plans by snapshot lineage + schema version and bind table
  access by table *id* resolved through the executing database (keeps clones cheap and
  warm). Until then each clone compiles lazily on first use.
- **Restore** of a fixed database name simply swaps the server's name → database mapping
  to a fresh COW clone (open attachments to the old instance are closed/rejected).
- Verification: test that mutations in clone A are invisible to clone B and to the
  snapshot, that untouched tables share storage (reference equality), and a benchmark of
  clone + first-write cost on a 100k-row seed.

## SQL coverage (Firebird 3, dialect 3)
**Types**: SMALLINT, INTEGER, BIGINT, FLOAT, DOUBLE PRECISION, NUMERIC/DECIMAL(p,s),
CHAR(n) (blank padded, trailing-blank-insensitive compare), VARCHAR(n) (length checked →
"string right truncation"), DATE, TIME, TIMESTAMP, BOOLEAN, BLOB SUB_TYPE TEXT/BINARY,
CHARACTER SET OCTETS/UTF8/NONE/WIN1252 (all stored as .NET strings except OCTETS),
COLLATE UNICODE_CI / UNICODE_CI_AI / PT_BR etc. mapped to culture-insensitive CI/AI
comparers. Dialect-3 rules: integer division, numeric scale arithmetic, quoted
identifiers case-sensitive, unquoted upper-cased, `"` vs `'`.

**DDL**: CREATE/ALTER/DROP/RECREATE/CREATE OR ALTER for TABLE, VIEW, PROCEDURE, TRIGGER
(table + DB-level ON CONNECT etc. accepted only if no-op safe), FUNCTION, EXCEPTION,
DOMAIN, SEQUENCE/GENERATOR (SET GENERATOR, ALTER SEQUENCE RESTART), INDEX (ASC/DESC,
computed-by), constraints (PK, FK with ON DELETE/UPDATE CASCADE/SET NULL/SET DEFAULT/NO
ACTION, UNIQUE, CHECK, NOT NULL), DEFAULT, COMPUTED BY columns, IDENTITY columns,
ALTER TABLE ADD/DROP/ALTER COLUMN/ADD CONSTRAINT/DROP CONSTRAINT, ALTER COLUMN POSITION,
GLOBAL TEMPORARY TABLE (ON COMMIT DELETE/PRESERVE ROWS). Parsed & ignored (no-ops):
GRANT/REVOKE, COMMENT ON, CREATE ROLE/USER, SET STATISTICS, ALTER INDEX ACTIVE/INACTIVE
(tracked), DECLARE EXTERNAL FUNCTION → error on call. PACKAGES: deferred (error).

**DML/query**: SELECT [FIRST/SKIP] [DISTINCT], `*`/`t.*`, aliases, INNER/LEFT/RIGHT/FULL/
CROSS/NATURAL JOIN, USING, derived tables, CTEs incl. RECURSIVE, WHERE, GROUP BY, HAVING,
ORDER BY (positional, expressions, NULLS FIRST/LAST, FB default: NULLs first ASC),
ROWS m TO n, OFFSET/FETCH, UNION [ALL|DISTINCT], subqueries (scalar, IN, EXISTS, SINGULAR,
ANY/SOME/ALL), FOR UPDATE / WITH LOCK (no-op), PLAN (ignored). Window functions
(ROW_NUMBER, RANK, DENSE_RANK, LAG, LEAD, FIRST_VALUE, LAST_VALUE, NTH_VALUE, aggregates
OVER PARTITION/ORDER). INSERT VALUES/SELECT/DEFAULT VALUES, UPDATE, DELETE, all with
RETURNING (singleton and FB3 semantics), UPDATE OR INSERT … MATCHING, MERGE (WHEN
MATCHED/NOT MATCHED, DELETE), EXECUTE PROCEDURE, EXECUTE BLOCK (with params).
**Expressions/functions**: arithmetic, `||`, CASE, IIF, COALESCE, NULLIF, DECODE, CAST,
LIKE/ESCAPE, STARTING WITH, CONTAINING, SIMILAR TO (translated to .NET regex, cached),
BETWEEN, IS [NOT] DISTINCT FROM, IS [NOT] NULL, IS TRUE/FALSE/UNKNOWN; aggregates COUNT/
SUM/AVG/MIN/MAX/LIST (DISTINCT); string: UPPER, LOWER, TRIM, SUBSTRING, POSITION,
CHAR_LENGTH/OCTET_LENGTH, REPLACE, LPAD/RPAD, LEFT/RIGHT, REVERSE, OVERLAY, ASCII_CHAR/VAL,
HASH; numeric: ABS, CEIL, FLOOR, ROUND, TRUNC, MOD, POWER, SQRT, SIGN, EXP, LN, LOG, LOG10,
RAND, trig; date: EXTRACT, DATEADD, DATEDIFF, CURRENT_DATE/TIME/TIMESTAMP, LOCALTIMESTAMP,
'NOW'/'TODAY'/'TOMORROW'/'YESTERDAY' literals; misc: GEN_ID, NEXT VALUE FOR, GEN_UUID,
UUID_TO_CHAR, CHAR_TO_UUID, RDB$GET_CONTEXT/RDB$SET_CONTEXT, CURRENT_USER, CURRENT_ROLE,
CURRENT_CONNECTION, CURRENT_TRANSACTION, ROW_COUNT, MAXVALUE/MINVALUE, BIN_AND/OR/XOR/SHL/SHR.

**PSQL** (procedures, triggers, EXECUTE BLOCK, functions): DECLARE VARIABLE (with
domains/TYPE OF/TYPE OF COLUMN), local cursors, assignments, IF/ELSE, WHILE, LEAVE/BREAK/
CONTINUE, FOR SELECT … INTO … DO, FOR EXECUTE STATEMENT, SELECT … INTO, SUSPEND
(selectable procs, streaming rows), EXIT, EXCEPTION (named, with custom message,
re-raise), WHEN EXCEPTION/SQLCODE/GDSCODE/ANY DO handlers with SAVEPOINT-style undo of
the block, EXECUTE STATEMENT (dynamic, recompiled via cache), IN AUTONOMOUS TRANSACTION
(executed in nested tx), NEW/OLD context, INSERTING/UPDATING/DELETING, trigger POSITION
ordering, sub-procedures/sub-functions (FB3). Compiled to the same closure tree as SQL.

**Constraint/trigger semantics**: per-statement order like FB: BEFORE triggers → NOT NULL/
CHECK → unique/PK → FK → AFTER triggers. FK checks immediate. Errors use real GDS codes &
SQLSTATE (e.g. 335544665 unique_key_violation, 335544466 foreign_key, 335544347
not_valid, 335544558 check_constraint, 335544321 arith_except/string truncation,
335544517 user exception) with FB-style messages so app `catch (FbException e)` logic works.

**System tables**: minimal read-only virtual relations backed by the catalog:
RDB$DATABASE, RDB$RELATIONS, RDB$RELATION_FIELDS, RDB$FIELDS, RDB$GENERATORS,
RDB$PROCEDURES, RDB$PROCEDURE_PARAMETERS, RDB$TRIGGERS, RDB$INDICES, RDB$INDEX_SEGMENTS,
RDB$RELATION_CONSTRAINTS, RDB$REF_CONSTRAINTS, RDB$EXCEPTIONS, MON$ATTACHMENTS (current only)
— enough for common "does table exist" checks and EF Core migrations history checks.

## Implementation phases
1. **Spike (de-risk first)**: solution skeleton + Wire handshake/attach/detach and a
   hardcoded `SELECT 1 FROM RDB$DATABASE`; test with real `FbConnection` (+ pooling on/off).
2. **Engine core**: lexer, parser for DDL tables/sequences/indexes + basic DML; Value/types;
   storage; transactions; prepare/execute/fetch on wire; parameters; RETURNING; blobs.
3. **Query breadth**: joins, grouping, ordering, subqueries, CTEs, unions, windows, functions.
4. **Constraints & triggers**, domains, views, computed/identity columns, error codes.
5. **PSQL**: procedures (selectable/executable), EXECUTE BLOCK, functions, exceptions,
   handlers, EXECUTE STATEMENT.
6. **Snapshot/restore API, RunScript, system tables**, EF Core + Dapper end-to-end tests.
7. **Performance pass**: plan cache, index use in WHERE/join equality, BenchmarkDotNet,
   allocation profiling of fetch path.

## Verification
- `dotnet test tests/Fakebird.Tests` — per-feature tests written as SQL + expected rows,
  executed **through the real FirebirdClient** (so wire + engine are both exercised).
- Error tests assert `FbException.ErrorCode` / SQLSTATE / message for constraint
  violations, update conflicts, string truncation, user exceptions, unsupported syntax.
- End-to-end: a sample EF Core DbContext (migrations-free `EnsureCreated` / raw schema
  script) + Dapper repository + a minimal ASP.NET Core `WebApplicationFactory` test.
- Optional differential mode: if env `FAKEBIRD_REAL_CS` points to a real Firebird 3
  (e.g. docker `jacobalberty/firebird:v3`), the same SQL test cases run against both and
  results/errors are compared — the main guard that the fake's semantics match.
- Benchmark: run the user's slow suite against real FB vs Fakebird and compare wall time.

## Status (2026-10-16)
Phases 1–6 implemented; phase 7 partially (index maintenance fix, measurements below).
- Wire protocol 10–12 server works with the unchanged FirebirdClient 10.3.4, Dapper and
  EF Core 10 (`FirebirdSql.EntityFrameworkCore.Firebird` 13): EnsureCreated, Migrate (with the
  migration lock), LINQ translations, ExecuteUpdate/Delete, split queries, transactions.
- COW snapshots implemented (`Database.Snapshot` / `FromSnapshot` / `EnsureOwned`).
- WAIT/NOWAIT lock semantics, deadlock detection, lock timeouts (client default is NOWAIT).
- 44 tests in `tests/Fakebird.Tests` + 4 ASP.NET Core `WebApplicationFactory` tests in `samples/`.
- Measured (Release, localhost): ~50 µs per INSERT and ~130 µs per trivial query through the
  client (round-trip bound), ~150 µs per COW clone of a 60k-row database, ~31 µs to compile an
  EF-style query (so plan sharing across clones is deferred).
- Differential harness ready (`DifferentialTests`, env `FAKEBIRD_REAL_CS`); not yet run against
  a real Firebird 3 server.

Remaining / known gaps: packages, database-level triggers, WHERE CURRENT OF, DML through views,
named parameters in EXECUTE STATEMENT, RANGE window frames with offsets, sharing compiled plans
across snapshot clones.
