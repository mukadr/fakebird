using Fakebird.Types;

namespace Fakebird.Engine;

/// <summary>An in-memory database: catalog + data. All access must hold <see cref="Sync"/>.</summary>
public sealed partial class Database
{
    public string Name { get; internal set; }
    public readonly object Sync = new();

    internal readonly Dictionary<string, Table> Tables = new(StringComparer.Ordinal);
    internal readonly Dictionary<string, View> Views = new(StringComparer.Ordinal);
    internal readonly Dictionary<string, Sequence> Sequences = new(StringComparer.Ordinal);
    internal readonly Dictionary<string, Domain> Domains = new(StringComparer.Ordinal);
    internal readonly Dictionary<string, Procedure> Procedures = new(StringComparer.Ordinal);
    internal readonly Dictionary<string, Function> Functions = new(StringComparer.Ordinal);
    internal readonly Dictionary<string, Trigger> Triggers = new(StringComparer.Ordinal);
    internal readonly Dictionary<string, ExceptionDef> Exceptions = new(StringComparer.Ordinal);
    internal readonly Dictionary<string, Index> Indexes = new(StringComparer.Ordinal);
    internal readonly Dictionary<string, Constraint> Constraints = new(StringComparer.Ordinal);

    internal int SchemaVersion;
    internal long NextTransactionId = 1;
    internal long CommitSeq;
    internal readonly List<Transaction> ActiveTransactions = new();
    internal int NextAttachmentId = 1;
    /// <summary>System name generators (Firebird's RDB$CONSTRAINT_NAME, RDB$INDEX_NAME, RDB$GENERATOR_NAME).</summary>
    internal int ConstraintNameCounter, IndexNameCounter, GeneratorNameCounter, CheckTriggerCounter;
    internal int ExceptionCounter;
    internal int ExecDepth;
    internal readonly Dictionary<string, object> PlanCache = new(StringComparer.Ordinal);
    internal readonly Dictionary<string, Exec.TableRuntime> Runtimes = new(StringComparer.Ordinal);
    internal readonly Dictionary<string, string> GlobalContext = new(StringComparer.Ordinal);
    /// <summary>Session time zone of new attachments (Firebird's DefaultTimeZone); null is the system's zone.</summary>
    public ushort? DefaultTimeZone;
    /// <summary>Character set of the text columns, domains and routine variables declared without one.</summary>
    public Charset DefaultCharset = Charset.Utf8;

    public Database(string name)
    {
        Name = name;
        SystemTables.Register(this);
    }

    internal void SchemaChanged()
    {
        SchemaVersion++;
        PlanCache.Clear();
        Runtimes.Clear();
    }

    internal string NewConstraintName() => "INTEG_" + (++ConstraintNameCounter);
    internal string NewIndexName(string prefix) => prefix + (++IndexNameCounter);
    internal string NewGeneratorName() => "RDB$" + (++GeneratorNameCounter);
    /// <summary>Firebird's system triggers for CHECK constraints and foreign key actions: CHECK_1, CHECK_2...</summary>
    internal string NewCheckTriggerName() => "CHECK_" + (++CheckTriggerCounter);

    internal Table GetTable(string name)
        => Tables.TryGetValue(name, out var t) ? t : throw Err.TableUnknown(name);

    internal Constraint GetConstraint(string name) => Constraints[name];
    internal Index GetIndex(string name) => Indexes[name];

    internal bool HasActiveSnapshotOtherThan(Transaction tx)
    {
        foreach (var t in ActiveTransactions) if (t.Snapshot && t != tx) return true;
        return false;
    }

    internal bool ObjectNameInUse(string name)
        => Tables.ContainsKey(name) || Views.ContainsKey(name) || Procedures.ContainsKey(name);

