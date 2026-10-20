using Fakebird.Engine;
using Fakebird.Types;

namespace Fakebird.Exec;

/// <summary>Iterator over rows of a relational operator; positions rows in Ctx.Rows.</summary>
public abstract class RowCursor
{
    public abstract bool Next();
}

/// <summary>Compiled relational operator (FROM / WHERE / GROUP BY / window / ORDER BY pipeline).</summary>
public abstract class RelNode
{
    /// <summary>All streams bound by this subtree.</summary>
    public int[] Streams = [];
    public abstract RowCursor Open(Ctx c);
}

public sealed class EmptyCursor : RowCursor
{
    public static readonly EmptyCursor Instance = new();
    public override bool Next() => false;
}

/// <summary>Frame save/restore of stream rows (for materializing operators).</summary>
/// <summary>The current rows of some streams, with their record slots (RDB$DB_KEY, positioned DML).</summary>
public sealed record Frame(Value[][] Rows, RowSlot?[] Slots);

public static class Frames
{
    public static Frame Save(Ctx c, int[] streams)
    {
        var f = new Value[streams.Length][];
        var slots = new RowSlot?[streams.Length];
        for (var i = 0; i < streams.Length; i++)
        {
            f[i] = c.Rows[streams[i]];
            slots[i] = c.Slots[streams[i]];
        }
        return new Frame(f, slots);
    }

    public static void Restore(Ctx c, int[] streams, Frame frame)
    {
        for (var i = 0; i < streams.Length; i++)
        {
            c.Rows[streams[i]] = frame.Rows[i];
            c.Slots[streams[i]] = frame.Slots[i];
        }
    }

    public static Frame Null(Value[][] nullRows) => new(nullRows, new RowSlot?[nullRows.Length]);

    public static void SetNull(Ctx c, int[] streams, Value[][] nullRows)
    {
        for (var i = 0; i < streams.Length; i++)
        {
            c.Rows[streams[i]] = nullRows[i];
            c.Slots[streams[i]] = null;
        }
    }
}

// ============================================================================ table access

public sealed class TableScanRel : RelNode
{
    public readonly Table Table;
    public readonly int Stream;
    public XNode? Filter;

    public TableScanRel(Table table, int stream)
    {
        Table = table;
        Stream = stream;
        Streams = [stream];
    }

    public override RowCursor Open(Ctx c)
    {
        if (Table.Virtual != null) return new ListCursor(c, Stream, Table.Virtual(c.Db), Filter);
        return new Cursor(this, c);
    }

    sealed class Cursor(TableScanRel n, Ctx c) : RowCursor
    {
        readonly List<RowSlot> _rows = n.Table.Rows;
        readonly int _count = n.Table.Rows.Count;
        int _i;

        public override bool Next()
        {
            var tx = c.Tx;
            var gtt = n.Table.IsGtt;
            while (_i < _count)
            {
                var slot = _rows[_i++];
                if (gtt && slot.Attachment != tx.Att.Id) continue;
                var d = slot.Visible(tx);
                if (d == null) continue;
                c.Rows[n.Stream] = d;
                c.Slots[n.Stream] = slot;
                if (n.Filter != null && !n.Filter.IsTrue(c)) continue;
                return true;
            }
            return false;
        }
    }
}

public sealed class ListCursor(Ctx c, int stream, List<Value[]> rows, XNode? filter) : RowCursor
{
    int _i;

    public override bool Next()
    {
        while (_i < rows.Count)
        {
            c.Rows[stream] = rows[_i++];
            if (filter != null && !filter.IsTrue(c)) continue;
            return true;
        }
        return false;
    }
}

/// <summary>Equality lookup through a hash index.</summary>
public sealed class IndexLookupRel : RelNode
{
    public readonly Table Table;
    public readonly int Stream;
    public readonly Engine.Index Index;
    public readonly XNode[] Keys;
    public XNode? Filter;

    public IndexLookupRel(Table table, int stream, Engine.Index index, XNode[] keys)
    {
        Table = table;
        Stream = stream;
        Index = index;
        Keys = keys;
        Streams = [stream];
    }

