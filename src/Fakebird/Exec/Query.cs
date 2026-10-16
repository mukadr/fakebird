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
    public XNode? First, Skip;

    public override QCursor Open(Ctx c)
    {
        long first = long.MaxValue, skip = 0;
        if (First != null) first = LimitValue(c, First, "FIRST");
        if (Skip != null) skip = LimitValue(c, Skip, "SKIP");
        if (first == 0) return new EmptyQ();
        return new Cursor(this, c, Source.Open(c), first, skip);
    }

    internal static long LimitValue(Ctx c, XNode n, string what)
    {
        var v = n.Eval(c);
        if (v.IsNull) return what == "FIRST" ? long.MaxValue : 0;
        var l = Conv.ToInt64(v);
        if (l < 0) throw Err.Random($"Invalid {what} value: must be zero or positive");
        return l;
    }

    sealed class Cursor(SpecQuery q, Ctx c, RowCursor src, long first, long skip) : QCursor
    {
        HashSet<IndexKey>? _seen;
        long _returned;
        long _skipped;
        readonly Collation[]? _cols = q.Distinct ? q.Projection.Select(p => p.Type.Collation).ToArray() : null;

        public override bool Next()
        {
            if (_returned >= first) return false;
            while (src.Next())
            {
                var p = q.Projection;
                var row = new Value[p.Length];
                // Firebird evaluates the select list right to left (visible with side effects such as GEN_ID)
                for (var i = p.Length - 1; i >= 0; i--) row[i] = p[i].Eval(c);
                if (_cols != null)
                {
                    _seen ??= new();
                    if (!_seen.Add(new IndexKey(row, _cols))) continue;
                }
                if (_skipped < skip)
                {
                    _skipped++;
                    continue;
                }
                _returned++;
                Current = row;
                return true;
            }
            return false;
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

    public override QCursor Open(Ctx c) => new Cursor(this, c);

    sealed class Cursor(UnionQuery q, Ctx c) : QCursor
    {
        QCursor? _cur = q.Left.Open(c);
        bool _right;
        HashSet<IndexKey>? _seen = q.All ? null : new();
        readonly Collation[] _cols = q.Columns.Select(x => x.Type.Collation).ToArray();

        public override bool Next()
        {
            while (true)
            {
                if (_cur!.Next())
                {
                    var src = _cur.Current;
                    var row = new Value[src.Length];
                    for (var i = 0; i < row.Length; i++) row[i] = Coercion.ToType(src[i], q.Columns[i].Type);
                    if (_seen != null && !_seen.Add(new IndexKey(row, _cols))) continue;
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
        long first = long.MaxValue, skip = 0;
        if (First != null) first = SpecQuery.LimitValue(c, First, "FIRST");
        if (Skip != null) skip = SpecQuery.LimitValue(c, Skip, "SKIP");
        return new Cursor(Input.Open(c), first, skip);
    }

    sealed class Cursor(QCursor input, long first, long skip) : QCursor
    {
        long _n, _s;

        public override bool Next()
        {
            if (_n >= first) return false;
            while (input.Next())
            {
                if (_s < skip)
                {
                    _s++;
                    continue;
                }
                _n++;
                Current = input.Current;
                return true;
            }
            return false;
        }
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
