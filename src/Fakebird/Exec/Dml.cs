using System.Text;
using Fakebird.Engine;
using Fakebird.Sql;
using Fakebird.Types;

namespace Fakebird.Exec;

/// <summary>An expression evaluated against a single row placed in stream 0 of its own request.</summary>
public sealed class RowExpr
{
    public XNode Node = null!;
    public int Streams;

    public Value Eval(Ctx caller, Value[] row)
    {
        var c = caller.Child(Streams);
        c.Rows[0] = row;
        return Node.Eval(c);
    }
}

public sealed class FkInfo
{
    public Constraint Constraint = null!;
    public int[] Columns = [];
    public Table Parent = null!;
    public Engine.Index ParentIndex = null!;
}

public sealed class RefInfo
{
    public Constraint Constraint = null!;
    public Table Child = null!;
    public int[] ChildColumns = [];
    public Engine.Index ChildIndex = null!;
    public int[] ParentColumns = [];
    public Engine.Index ParentIndex = null!;
    public Table Parent = null!;
}

/// <summary>Compiled per-table write rules: defaults, identity, computed columns, constraints, triggers.</summary>
public sealed class TableRuntime
{
    public Table Table = null!;
    public int Version;
    public XNode?[] Defaults = [];
    public Sequence?[] Identity = [];
    public (int Col, RowExpr Expr)[] Computed = [];
    public (Constraint Constraint, RowExpr Expr)[] Checks = [];
    public (int Col, string Domain, RowExpr Expr)[] DomainChecks = [];
    public (string Constraint, Engine.Index Index)[] Uniques = [];
    public FkInfo[] Fks = [];
    public RefInfo[] Refs = [];
    public Trigger[][] Triggers = new Trigger[6][];
    public bool HasComputed => Computed.Length > 0;

    public static int TriggerSlot(TriggerTiming t, TriggerEvent e)
        => (t == TriggerTiming.Before ? 0 : 3) + e switch { TriggerEvent.Insert => 0, TriggerEvent.Update => 1, _ => 2 };
}

/// <summary>Row-level write pipelines (insert/update/delete with Firebird constraint and trigger semantics).</summary>
public static class Dml
{
    /// <summary>Resolver installed by the compiler: builds/caches TableRuntime and fires triggers.</summary>
    public static Func<Database, Table, TableRuntime> GetRuntime = null!;
    public static Action<Ctx, Trigger, Table, TriggerEvent, Value[]?, Value[]?> FireTrigger = null!;

    static void Fire(Ctx c, TableRuntime rt, TriggerTiming timing, TriggerEvent ev, Value[]? oldRow, Value[]? newRow)
    {
        var list = rt.Triggers[TableRuntime.TriggerSlot(timing, ev)];
        if (list == null || list.Length == 0) return;
        foreach (var t in list) FireTrigger(c, t, rt.Table, ev, oldRow, newRow);
    }

    /// <summary>Fills defaults/identity for columns not provided and casts provided values.</summary>
    public static Value[] PrepareInsertRow(Ctx c, TableRuntime rt, Value[] values, bool[] provided)
    {
        var t = rt.Table;
        var row = new Value[t.Columns.Count];
        for (var i = 0; i < row.Length; i++)
        {
            var col = t.Columns[i];
            if (col.IsComputed) continue;
            if (provided[i])
            {
                row[i] = CastColumn(t, i, values[i]);
                continue;
            }
            var seq = rt.Identity[i];
            if (seq != null)
            {
                seq.Value += seq.Increment;
                row[i] = CastColumn(t, i, Value.Int(seq.Value));
            }
            else if (rt.Defaults[i] != null) row[i] = CastColumn(t, i, rt.Defaults[i]!.Eval(c));
        }
        return row;
    }

    public static Value CastColumn(Table t, int i, in Value v)
    {
        var col = t.Columns[i];
        try
        {
            return Conv.Cast(v, col.Type);
        }
        catch (FbError e) when (e.HasCode(Err.isc_string_truncation))
        {
            throw new StatusBuilder().Gds(Err.isc_arith_except).Gds(Err.isc_string_truncation)
                .Gds(Err.isc_trunc_limits).Num(col.Type.Length).Num(Conv.CharLength(Conv.ToStr(v))).Build("22001");
        }
    }