    public override RowCursor Open(Ctx c)
    {
        var vals = new Value[Keys.Length];
        for (var i = 0; i < Keys.Length; i++)
        {
            var v = Keys[i].Eval(c);
            if (v.IsNull) return EmptyCursor.Instance;
            vals[i] = Coercion.ToType(v, Table.Columns[Index.Columns[i]].Type.IsString
                ? Table.Columns[Index.Columns[i]].Type.WithLength(32765) : Table.Columns[Index.Columns[i]].Type);
        }
        var key = Index.MakeKey(vals);
        var list = Index.Lookup(key);
        if (list == null) return EmptyCursor.Instance;
        if (list.Count > 4 && list.Count > Table.Rows.Count / 2) Index.Prune(key, list);
        return new Cursor(this, c, list.ToArray(), key);
    }

    sealed class Cursor(IndexLookupRel n, Ctx c, RowSlot[] slots, IndexKey key) : RowCursor
    {
        int _i;

        public override bool Next()
        {
            var tx = c.Tx;
            while (_i < slots.Length)
            {
                var slot = slots[_i++];
                if (n.Table.IsGtt && slot.Attachment != tx.Att.Id) continue;
                var d = slot.Visible(tx);
                if (d == null || !n.Index.RowHasKey(d, key)) continue;
                c.Rows[n.Stream] = d;
                c.Slots[n.Stream] = slot;
                if (n.Filter != null && !n.Filter.IsTrue(c)) continue;
                return true;
            }
            return false;
        }
    }
}

/// <summary>Derived table / view / CTE reference: rows of a query positioned in a stream.</summary>
public sealed class DerivedRel : RelNode
{
    public readonly QueryNode Query;
    public readonly int Stream;
    public XNode? Filter;
    /// <summary>Uncorrelated: results can be cached per request.</summary>
    public bool Cacheable;

    public DerivedRel(QueryNode query, int stream, int[] innerStreams)
    {
        Query = query;
        Stream = stream;
        Streams = [stream, .. innerStreams];
    }

    public override RowCursor Open(Ctx c)
    {
        if (Cacheable)
        {
            c.Cache ??= new();
            if (!c.Cache.TryGetValue(this, out var cached))
            {
                var list = new List<Value[]>();
                var qc = Query.Open(c);
                while (qc.Next()) list.Add(qc.Current);
                c.Cache[this] = cached = list;
            }
            return new ListCursor(c, Stream, (List<Value[]>)cached, Filter);
        }
        return new Cursor(this, c, Query.Open(c));
    }

    sealed class Cursor(DerivedRel n, Ctx c, QCursor qc) : RowCursor
    {
        public override bool Next()
        {
            while (qc.Next())
            {
                c.Rows[n.Stream] = qc.Current;
                if (n.Filter != null && !n.Filter.IsTrue(c)) continue;
                return true;
            }
            return false;
        }
    }
}

/// <summary>Rows produced by a function at open time (selectable procedures, recursive CTE working set).</summary>
public sealed class FuncRel : RelNode
{
    public readonly Func<Ctx, List<Value[]>> Producer;
    public readonly int Stream;
    public XNode? Filter;

    public FuncRel(Func<Ctx, List<Value[]>> producer, int stream)
    {
        Producer = producer;
        Stream = stream;
        Streams = [stream];
    }

    public override RowCursor Open(Ctx c) => new ListCursor(c, Stream, Producer(c), Filter);
}

/// <summary>Single empty row (used for statements evaluated without FROM).</summary>
public sealed class OneRowRel : RelNode
{
    public override RowCursor Open(Ctx c) => new Cursor();

    sealed class Cursor : RowCursor
    {
        bool _done;
        public override bool Next()
        {
            if (_done) return false;
            _done = true;
            return true;
        }
    }
}

// ============================================================================ joins / filters

public sealed class FilterRel : RelNode
{
    public readonly RelNode Input;
    public readonly XNode Condition;

    public FilterRel(RelNode input, XNode cond)
    {
        Input = input;
        Condition = cond;
        Streams = input.Streams;
    }