    /// <summary>True for frozen snapshot databases (read-only template data).</summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Produces a frozen snapshot: catalog definitions plus committed rows only. The snapshot's slots and
    /// index maps are never modified afterwards, so databases created from it can share them (COW).
    /// </summary>
    public Database Snapshot()
    {
        lock (Sync)
        {
            var d = new Database(Name + "$snapshot");
            CopyCatalogInto(d);
            foreach (var (k, t) in Tables)
            {
                if (t.IsSystem) continue;
                var nt = d.Tables[k];
                if (t.IsGtt) continue;
                var rows = new List<RowSlot>(t.Rows.Count);
                foreach (var slot in t.Rows)
                {
                    var data = slot.LatestCommitted;
                    if (data == null) continue;
                    rows.Add(new RowSlot { Key = slot.Key, Committed = new RowVersion { Data = data, Seq = 0 }, Frozen = true });
                }
                nt.Rows = rows;
            }
            foreach (var ix in d.Indexes.Values)
                if (d.Tables.TryGetValue(ix.Table, out var tt)) ix.Rebuild(tt.Rows);
            d.IsFrozen = true;
            return d;
        }
    }

    /// <summary>
    /// Creates a writable database from a frozen snapshot in O(number of tables): row storage and index maps
    /// are shared by reference and copied per table on the first write (<see cref="EnsureOwned"/>).
    /// </summary>
    public static Database FromSnapshot(Database snapshot, string name)
    {
        if (!snapshot.IsFrozen) throw new InvalidOperationException("Database is not a snapshot; call Snapshot() first.");
        var d = new Database(name);
        snapshot.CopyCatalogInto(d);
        foreach (var (k, t) in snapshot.Tables)
        {
            if (t.IsSystem || t.IsGtt) continue;
            var nt = d.Tables[k];
            nt.Rows = t.Rows;
            nt.Shared = true;
        }
        foreach (var (k, ix) in snapshot.Indexes)
        {
            var nix = d.Indexes[k];
            nix.Map = ix.Map;
            nix.Shared = true;
        }
        return d;
    }

    void CopyCatalogInto(Database d)
    {
        d.DefaultCharset = DefaultCharset;
        foreach (var (k, t) in Tables)
            if (!t.IsSystem) d.Tables[k] = t.CloneDef();
        foreach (var (k, v) in Views) d.Views[k] = v.Clone();
        foreach (var (k, s) in Sequences) d.Sequences[k] = s.Clone();
        foreach (var (k, x) in Domains) d.Domains[k] = x.Clone();
        foreach (var (k, p) in Procedures) d.Procedures[k] = p.CloneDef();
        foreach (var (k, f) in Functions) d.Functions[k] = f.CloneDef();
        foreach (var (k, t) in Triggers) d.Triggers[k] = t.CloneDef();
        foreach (var (k, e) in Exceptions) d.Exceptions[k] = e;
        foreach (var (k, c) in Constraints) d.Constraints[k] = c.Clone();
        foreach (var (k, ix) in Indexes) d.Indexes[k] = ix.CloneDef();
        d.ConstraintNameCounter = ConstraintNameCounter;
        d.IndexNameCounter = IndexNameCounter;
        d.GeneratorNameCounter = GeneratorNameCounter;
        d.CheckTriggerCounter = CheckTriggerCounter;
        d.ExceptionCounter = ExceptionCounter;
    }

    /// <summary>Copy-on-write: gives the table private row slots and index maps before it is modified.</summary>
    internal void EnsureOwned(Table t)
    {
        if (IsFrozen) throw Err.Random("snapshot databases are read-only");
        if (!t.Shared) return;
        var rows = new List<RowSlot>(t.Rows.Count + 16);
        // version objects are copied too, so in-place DDL rewrites never touch the snapshot
        foreach (var s in t.Rows)
            rows.Add(new RowSlot
            {
                Key = s.Key,
                Committed = s.Committed == null ? null : new RowVersion { Data = s.Committed.Data, Seq = s.Committed.Seq },
                Attachment = s.Attachment,
            });
        t.Rows = rows;
        t.Shared = false;
        foreach (var ix in t.Indexes) Indexes[ix].Rebuild(rows);
    }

    /// <summary>Physically removes dead slots. Only called when no statement is running.</summary>
    internal void CompactIfNeeded(Table t)
    {
        if (t.Shared || t.DeadRows < 64 || t.DeadRows * 2 < t.Rows.Count || ExecDepth > 0) return;
        t.Rows.RemoveAll(s => s.IsDead);
        t.DeadRows = 0;
        foreach (var ixName in t.Indexes) Indexes[ixName].Rebuild(t.Rows);
    }
}