    /// <summary>Re-casts values (triggers may assign anything), computes COMPUTED columns, validates NOT NULL/CHECK.</summary>
    static void Finalize(Ctx c, TableRuntime rt, Value[] row, bool update)
    {
        var t = rt.Table;
        for (var i = 0; i < row.Length; i++)
            if (!t.Columns[i].IsComputed && !row[i].IsNull) row[i] = CastColumn(t, i, row[i]);
        foreach (var (col, expr) in rt.Computed)
            row[col] = Coercion.ToType(expr.Eval(c, row), t.Columns[col].Type);
        for (var i = 0; i < row.Length; i++)
        {
            var col = t.Columns[i];
            if (col.NotNull && row[i].IsNull) throw Err.NotNull($"\"{t.Name}\".\"{col.Name}\"");
        }
        foreach (var (col, domain, expr) in rt.DomainChecks)
        {
            var v = expr.Eval(c, [row[col]]);
            if (!v.IsNull && v.Tag == VTag.Bool && !v.AsBool)
                throw Err.NotValidDomain($"\"{t.Name}\".\"{t.Columns[col].Name}\"", row[col]);
        }
        foreach (var (con, expr) in rt.Checks)
        {
            var v = expr.Eval(c, row);
            if (!v.IsNull && v.Tag == VTag.Bool && !v.AsBool) throw Err.Check(con.Name, t.Name, update ? con.UpdateTrigger : con.InsertTrigger);
        }
    }

    public static string KeyText(Table t, int[] cols, Value[] row)
    {
        var sb = new StringBuilder("(");
        for (var i = 0; i < cols.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            var v = row[cols[i]];
            sb.Append('"').Append(t.Columns[cols[i]].Name).Append("\" = ");
            if (v.IsNull) sb.Append("NULL");
            else if (v.Tag is VTag.String or VTag.Date or VTag.Time or VTag.Timestamp) sb.Append('\'').Append(Conv.ToStr(v)).Append('\'');
            else if (v.Tag == VTag.Bytes) sb.Append("x'").Append(Convert.ToHexString(v.AsBytes)).Append('\'');
            else sb.Append(Conv.ToStr(v));
        }
        return sb.Append(')').ToString();
    }

    static void CheckUnique(Ctx c, TableRuntime rt, Value[] row, RowSlot? self, Value[]? oldRow)
    {
        var tx = c.Tx;
        foreach (var (name, ix) in rt.Uniques)
        {
            if (oldRow != null && SameKey(ix, row, oldRow)) continue;
            var key = ix.KeyOf(row);
            if (key.HasNull) continue;
            var list = ix.Lookup(key);
            if (list == null) continue;
            foreach (var s in list)
            {
                if (ReferenceEquals(s, self)) continue;
                if (rt.Table.IsGtt && s.Attachment != tx.Att.Id) continue;
                var conflict = false;
                if (s.Owner == tx)
                    conflict = !s.PendingDelete && ix.RowHasKey(s.Pending, key);
                else
                {
                    if (s.Owner is { Active: true } other && (s.PendingDelete || ix.RowHasKey(s.Pending, key)))
                    {
                        // a concurrent transaction holds the key: wait for it like Firebird does
                        tx.WaitFor(other);
                        CheckUnique(c, rt, row, self, oldRow);
                        return;
                    }
                    if (ix.RowHasKey(s.LatestCommitted, key)) conflict = true;
                }
                if (conflict) throw Err.Unique(name, rt.Table.Name, KeyText(rt.Table, ix.Columns, row));
            }
        }
    }

    static bool SameKey(Engine.Index ix, Value[] a, Value[] b)
    {
        foreach (var col in ix.Columns)
        {
            var x = a[col];
            var y = b[col];
            if (x.IsNull != y.IsNull) return false;
            if (!x.IsNull && (x.Tag != y.Tag || Conv.Compare(x, y) != 0)) return false;
        }
        return true;
    }

    static bool SameCols(int[] cols, Value[] a, Value[] b)
    {
        foreach (var col in cols)
        {
            var x = a[col];
            var y = b[col];
            if (x.IsNull != y.IsNull) return false;
            if (!x.IsNull && (x.Tag != y.Tag || Conv.Compare(x, y) != 0)) return false;
        }
        return true;
    }

