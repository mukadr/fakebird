using Fakebird.Engine;
using Fakebird.Types;

namespace Fakebird.Exec;

/// <summary>Output column description.</summary>
public sealed class OutCol
{
    public string Name = "";      // alias (what readers see)
    public string Field = "";     // source column name
    public string Relation = "";  // source table
    public SqlType Type = SqlType.Null;
    public bool Nullable = true;
    /// <summary>Name derived from an expression (no alias, not a column): a UNION does not inherit it.</summary>
    public bool Derived;
    /// <summary>A bare NULL (described as CHAR(1)): it does not take part in a UNION's type.</summary>
    public bool NullLiteral;
}

public abstract class QCursor
{
    public Value[] Current = [];
    public abstract bool Next();
}

/// <summary>Compiled query producing output rows.</summary>
public abstract class QueryNode
{
    public OutCol[] Columns = [];
    /// <summary>The statement's top-level query: what fails while it opens is reported by op_execute.</summary>
    public bool AtExecute;
    public abstract QCursor Open(Ctx c);

    public List<Value[]> ToList(Ctx c)
    {
        var list = new List<Value[]>();
        var qc = Open(c);
        while (qc.Next()) list.Add(qc.Current);
        return list;
    }
}

/// <summary>SELECT list over a relational pipeline (+ DISTINCT, FIRST/SKIP).</summary>
public sealed class SpecQuery : QueryNode
{
    public RelNode Source = null!;
    public XNode[] Projection = [];
    public bool Distinct;
    /// <summary>The fields carried in the sort records of the DISTINCT sort (see SortImage).</summary>
    public SortField[] SortFields = [];
    /// <summary>The source is sorted by ORDER BY, which the DISTINCT keeps.</summary>
    public bool Ordered;
    public XNode? First, Skip;

    public override QCursor Open(Ctx c)
    {
        var limit = Limit.Open(AtExecute, c, First, Skip);
        return limit.Empty ? new EmptyQ() : new Cursor(this, c, Source.Open(c), limit);
    }

    sealed class Cursor(SpecQuery q, Ctx c, RowCursor src, Limit limit) : QCursor
    {
        List<Value[]>? _distinct;
        int _next;

        public override bool Next()
        {
            if (!limit.Next(Fetch)) return false;
            Current = _row;
            return true;
        }

        Value[] _row = [];

        bool Fetch()
        {
            if (q.Distinct && _distinct == null) _distinct = Distinct();
            if (_distinct != null)
            {
                if (_next >= _distinct.Count) return false;
                _row = _distinct[_next++];
                return true;
            }
            if (!src.Next()) return false;
            _row = Project();
            return true;
        }

        Value[] Project()
        {
            var p = q.Projection;
            var row = new Value[p.Length];
            // Firebird evaluates the select list right to left (visible with side effects such as GEN_ID)
            for (var i = p.Length - 1; i >= 0; i--) row[i] = p[i].Eval(c);
            return row;
        }

        /// <summary>The distinct rows, each the last of its duplicates in sort-record order (see SortImage).</summary>
        List<Value[]> Distinct()
        {
            var cols = q.Projection.Select(p => p.Type.Collation).ToArray();
            var seen = new Dictionary<IndexKey, int>();
            var rows = new List<(Value[] Row, byte[] Image)>();
            while (src.Next())
            {
                var row = Project();
                var image = SortImage.Build(c, q.SortFields);
                if (!seen.TryGetValue(new IndexKey(row, cols), out var i))
                {
                    seen.Add(new IndexKey(row, cols), rows.Count);
                    rows.Add((row, image));
                }
                else if (SortImage.Compare(image, rows[i].Image) >= 0) rows[i] = (row, image);
            }
            // without ORDER BY the rows come out of the sort in key order (NULLs first)
            var distinct = rows.Select(r => r.Row);
            if (!q.Ordered) distinct = distinct.OrderBy(r => r, SortImage.KeyOrder(q.Projection.Select(p => p.Type).ToArray()));
            return distinct.ToList();
        }
    }
}

public sealed class EmptyQ : QCursor
{
    public override bool Next() => false;
}

public sealed class UnionQuery : QueryNode
{
    public QueryNode Left = null!, Right = null!;
    public bool All;

    public override QCursor Open(Ctx c) => All ? new Cursor(this, c) : new ListQ(Distinct(c));

    /// <summary>
    /// UNION: Firebird sorts the union's rows, keeps the last of equal ones (in sort-record order: input order, or the
    /// bytes of ICU-collated text, see SortImage) and returns them in key order.
    /// </summary>
    List<Value[]> Distinct(Ctx c)
    {
        var types = Columns.Select(x => x.Type).ToArray();
        var cols = types.Select(t => t.Collation).ToArray();
        var carried = types.Where(SortImage.Volatile).ToArray();
        var seen = new Dictionary<IndexKey, int>();
        var rows = new List<(Value[] Row, byte[] Image)>();
        var cur = new Cursor(this, c);
        while (cur.Next())
        {
            var row = cur.Current;
            var image = SortImage.Build(carried, row.Where((_, i) => SortImage.Volatile(types[i])).ToArray());
            var key = new IndexKey(row, cols);
            if (!seen.TryGetValue(key, out var i))
            {
                seen.Add(key, rows.Count);
                rows.Add((row, image));
            }
            else if (SortImage.Compare(image, rows[i].Image) >= 0) rows[i] = (row, image);
        }
        return rows.Select(r => r.Row).OrderBy(r => r, SortImage.KeyOrder(types)).ToList();
    }

    sealed class ListQ(List<Value[]> rows) : QCursor
    {
        int _i;

        public override bool Next()
        {
            if (_i >= rows.Count) return false;
            Current = rows[_i++];
            return true;
        }
    }