/// <summary>A client connection to a database.</summary>
public sealed class Attachment
{
    public readonly int Id;
    public readonly Database Db;
    public string User = "SYSDBA";
    public string Role = "NONE";
    public readonly Dictionary<string, string> SessionContext = new(StringComparer.Ordinal);
    internal readonly List<Transaction> Transactions = new();
    /// <summary>The session time zone (SET TIME ZONE) and the one the attachment started with (SET TIME ZONE LOCAL).</summary>
    public ushort SessionTimeZone, OriginalTimeZone;

    public Attachment(Database db)
    {
        Db = db;
        Id = db.NextAttachmentId++;
        SessionTimeZone = OriginalTimeZone = db.DefaultTimeZone ?? TimeZones.System;
    }

    public Transaction StartTransaction(bool snapshot = false, bool readOnly = false, bool wait = true)
    {
        var tx = new Transaction(this, snapshot, readOnly, wait);
        Transactions.Add(tx);
        return tx;
    }

    /// <summary>Rolls back open transactions and drops GTT rows.</summary>
    public void Detach()
    {
        foreach (var tx in Transactions.ToArray())
            if (tx.Active) tx.Rollback();
        Transactions.Clear();
        foreach (var t in Db.Tables.Values)
        {
            if (!t.IsGtt) continue;
            t.Rows.RemoveAll(s => s.Attachment == Id);
            foreach (var ix in t.Indexes) Db.Indexes[ix].Rebuild(t.Rows);
        }
    }
}

internal struct UndoEntry
{
    public Table Table;
    public RowSlot Slot;
    public Value[]? PrevPending;
    public Transaction? PrevOwner;
    public bool PrevDelete;
    public bool Insert;
}

public sealed class Transaction
{
    public readonly long Id;
    public readonly Attachment Att;
    public readonly Database Db;
    public bool Snapshot;
    public bool ReadOnly;
    public bool Wait;
    public long StartSeq;
    /// <summary>
    /// READ COMMITTED (READ CONSISTENCY, Firebird 4+): the commit sequence when the current top-level statement
    /// started; the whole statement, PSQL included, reads the data committed up to then.
    /// </summary>
    internal long? StatementSeq;
    public bool Active = true;
    internal readonly List<UndoEntry> Undo = new();
    internal List<(string Name, int Mark)>? Savepoints;
    public readonly Dictionary<string, string> Context = new(StringComparer.Ordinal);
    internal HashSet<Table>? GttTouched;
    /// <summary>NO ACTION foreign key checks deferred to the end of the current top-level statement.</summary>
    internal List<(Exec.RefInfo Ref, Value[] OldParent)>? DeferredRefChecks;

    internal Transaction(Attachment att, bool snapshot, bool readOnly, bool wait)
    {
        Att = att;
        Db = att.Db;
        Id = Db.NextTransactionId++;
        Snapshot = snapshot;
        ReadOnly = readOnly;
        Wait = wait;
        StartSeq = Db.CommitSeq;
        Db.ActiveTransactions.Add(this);
    }

    void EnsureWritable()
    {
        if (!Active) throw Err.Random("transaction is not active");
        if (ReadOnly) throw new StatusBuilder().Gds(335544361).Build("25006"); // read_only_trans
    }

    internal RowSlot Insert(Table t, Value[] row)
    {
        EnsureWritable();
        Db.EnsureOwned(t);
        var slot = new RowSlot { Owner = this, Pending = row };
        if (t.IsGtt)
        {
            slot.Attachment = Att.Id;
            (GttTouched ??= new()).Add(t);
        }
        t.Rows.Add(slot);
        Undo.Add(new UndoEntry { Table = t, Slot = slot, Insert = true });
        foreach (var ix in t.Indexes) Db.Indexes[ix].AddNew(slot, row);
        return slot;
    }