    static bool ExistsVisible(Ctx c, Engine.Index ix, Table table, IndexKey key, RowSlot? exclude = null)
    {
        var list = ix.Lookup(key);
        if (list == null) return false;
        foreach (var s in list)
        {
            if (ReferenceEquals(s, exclude)) continue;
            if (table.IsGtt && s.Attachment != c.Tx.Att.Id) continue;
            if (ix.RowHasKey(s.Visible(c.Tx), key)) return true;
        }
        return false;
    }

    /// <summary>A self-referencing row (FK value equal to its own key) satisfies its constraint, like Firebird.</summary>
    static bool ReferencesItself(Engine.Index parentIndex, Value[] row, in IndexKey key) => parentIndex.RowHasKey(row, key);

    static void CheckForeignKeys(Ctx c, TableRuntime rt, Value[] row, Value[]? oldRow)
    {
        foreach (var fk in rt.Fks)
        {
            if (oldRow != null && SameCols(fk.Columns, row, oldRow)) continue;
            var vals = new Value[fk.Columns.Length];
            var hasNull = false;
            for (var i = 0; i < vals.Length; i++)
            {
                vals[i] = row[fk.Columns[i]];
                if (vals[i].IsNull) hasNull = true;
                else vals[i] = Conv.Cast(vals[i], fk.Parent.Columns[fk.ParentIndex.Columns[i]].Type);
            }
            if (hasNull) continue;
            var key = fk.ParentIndex.MakeKey(vals);
            if (ReferenceEquals(fk.Parent, rt.Table) && ReferencesItself(fk.ParentIndex, row, key)) continue;
            if (!ExistsVisible(c, fk.ParentIndex, fk.Parent, key))
                throw Err.ForeignKeyMissing(fk.Constraint.Name, rt.Table.Name, KeyText(rt.Table, fk.Columns, row));
        }
    }

    public static Value[] Insert(Ctx c, TableRuntime rt, Value[] row)
    {
        Fire(c, rt, TriggerTiming.Before, TriggerEvent.Insert, null, row);
        Finalize(c, rt, row, update: false);
        CheckUnique(c, rt, row, null, null);
        CheckForeignKeys(c, rt, row, null);
        c.Tx.Insert(rt.Table, row);
        Fire(c, rt, TriggerTiming.After, TriggerEvent.Insert, null, row);
        return row;
    }

    public static Value[] Update(Ctx c, TableRuntime rt, RowSlot slot, Value[] oldRow, Value[] newRow)
    {
        Fire(c, rt, TriggerTiming.Before, TriggerEvent.Update, oldRow, newRow);
        Finalize(c, rt, newRow, update: true);
        CheckUnique(c, rt, newRow, slot, oldRow);
        CheckForeignKeys(c, rt, newRow, oldRow);
        c.Tx.Update(rt.Table, slot, newRow);
        foreach (var r in rt.Refs)
        {
            if (SameCols(r.ParentColumns, newRow, oldRow)) continue;
            ApplyReference(c, r, oldRow, newRow, isDelete: false, rt);
        }
        Fire(c, rt, TriggerTiming.After, TriggerEvent.Update, oldRow, newRow);
        return newRow;
    }

    public static void Delete(Ctx c, TableRuntime rt, RowSlot slot, Value[] oldRow)
    {
        Fire(c, rt, TriggerTiming.Before, TriggerEvent.Delete, oldRow, null);
        c.Tx.Delete(rt.Table, slot);
        foreach (var r in rt.Refs) ApplyReference(c, r, oldRow, null, isDelete: true, rt);
        Fire(c, rt, TriggerTiming.After, TriggerEvent.Delete, oldRow, null);
    }