    public override RowCursor Open(Ctx c) => new Cursor(Input.Open(c), Condition, c);

    sealed class Cursor(RowCursor input, XNode cond, Ctx c) : RowCursor
    {
        public override bool Next()
        {
            while (input.Next())
                if (cond.IsTrue(c)) return true;
            return false;
        }
    }
}

/// <summary>
/// Firebird's PreFilteredStream: a condition independent of the input's streams, tested when the cursor opens.
/// The input is not opened at all when it fails.
/// </summary>
public sealed class PreFilterRel : RelNode
{
    public readonly RelNode Input;
    public readonly XNode Condition;
    /// <summary>The top-level query's filter: it is evaluated by op_execute, so its errors are raised there.</summary>
    public bool AtExecute;

    public PreFilterRel(RelNode input, XNode cond)
    {
        Input = input;
        Condition = cond;
        Streams = input.Streams;
    }

    public override RowCursor Open(Ctx c)
    {
        bool pass;
        try { pass = Condition.IsTrue(c); }
        catch (FbError e) when (AtExecute)
        {
            e.AtExecute = true;
            throw;
        }
        return pass ? Input.Open(c) : new EmptyCursor();
    }

    sealed class EmptyCursor : RowCursor
    {
        public override bool Next() => false;
    }
}

/// <summary>SELECT ... WITH LOCK: locks the current row of a table stream as rows flow past.</summary>
public sealed class LockRel : RelNode
{
    public readonly RelNode Input;
    readonly Table _table;
    readonly int _stream;

    public LockRel(RelNode input, Table table, int stream)
    {
        Input = input;
        _table = table;
        _stream = stream;
        Streams = input.Streams;
    }

    public override RowCursor Open(Ctx c)
    {
        c.Db.EnsureOwned(_table); // the lock writes to the slots: copy-on-write first
        return new Cursor(Input.Open(c), this, c);
    }

    sealed class Cursor(RowCursor input, LockRel n, Ctx c) : RowCursor
    {
        public override bool Next()
        {
            if (!input.Next()) return false;
            if (c.Slots[n._stream] is { } slot) c.Tx.Lock(n._table, slot);
            return true;
        }
    }
}

/// <summary>Nested loop join (inner / left outer). The right side is re-opened for each left row.</summary>
public sealed class NestedLoopJoinRel : RelNode
{
    public readonly RelNode Left, Right;
    public readonly bool LeftOuter;
    public readonly XNode? Condition;
    public readonly Value[][] RightNullRows;

    public NestedLoopJoinRel(RelNode left, RelNode right, bool leftOuter, XNode? cond, Value[][] rightNullRows)
    {
        Left = left;
        Right = right;
        LeftOuter = leftOuter;
        Condition = cond;
        RightNullRows = rightNullRows;
        Streams = [.. left.Streams, .. right.Streams];
    }

    public override RowCursor Open(Ctx c) => new Cursor(this, c, Left.Open(c));

    sealed class Cursor(NestedLoopJoinRel n, Ctx c, RowCursor left) : RowCursor
    {
        RowCursor? _right;
        bool _matched;

        public override bool Next()
        {
            while (true)
            {
                if (_right == null)
                {
                    if (!left.Next()) return false;
                    _right = n.Right.Open(c);
                    _matched = false;
                }
                while (_right.Next())
                {
                    if (n.Condition != null && !n.Condition.IsTrue(c)) continue;
                    _matched = true;
                    return true;
                }
                _right = null;
                if (n.LeftOuter && !_matched)
                {
                    Frames.SetNull(c, n.Right.Streams, n.RightNullRows);
                    return true;
                }
            }
        }
    }
}

/// <summary>Hash join for equi-joins whose right side does not depend on the left side.</summary>
public sealed class HashJoinRel : RelNode
{
    public readonly RelNode Left, Right;
    public readonly XNode[] LeftKeys, RightKeys;
    public readonly Collation[] Collations;
    public readonly bool LeftOuter;
    public readonly XNode? Condition;
    public readonly Value[][] RightNullRows;

