using System.Runtime.CompilerServices;
using Fakebird.Types;

namespace Fakebird.Engine;

/// <summary>A committed version of a row. Data == null marks a committed delete.</summary>
public sealed class RowVersion
{
    public Value[]? Data;
    public long Seq;
    public RowVersion? Older;
}

/// <summary>
/// A row slot: its committed version chain plus at most one uncommitted version owned by a transaction.
/// Row arrays are immutable once stored; updates always store a new array.
/// </summary>
public sealed class RowSlot
{
    public RowVersion? Committed;
    public Value[]? Pending;
    public Transaction? Owner;
    public bool PendingDelete;
    /// <summary>Global temporary tables: owning attachment (0 = regular table).</summary>
    public int Attachment;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Value[]? Visible(Transaction tx)
    {
        if (Owner == tx) return PendingDelete ? null : Pending;
        var v = Committed;
        if (tx.Snapshot)
            while (v != null && v.Seq > tx.StartSeq) v = v.Older;
        return v?.Data;
    }

    /// <summary>Latest committed data (ignoring snapshots), null if deleted / never committed.</summary>
    public Value[]? LatestCommitted => Committed?.Data;

    /// <summary>Slot belongs to a frozen snapshot and must never be modified.</summary>
    public bool Frozen;

    public bool IsDead => Owner == null && (Committed == null || (Committed.Data == null && Committed.Older == null));
}

/// <summary>Composite index key with precomputed hash.</summary>
public readonly struct IndexKey : IEquatable<IndexKey>
{
    public readonly Value[] Values;
    readonly int _hash;
    readonly Collation[] _collations;

    public IndexKey(Value[] values, Collation[] collations)
    {
        Values = values;
        _collations = collations;
        var h = new HashCode();
        for (var i = 0; i < values.Length; i++) h.Add(Conv.Hash(values[i], collations[i]));
        _hash = h.ToHashCode();
    }

    public bool HasNull
    {
        get
        {
            foreach (var v in Values) if (v.IsNull) return true;
            return false;
        }
    }

    public bool Equals(IndexKey o)
    {
        if (_hash != o._hash || Values.Length != o.Values.Length) return false;
        for (var i = 0; i < Values.Length; i++)
            if (!Conv.KeyEquals(Values[i], o.Values[i], _collations[i])) return false;
        return true;
    }

    public override bool Equals(object? obj) => obj is IndexKey k && Equals(k);
    public override int GetHashCode() => _hash;

    public override string ToString() => string.Join(", ", Values.Select(v => v.IsNull ? "NULL" : Conv.ToStr(v)));
}

/// <summary>
/// Hash index over slots. Entries may be stale (key no longer present in any version of the slot);
/// readers always re-check row data, and stale entries are dropped lazily.
/// </summary>
public sealed class Index
{
    public string Name = null!;
    public string Table = null!;
    public int[] Columns = [];
    public bool Unique;
    public bool Descending;
    public bool Active = true;
    /// <summary>Owning constraint (PK/UK/FK) or null for user indexes.</summary>
    public string? Constraint;
    public Collation[] Collations = [];

    internal Dictionary<IndexKey, List<RowSlot>> Map = new();
    /// <summary>Map is shared with a frozen snapshot (copy-on-write): never mutate it.</summary>
    internal bool Shared;

    public Index CloneDef() => new()
    {
        Name = Name, Table = Table, Columns = Columns, Unique = Unique, Descending = Descending, Active = Active,
        Constraint = Constraint, Collations = Collations,
    };

    public IndexKey KeyOf(Value[] row)
    {
        var vals = new Value[Columns.Length];
        for (var i = 0; i < Columns.Length; i++) vals[i] = row[Columns[i]];
        return new IndexKey(vals, Collations);
    }

    public IndexKey MakeKey(Value[] values) => new(values, Collations);

    public void Add(RowSlot slot, Value[] row) => Add(slot, KeyOf(row), isNewSlot: false);

    /// <summary>Adds a freshly inserted slot (cannot already be in any bucket).</summary>
    public void AddNew(RowSlot slot, Value[] row) => Add(slot, KeyOf(row), isNewSlot: true);