    /// <summary>ON DELETE / ON UPDATE actions for child rows referencing the old parent key.</summary>
    static void ApplyReference(Ctx c, RefInfo r, Value[] oldParent, Value[]? newParent, bool isDelete, TableRuntime parentRt)
    {
        var vals = new Value[r.ParentColumns.Length];
        for (var i = 0; i < vals.Length; i++)
        {
            vals[i] = oldParent[r.ParentColumns[i]];
            if (vals[i].IsNull) return;
            vals[i] = Conv.Cast(vals[i], r.Child.Columns[r.ChildColumns[i]].Type);
        }
        var key = r.ChildIndex.MakeKey(vals);
        var list = r.ChildIndex.Lookup(key);
        if (list == null) return;
        var action = isDelete ? r.Constraint.OnDelete : r.Constraint.OnUpdate;
        // When the parent key still exists (e.g. another row with the same unique key), nothing to do.
        var children = new List<(RowSlot Slot, Value[] Row)>();
        foreach (var s in list.ToArray())
        {
            if (r.Child.IsGtt && s.Attachment != c.Tx.Att.Id) continue;
            var d = s.Visible(c.Tx);
            if (d != null && r.ChildIndex.RowHasKey(d, key)) children.Add((s, d));
        }
        if (children.Count == 0) return;
        if (action == RefAction.NoAction)
        {
            // checked at the end of the statement (rows referencing each other may all be deleted)
            (c.Tx.DeferredRefChecks ??= new()).Add((r, oldParent));
            return;
        }
        c.Db.EnsureOwned(r.Child);
        // after EnsureOwned the slot objects may have been replaced: re-resolve through the index
        if (!ReferenceEquals(list, r.ChildIndex.Lookup(key)))
        {
            children.Clear();
            foreach (var s in r.ChildIndex.Lookup(key)?.ToArray() ?? [])
            {
                var d = s.Visible(c.Tx);
                if (d != null && r.ChildIndex.RowHasKey(d, key)) children.Add((s, d));
            }
        }
        var childRt = GetRuntime(c.Db, r.Child);
        foreach (var (slot, row) in children)
        {
            var current = slot.Visible(c.Tx);
            if (current == null) continue;
            switch (action)
            {
                case RefAction.Cascade when isDelete:
                    Delete(c, childRt, slot, current);
                    break;
                case RefAction.Cascade:
                {
                    var nr = (Value[])current.Clone();
                    for (var i = 0; i < r.ChildColumns.Length; i++) nr[r.ChildColumns[i]] = newParent![r.ParentColumns[i]];
                    Update(c, childRt, slot, current, nr);
                    break;
                }
                case RefAction.SetNull:
                {
                    var nr = (Value[])current.Clone();
                    foreach (var col in r.ChildColumns) nr[col] = Value.Null;
                    Update(c, childRt, slot, current, nr);
                    break;
                }
                case RefAction.SetDefault:
                {
                    var nr = (Value[])current.Clone();
                    foreach (var col in r.ChildColumns)
                        nr[col] = childRt.Defaults[col] == null ? Value.Null : CastColumn(r.Child, col, childRt.Defaults[col]!.Eval(c));
                    Update(c, childRt, slot, current, nr);
                    break;
                }
            }
        }
    }
}

public static class DeferredChecks
{
    /// <summary>Verifies NO ACTION references after a statement: no child may reference a vanished parent key.</summary>
    public static void Run(Ctx c)
    {
        var list = c.Tx.DeferredRefChecks;
        if (list == null) return;
        c.Tx.DeferredRefChecks = null;
        foreach (var (r, oldParent) in list)
        {
            var vals = new Value[r.ParentColumns.Length];
            for (var i = 0; i < vals.Length; i++) vals[i] = Conv.Cast(oldParent[r.ParentColumns[i]], r.Child.Columns[r.ChildColumns[i]].Type);
            var childKey = r.ChildIndex.MakeKey(vals);
            var children = r.ChildIndex.Lookup(childKey);
            if (children == null) continue;
            var referenced = false;
            foreach (var s in children)
            {
                if (r.Child.IsGtt && s.Attachment != c.Tx.Att.Id) continue;
                if (r.ChildIndex.RowHasKey(s.Visible(c.Tx), childKey)) { referenced = true; break; }
            }
            if (!referenced) continue;
            var pvals = new Value[r.ParentColumns.Length];
            for (var i = 0; i < pvals.Length; i++) pvals[i] = oldParent[r.ParentColumns[i]];
            var parentKey = r.ParentIndex.MakeKey(pvals);
            var stillThere = r.ParentIndex.Lookup(parentKey)?.Any(s => r.ParentIndex.RowHasKey(s.Visible(c.Tx), parentKey)) ?? false;
            if (!stillThere)
                throw Err.ForeignKeyPresent(r.Constraint.Name, r.Child.Name, Dml.KeyText(r.Parent, r.ParentColumns, oldParent));
        }
    }
}