    public HashJoinRel(RelNode left, RelNode right, XNode[] leftKeys, XNode[] rightKeys, Collation[] collations,
        bool leftOuter, XNode? cond, Value[][] rightNullRows)
    {
        Left = left;
        Right = right;
        LeftKeys = leftKeys;
        RightKeys = rightKeys;
        Collations = collations;
        LeftOuter = leftOuter;
        Condition = cond;
        RightNullRows = rightNullRows;
        Streams = [.. left.Streams, .. right.Streams];
    }

    public override RowCursor Open(Ctx c)
    {
        var table = new Dictionary<IndexKey, List<Frame>>();
        var rc = Right.Open(c);
        while (rc.Next())
        {
            var vals = new Value[RightKeys.Length];
            var hasNull = false;
            for (var i = 0; i < vals.Length; i++)
            {
                vals[i] = RightKeys[i].Eval(c);
                if (vals[i].IsNull) hasNull = true;
            }
            if (hasNull) continue;
            var key = new IndexKey(vals, Collations);
            if (!table.TryGetValue(key, out var list)) table[key] = list = new();
            list.Add(Frames.Save(c, Right.Streams));
        }
        return new Cursor(this, c, Left.Open(c), table);
    }

    sealed class Cursor(HashJoinRel n, Ctx c, RowCursor left, Dictionary<IndexKey, List<Frame>> table) : RowCursor
    {
        List<Frame>? _bucket;
        int _pos;
        bool _matched;

        public override bool Next()
        {
            while (true)
            {
                if (_bucket == null)
                {
                    if (!left.Next()) return false;
                    _matched = false;
                    _pos = 0;
                    var vals = new Value[n.LeftKeys.Length];
                    var hasNull = false;
                    for (var i = 0; i < vals.Length; i++)
                    {
                        vals[i] = n.LeftKeys[i].Eval(c);
                        if (vals[i].IsNull) hasNull = true;
                    }
                    if (!hasNull && table.TryGetValue(new IndexKey(vals, n.Collations), out var b)) _bucket = b;
                    else
                    {
                        if (n.LeftOuter)
                        {
                            Frames.SetNull(c, n.Right.Streams, n.RightNullRows);
                            return true;
                        }
                        continue;
                    }
                }
                while (_pos < _bucket.Count)
                {
                    Frames.Restore(c, n.Right.Streams, _bucket[_pos++]);
                    if (n.Condition != null && !n.Condition.IsTrue(c)) continue;
                    _matched = true;
                    return true;
                }
                _bucket = null;
                if (n.LeftOuter && !_matched)
                {
                    Frames.SetNull(c, n.Right.Streams, n.RightNullRows);
                    return true;
                }
            }
        }
    }
}

public sealed class FullJoinRel : RelNode
{
    public readonly RelNode Left, Right;
    public readonly XNode? Condition;
    public readonly Value[][] LeftNullRows, RightNullRows;

    public FullJoinRel(RelNode left, RelNode right, XNode? cond, Value[][] leftNull, Value[][] rightNull)
    {
        Left = left;
        Right = right;
        Condition = cond;
        LeftNullRows = leftNull;
        RightNullRows = rightNull;
        Streams = [.. left.Streams, .. right.Streams];
    }

    public override RowCursor Open(Ctx c)
    {
        var rights = new List<Frame>();
        var rc = Right.Open(c);
        while (rc.Next()) rights.Add(Frames.Save(c, Right.Streams));
        return new Cursor(this, c, Left.Open(c), rights);
    }

    sealed class Cursor(FullJoinRel n, Ctx c, RowCursor left, List<Frame> rights) : RowCursor
    {
        readonly bool[] _rightMatched = new bool[rights.Count];
        bool _inLeft;
        int _pos;
        bool _matched;
        bool _leftDone;
        Frame? _leftFrame;