    void CheckConflict(RowSlot slot)
    {
        if (slot.Frozen) throw new InvalidOperationException("internal error: write to a shared snapshot row (missing EnsureOwned)");
        while (slot.Owner != null && slot.Owner != this && slot.Owner.Active)
        {
            var other = slot.Owner;
            WaitFor(other);
            // the concurrent transaction changed the record and committed: a snapshot transaction gets an update
            // conflict, a READ COMMITTED one (READ CONSISTENCY, Firebird 4+) restarts the top-level statement
            if (other.WasCommitted)
            {
                if (!Snapshot) throw new StatementRestart(other.Id);
                throw Err.UpdateConflict(other.Id);
            }
        }
        if (Snapshot && slot.Committed != null && slot.Committed.Seq > StartSeq)
            throw Err.UpdateConflict(0);
        // the row was committed after the statement's view was taken: READ CONSISTENCY restarts the statement
        if (!Snapshot && StatementSeq is { } seq && slot.Committed != null && slot.Committed.Seq > seq)
            throw new StatementRestart(0);
    }

    /// <summary>Blocks (releasing the database lock) until <paramref name="other"/> ends, like a WAIT transaction.</summary>
    internal void WaitFor(Transaction other)
    {
        // NOWAIT: Firebird reports the conflict as deadlock / update conflict / concurrent transaction
        if (!Wait)
            throw Err.UpdateConflict(other.Id);
        for (var t = other; t != null; t = t.WaitingFor)
            if (t == this) throw Err.UpdateConflict(other.Id); // deadlock
        WaitingFor = other;
        try
        {
            var deadline = LockTimeout > 0 ? DateTime.UtcNow.AddSeconds(LockTimeout) : DateTime.MaxValue;
            while (other.Active)
            {
                var remaining = deadline - DateTime.UtcNow;
                // a timed-out wait ends like a NOWAIT conflict
                if (remaining <= TimeSpan.Zero) throw Err.UpdateConflict(other.Id);
                Monitor.Wait(Db.Sync, remaining > TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : remaining);
            }
        }
        finally
        {
            WaitingFor = null;
        }
    }

    internal Transaction? WaitingFor;
    /// <summary>Restarts of the current top-level statement (Firebird gives up after 10).</summary>
    internal int Restarts;
    public bool WasCommitted { get; private set; }
    public int LockTimeout;
    /// <summary>Isolation CONSISTENCY (table stability): a snapshot that the context reports as such.</summary>
    public bool Consistency;

    internal void Update(Table t, RowSlot slot, Value[] row)
    {
        EnsureWritable();
        CheckConflict(slot);
        var previous = slot.Owner == this && !slot.PendingDelete ? slot.Pending : slot.LatestCommitted;
        Undo.Add(new UndoEntry { Table = t, Slot = slot, PrevPending = slot.Pending, PrevOwner = slot.Owner, PrevDelete = slot.PendingDelete });
        slot.Owner = this;
        slot.Pending = row;
        slot.PendingDelete = false;
        foreach (var ix in t.Indexes) Db.Indexes[ix].AddChanged(slot, previous, row);
    }

    /// <summary>
    /// SELECT ... WITH LOCK: a dummy update of the row (same values, no triggers), so concurrent writers wait
    /// or conflict exactly as with a real UPDATE.
    /// </summary>
    internal void Lock(Table t, RowSlot slot)
    {
        if (slot.Owner == this) return;
        EnsureWritable();
        CheckConflict(slot);
        var current = slot.LatestCommitted;
        if (current == null) return;
        Undo.Add(new UndoEntry { Table = t, Slot = slot, PrevPending = slot.Pending, PrevOwner = slot.Owner, PrevDelete = slot.PendingDelete });
        slot.Owner = this;
        slot.Pending = current;
        slot.PendingDelete = false;
    }

    internal void Delete(Table t, RowSlot slot)
    {
        EnsureWritable();
        CheckConflict(slot);
        Undo.Add(new UndoEntry { Table = t, Slot = slot, PrevPending = slot.Pending, PrevOwner = slot.Owner, PrevDelete = slot.PendingDelete });
        slot.Owner = this;
        slot.PendingDelete = true;
    }