    /// <summary>Adds the slot under the key of <paramref name="row"/> unless <paramref name="previous"/> has the same key.</summary>
    public void AddChanged(RowSlot slot, Value[]? previous, Value[] row)
    {
        if (previous != null && RowHasKey(previous, KeyOf(row))) return;
        Add(slot, KeyOf(row), isNewSlot: false);
    }

    void Add(RowSlot slot, IndexKey key, bool isNewSlot)
    {
        if (!Map.TryGetValue(key, out var list))
        {
            Map[key] = [slot];
            return;
        }
        if (!isNewSlot)
            for (var i = list.Count - 1; i >= 0; i--)
                if (ReferenceEquals(list[i], slot)) return;
        list.Add(slot);
    }

    public List<RowSlot>? Lookup(in IndexKey key) => Map.TryGetValue(key, out var l) ? l : null;

    public bool RowHasKey(Value[]? row, in IndexKey key)
    {
        if (row == null) return false;
        for (var i = 0; i < Columns.Length; i++)
            if (!Conv.KeyEquals(row[Columns[i]], key.Values[i], Collations[i])) return false;
        return true;
    }

    /// <summary>Removes entries whose slot no longer holds the key in any version.</summary>
    public void Prune(in IndexKey key, List<RowSlot> list)
    {
        if (Shared) return;
        for (var i = list.Count - 1; i >= 0; i--)
        {
            var s = list[i];
            if (SlotMayHaveKey(s, key)) continue;
            list.RemoveAt(i);
        }
        if (list.Count == 0) Map.Remove(key);
    }

    bool SlotMayHaveKey(RowSlot s, in IndexKey key)
    {
        if (s.Owner != null && !s.PendingDelete && RowHasKey(s.Pending, key)) return true;
        for (var v = s.Committed; v != null; v = v.Older)
            if (RowHasKey(v.Data, key)) return true;
        return false;
    }

    public void Rebuild(List<RowSlot> slots)
    {
        Map = new Dictionary<IndexKey, List<RowSlot>>();
        Shared = false;
        foreach (var s in slots)
        {
            Value[]? first = null;
            for (var v = s.Committed; v != null; v = v.Older)
            {
                if (v.Data == null) continue;
                if (first == null)
                {
                    first = v.Data;
                    AddNew(s, v.Data);
                }
                else Add(s, v.Data);
            }
            if (s.Owner != null && s.Pending != null && !s.PendingDelete)
            {
                if (first == null) AddNew(s, s.Pending);
                else AddChanged(s, first, s.Pending);
            }
        }
    }
}

public sealed class Table
{
    public string Name = null!;
    public List<Column> Columns = new();
    public Dictionary<string, int> ColumnIndex = new(StringComparer.Ordinal);
    public List<RowSlot> Rows = new();
    /// <summary>Rows (and the table's index maps) are shared with a frozen snapshot: copy before writing.</summary>
    internal bool Shared;
    public int DeadRows;
    public bool IsSystem;
    public bool IsGtt;
    public bool GttPreserveRows;
    /// <summary>For virtual (system) tables: produces rows on demand.</summary>
    public Func<Database, List<Value[]>>? Virtual;

    public string? PrimaryKey;
    public List<string> Uniques = new();
    public List<string> ForeignKeys = new();
    public List<string> Checks = new();
    public List<string> Indexes = new();
    /// <summary>FK constraints in other tables referencing this table.</summary>
    public List<string> ReferencedBy = new();
    public List<string> Triggers = new();

    public int Width => Columns.Count;

    public void RebuildColumnIndex()
    {
        ColumnIndex.Clear();
        for (var i = 0; i < Columns.Count; i++) ColumnIndex[Columns[i].Name] = i;
    }

    public int FindColumn(string name) => ColumnIndex.TryGetValue(name, out var i) ? i : -1;

    public Table CloneDef()
    {
        var t = new Table
        {
            Name = Name, IsSystem = IsSystem, IsGtt = IsGtt, GttPreserveRows = GttPreserveRows, Virtual = Virtual,
            PrimaryKey = PrimaryKey, Uniques = [.. Uniques], ForeignKeys = [.. ForeignKeys], Checks = [.. Checks],
            Indexes = [.. Indexes], ReferencedBy = [.. ReferencedBy], Triggers = [.. Triggers],
        };
        foreach (var c in Columns) t.Columns.Add(c.Clone());
        t.RebuildColumnIndex();
        return t;
    }
}