        public override bool Next()
        {
            while (!_leftDone)
            {
                if (!_inLeft)
                {
                    if (!left.Next())
                    {
                        _leftDone = true;
                        _pos = 0;
                        break;
                    }
                    _leftFrame = Frames.Save(c, n.Left.Streams);
                    _inLeft = true;
                    _pos = 0;
                    _matched = false;
                }
                while (_pos < rights.Count)
                {
                    var i = _pos++;
                    Frames.Restore(c, n.Left.Streams, _leftFrame!);
                    Frames.Restore(c, n.Right.Streams, rights[i]);
                    if (n.Condition != null && !n.Condition.IsTrue(c)) continue;
                    _rightMatched[i] = true;
                    _matched = true;
                    return true;
                }
                _inLeft = false;
                if (!_matched)
                {
                    Frames.Restore(c, n.Left.Streams, _leftFrame!);
                    Frames.SetNull(c, n.Right.Streams, n.RightNullRows);
                    return true;
                }
            }
            while (_pos < rights.Count)
            {
                var i = _pos++;
                if (_rightMatched[i]) continue;
                Frames.SetNull(c, n.Left.Streams, n.LeftNullRows);
                Frames.Restore(c, n.Right.Streams, rights[i]);
                return true;
            }
            return false;
        }
    }
}

// ============================================================================ sort

public sealed class SortKey
{
    public XNode Expr = null!;
    public bool Descending;
    public bool NullsFirst;
    public Collation Collation;
    /// <summary>Blobs sort by blob id (creation order), not by content: their keys compare equal.</summary>
    public bool IsBlob;
}

public sealed class SortRel : RelNode
{
    public readonly RelNode Input;
    public readonly SortKey[] Keys;
    /// <summary>Streams whose rows are saved/restored (all streams visible to the query).</summary>
    public readonly int[] Save;

    public SortRel(RelNode input, SortKey[] keys, int[] save)
    {
        Input = input;
        Keys = keys;
        Save = save;
        Streams = input.Streams;
    }

    /// <summary>The fields carried in the sort records, which order rows with equal keys (see SortImage).</summary>
    public SortField[] SortFields = [];

    public override RowCursor Open(Ctx c)
    {
        var rows = new List<(Value[] Keys, Frame Frame)>();
        var images = new List<byte[]>();
        var ic = Input.Open(c);
        while (ic.Next())
        {
            var k = new Value[Keys.Length];
            for (var i = 0; i < Keys.Length; i++) k[i] = Keys[i].Expr.Eval(c);
            rows.Add((k, Frames.Save(c, Save)));
            images.Add(SortImage.Build(c, SortFields));
        }
        var arr = rows.ToArray();
        var keys = Keys;
        var idx = new int[arr.Length];
        for (var i = 0; i < idx.Length; i++) idx[i] = i;
        Array.Sort(idx, (x, y) =>
        {
            var r = CompareKeys(arr[x].Keys, arr[y].Keys, keys, x, y);
            if (r == 0) r = SortImage.Compare(images[x], images[y]);
            return r != 0 ? r : x.CompareTo(y);
        });
        return new Cursor(c, Save, arr, idx);
    }

    public static int CompareKeys(Value[] a, Value[] b, SortKey[] keys) => CompareKeys(a, b, keys, 0, 0);

    /// <summary>Compares sort keys; blobs order by creation (approximated by the rows' input positions).</summary>
    public static int CompareKeys(Value[] a, Value[] b, SortKey[] keys, int posA, int posB)
    {
        for (var i = 0; i < keys.Length; i++)
        {
            var k = keys[i];
            var x = a[i];
            var y = b[i];
            int r;
            if (x.IsNull || y.IsNull)
            {
                if (x.IsNull && y.IsNull) continue;
                r = x.IsNull ? (k.NullsFirst ? -1 : 1) : (k.NullsFirst ? 1 : -1);
                return r;
            }
            r = k.IsBlob ? posA.CompareTo(posB) : Conv.Compare(x, y, k.Collation);
            if (r != 0) return k.Descending ? -r : r;
        }
        return 0;
    }

    sealed class Cursor(Ctx c, int[] save, (Value[] Keys, Frame Frame)[] rows, int[] idx) : RowCursor
    {
        int _i;

        public override bool Next()
        {
            if (_i >= idx.Length) return false;
            Frames.Restore(c, save, rows[idx[_i++]].Frame);
            return true;
        }
    }
}