    /// <summary>The branches' rows one after the other, converted to the union's column types.</summary>
    sealed class Cursor(UnionQuery q, Ctx c) : QCursor
    {
        QCursor? _cur = q.Left.Open(c);
        bool _right;

        public override bool Next()
        {
            while (true)
            {
                if (_cur!.Next())
                {
                    var src = _cur.Current;
                    var row = new Value[src.Length];
                    for (var i = 0; i < row.Length; i++) row[i] = Coercion.ToType(src[i], q.Columns[i].Type);
                    Current = row;
                    return true;
                }
                if (_right) return false;
                _right = true;
                _cur = q.Right.Open(c);
            }
        }
    }
}

/// <summary>ORDER BY over output columns (query expressions with UNION).</summary>
public sealed class SortQuery : QueryNode
{
    public QueryNode Input = null!;
    public int[] KeyColumns = [];
    public SortKey[] Keys = [];

    public override QCursor Open(Ctx c)
    {
        var rows = Input.ToList(c);
        var keys = Keys;
        var cols = KeyColumns;
        var arr = rows.Select(r => (Row: r, Key: cols.Select(i => r[i]).ToArray())).ToArray();
        var idx = Enumerable.Range(0, arr.Length).ToArray();
        Array.Sort(idx, (x, y) =>
        {
            var r = SortRel.CompareKeys(arr[x].Key, arr[y].Key, keys);
            return r != 0 ? r : x.CompareTo(y);
        });
        return new ListQ(idx.Select(i => arr[i].Row).ToList());
    }
}

public sealed class ListQ(List<Value[]> rows) : QCursor
{
    int _i;

    public override bool Next()
    {
        if (_i >= rows.Count) return false;
        Current = rows[_i++];
        return true;
    }
}

public sealed class LimitQuery : QueryNode
{
    public QueryNode Input = null!;
    public XNode? First, Skip;

    public override QCursor Open(Ctx c)
    {
        var limit = Limit.Open(AtExecute, c, First, Skip);
        return limit.Empty ? new EmptyQ() : new Cursor(Input.Open(c), limit);
    }

    sealed class Cursor(QCursor input, Limit limit) : QCursor
    {
        public override bool Next()
        {
            if (!limit.Next(input.Next)) return false;
            Current = input.Current;
            return true;
        }
    }
}

/// <summary>
/// FIRST/SKIP (and ROWS, OFFSET/FETCH), as Firebird's FirstRowsStream over SkipRowsStream: the FIRST value is
/// evaluated when the query opens, a zero FIRST opens nothing, then SKIP; NULL counts as 0, a negative value fails.
/// The counters are Firebird's, overflow included (SKIP 2^63-1 returns the first row).
/// </summary>
public sealed class Limit
{
    long _first = long.MaxValue, _skip = 1;
    public bool Empty;

    public static Limit Open(bool atExecute, Ctx c, XNode? first, XNode? skip)
    {
        var l = new Limit();
        try
        {
            if (first != null)
            {
                l._first = Value(c, first);
                if (l._first < 0) throw new StatusBuilder().Gds(FbMessages.bad_limit_param).Build();
                if (l._first == 0) { l.Empty = true; return l; }
            }
            if (skip != null)
            {
                var n = Value(c, skip);
                if (n < 0) throw new StatusBuilder().Gds(FbMessages.bad_skip_param).Build();
                l._skip = unchecked(n + 1);
            }
        }
        catch (FbError e) when (atExecute)
        {
            e.AtExecute = true;
            throw;
        }
        return l;
    }

    static long Value(Ctx c, XNode n)
    {
        var v = n.Eval(c);
        return v.IsNull ? 0 : Conv.ToInt64(v);
    }

    public bool Next(Func<bool> next)
    {
        if (_first <= 0) return false;
        _first--;
        while (_skip > 1)
        {
            _skip--;
            if (!next()) return false;
        }
        _skip = unchecked(_skip - 1);
        return next();
    }
}

/// <summary>Recursive CTE: anchor rows, then iterate the recursive member over the previous level.</summary>
public sealed class RecursiveCteQuery : QueryNode
{
    public QueryNode Anchor = null!;
    public QueryNode Recursive = null!;
    public bool All = true;
    /// <summary>Working set read by references to the CTE inside the recursive member.</summary>
    public List<Value[]> Working = new();

    public override QCursor Open(Ctx c)
    {
        var saved = Working;
        try
        {
            var result = new List<Value[]>();
            var seen = All ? null : new HashSet<IndexKey>();
            var cols = Columns.Select(x => x.Type.Collation).ToArray();
            List<Value[]> Dedupe(List<Value[]> rows)
            {
                var outRows = new List<Value[]>(rows.Count);
                foreach (var r0 in rows)
                {
                    var r = new Value[r0.Length];
                    for (var i = 0; i < r.Length; i++) r[i] = Conv.Cast(r0[i], Columns[i].Type); // rows take the anchor types (string truncation errors included)
                    if (seen != null && !seen.Add(new IndexKey(r, cols))) continue;
                    outRows.Add(r);
                }
                return outRows;
            }
            var level = Dedupe(Anchor.ToList(c));
            var depth = 0;
            while (level.Count > 0)
            {
                result.AddRange(level);
                if (++depth > 1024) throw Err.Random("Recursion depth exceeded a limit of 1024");
                Working = level;
                level = Dedupe(Recursive.ToList(c));
            }
            return new ListQ(result);
        }
        finally
        {
            Working = saved;
        }
    }
}

/// <summary>Materialized rows (EXECUTE BLOCK output, cached derived tables...).</summary>
public sealed class RowsQuery(Func<Ctx, List<Value[]>> producer) : QueryNode
{
    public override QCursor Open(Ctx c) => new ListQ(producer(c));
}