    /// <summary>Marker for statement-level atomicity / savepoints.</summary>
    internal int Mark => Undo.Count;

    internal void RollbackTo(int mark)
    {
        for (var i = Undo.Count - 1; i >= mark; i--)
        {
            var u = Undo[i];
            if (u.Insert)
            {
                u.Slot.Owner = null;
                u.Slot.Pending = null;
                u.Slot.Committed = null;
                u.Table.DeadRows++;
            }
            else
            {
                u.Slot.Pending = u.PrevPending;
                u.Slot.Owner = u.PrevOwner;
                u.Slot.PendingDelete = u.PrevDelete;
            }
        }
        Undo.RemoveRange(mark, Undo.Count - mark);
    }

    public void Commit(bool retaining = false)
    {
        lock (Db.Sync) CommitCore(retaining);
    }

    void CommitCore(bool retaining)
    {
        if (!Active) throw Err.Random("transaction is not active");
        var seq = ++Db.CommitSeq;
        var keepOld = Db.HasActiveSnapshotOtherThan(this);
        foreach (var u in Undo)
        {
            var s = u.Slot;
            if (s.Owner != this) continue;
            var data = s.PendingDelete ? null : s.Pending;
            if (data == null && s.Committed == null)
            {
                // inserted and deleted in the same transaction
                s.Owner = null;
                s.Pending = null;
                u.Table.DeadRows++;
                continue;
            }
            s.Committed = new RowVersion { Data = data, Seq = seq, Older = keepOld ? s.Committed : null };
            s.Owner = null;
            s.Pending = null;
            s.PendingDelete = false;
            if (data == null && !keepOld) u.Table.DeadRows++;
        }
        Undo.Clear();
        Savepoints = null;
        ClearGtt();
        if (retaining)
        {
            StartSeq = Db.CommitSeq;
            Monitor.PulseAll(Db.Sync);
            return;
        }
        WasCommitted = true;
        End();
    }

    public void Rollback(bool retaining = false)
    {
        lock (Db.Sync) RollbackCore(retaining);
    }

    void RollbackCore(bool retaining)
    {
        if (!Active) throw Err.Random("transaction is not active");
        RollbackTo(0);
        Savepoints = null;
        ClearGtt();
        if (retaining)
        {
            StartSeq = Db.CommitSeq;
            return;
        }
        End();
    }

    void ClearGtt()
    {
        if (GttTouched == null) return;
        foreach (var t in GttTouched)
        {
            if (t.GttPreserveRows) continue;
            t.Rows.RemoveAll(s => s.Attachment == Att.Id);
            foreach (var ix in t.Indexes) Db.Indexes[ix].Rebuild(t.Rows);
        }
        GttTouched = null;
    }

    void End()
    {
        Active = false;
        Monitor.PulseAll(Db.Sync);
        Db.ActiveTransactions.Remove(this);
        Att.Transactions.Remove(this);
        foreach (var t in Db.Tables.Values)
            if (t.DeadRows > 0) Db.CompactIfNeeded(t);
    }

    internal void Savepoint(string name)
    {
        Savepoints ??= new();
        Savepoints.RemoveAll(s => s.Name == name);
        Savepoints.Add((name, Mark));
    }

    internal void RollbackToSavepoint(string name)
    {
        var idx = Savepoints?.FindLastIndex(s => s.Name == name) ?? -1;
        if (idx < 0) throw new StatusBuilder().Gds(FbMessages.invalid_savepoint).Str(name).Build();
        RollbackTo(Savepoints![idx].Mark);
        Savepoints.RemoveRange(idx + 1, Savepoints.Count - idx - 1);
    }

    internal void ReleaseSavepoint(string name, bool only)
    {
        var idx = Savepoints?.FindLastIndex(s => s.Name == name) ?? -1;
        if (idx < 0) throw new StatusBuilder().Gds(FbMessages.invalid_savepoint).Str(name).Build();
        if (only) Savepoints!.RemoveAt(idx);
        else Savepoints!.RemoveRange(idx, Savepoints.Count - idx);
    }
}
