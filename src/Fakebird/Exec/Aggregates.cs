using Fakebird.Engine;
using System.Text;
using Fakebird.Types;

namespace Fakebird.Exec;

public enum AggKind { CountStar, Count, Sum, Avg, Min, Max, List }

public sealed class AggSpec
{
    public AggKind Kind;
    public XNode? Arg;
    public XNode? Separator;
    public bool Distinct;
    public SqlType Type = SqlType.Null;
    public Collation Collation;
}

/// <summary>Running state of one aggregate.</summary>
public sealed class AggState
{
    readonly AggSpec _spec;
    long _count;
    Int128 _isum;
    int _scale;
    double _dsum;
    bool _isDouble;
    bool _any;
    Value _best;
    StringBuilder? _list;
    List<byte>? _blist;
    HashSet<IndexKey>? _distinct;

    public AggState(AggSpec spec) => _spec = spec;

    public void Add(Ctx c)
    {
        if (_spec.Kind == AggKind.CountStar)
        {
            _count++;
            return;
        }
        var v = _spec.Arg!.Eval(c);
        if (v.IsNull) return;
        if (_spec.Distinct)
        {
            _distinct ??= new();
            if (!_distinct.Add(new IndexKey([v], [_spec.Collation]))) return;
        }
        switch (_spec.Kind)
        {
            case AggKind.Count:
                _count++;
                break;
            case AggKind.Sum:
            case AggKind.Avg:
                _count++;
                if (v.Tag == VTag.String) v = Conv.ParseNumber(v.AsString);
                if (v.Tag is VTag.Int or VTag.Int128 && !_isDouble)
                {
                    var x = v.AsInt128;
                    try
                    {
                        if (!_any)
                        {
                            _scale = v.Scale;
                            _isum = x;
                        }
                        else if (v.Scale == _scale) _isum = checked(_isum + x);
                        else
                        {
                            var s = Math.Max(_scale, v.Scale);
                            _isum = checked(_isum * Value.Pow10[s - _scale] + x * Value.Pow10[s - v.Scale]);
                            _scale = s;
                        }
                    }
                    catch (OverflowException) { throw Err.IntegerOverflow128(); }
                    // a BIGINT sum overflows like int64 arithmetic
                    if (_spec.Kind == AggKind.Sum && _spec.Type.Kind != TypeKind.Int128 && (_isum > long.MaxValue || _isum < long.MinValue))
                        throw Err.IntegerOverflow();
                }
                else
                {
                    if (!_isDouble && _any) _dsum = (double)_isum / Value.Pow10Double[_scale];
                    _isDouble = true;
                    _dsum += Conv.ToDouble(v);
                }
                _any = true;
                break;
            case AggKind.Min:
            case AggKind.Max:
                if (!_any) { _best = v; _any = true; break; }
                var cmp = Conv.Compare(v, _best, _spec.Collation);
                if (_spec.Kind == AggKind.Min ? cmp < 0 : cmp > 0) _best = v;
                break;
            case AggKind.List:
            {
                var sep = ",";
                if (_spec.Separator != null)
                {
                    var sv = _spec.Separator.Eval(c);
                    sep = sv.IsNull ? "" : Conv.ToStr(sv);
                }
                if (v.Tag == VTag.Bytes && _spec.Type.SubType != 1)
                {
                    _blist ??= new();
                    if (_any) _blist.AddRange(Conv.Utf8.GetBytes(sep));
                    _blist.AddRange(v.AsBytes);
                }
                else
                {
                    _list ??= new StringBuilder();
                    if (_any) _list.Append(sep);
                    var str = Conv.ToStr(v);
                    _list.Append(str);
                    // Firebird concatenates the stored CHAR bytes: multi-byte charsets pad to the byte length
                    if (_spec.Arg!.Type is { Kind: TypeKind.Char, BytesPerChar: > 1 } ct)
                        _list.Append(' ', Math.Max(0, ct.Length * ct.BytesPerChar - Conv.Utf8.GetByteCount(str)));
                }
                _any = true;
                break;
            }
        }
    }

    public Value Result()
    {
        switch (_spec.Kind)
        {
            case AggKind.CountStar:
            case AggKind.Count:
                return Value.Int(_count);
            case AggKind.Sum:
                if (!_any) return Value.Null;
                if (_isDouble) return Value.Dbl(_dsum);
                if (_spec.Type.Kind == TypeKind.Int128) return Coercion.ToType(Value.Wide(_isum, _scale), _spec.Type);
                return Coercion.ToType(Value.Int((long)_isum, _scale), _spec.Type);
            case AggKind.Avg:
                if (!_any) return Value.Null;
                if (_isDouble) return Value.Dbl(_dsum / _count);
                if (_spec.Type.Kind == TypeKind.Int128) return Coercion.ToType(Value.Wide(_isum / _count, _scale), _spec.Type);
                return Coercion.ToType(Value.Int((long)(_isum / _count), _scale), _spec.Type);
            case AggKind.Min:
            case AggKind.Max:
                return _any ? _best : Value.Null;
            case AggKind.List:
                if (!_any) return Value.Null;
                return _blist != null ? Value.Bytes(_blist.ToArray()) : Value.Str(_list!.ToString());
        }
        return Value.Null;
    }
}

/// <summary>GROUP BY / aggregate operator. Output stream = [group keys..., aggregate results...].</summary>
public sealed class AggregateRel : RelNode
{
    public readonly RelNode Input;
    public readonly XNode[] Keys;
    public readonly Collation[] KeyCollations;
    public readonly AggSpec[] Aggs;
    public readonly int OutStream;
    /// <summary>Streams of the input whose rows are restored from the group's first row.</summary>
    public readonly int[] Save;
    public readonly Value[][] SaveNullRows;

    public AggregateRel(RelNode input, XNode[] keys, AggSpec[] aggs, int outStream, int[] save, Value[][] saveNull)
    {
        Input = input;
        Keys = keys;
        KeyCollations = keys.Select(k => k.Type.Collation).ToArray();
        Aggs = aggs;
        OutStream = outStream;
        Save = save;
        SaveNullRows = saveNull;
        Streams = [.. input.Streams, outStream];
    }

    public override RowCursor Open(Ctx c)
    {
        var groups = new Dictionary<IndexKey, int>();
        var list = new List<(Value[] Keys, Value[][] Frame, AggState[] States)>();
        var ic = Input.Open(c);
        while (ic.Next())
        {
            var kv = new Value[Keys.Length];
            for (var i = 0; i < Keys.Length; i++) kv[i] = Keys[i].Eval(c);
            var key = new IndexKey(kv, KeyCollations);
            if (!groups.TryGetValue(key, out var gi))
            {
                gi = list.Count;
                groups[key] = gi;
                var states = new AggState[Aggs.Length];
                for (var i = 0; i < states.Length; i++) states[i] = new AggState(Aggs[i]);
                list.Add((kv, Frames.Save(c, Save), states));
            }
            foreach (var s in list[gi].States) s.Add(c);
        }
        if (list.Count == 0 && Keys.Length == 0)
        {
            var states = new AggState[Aggs.Length];
            for (var i = 0; i < states.Length; i++) states[i] = new AggState(Aggs[i]);
            list.Add(([], SaveNullRows, states));
        }
        return new Cursor(this, c, list);
    }

    sealed class Cursor(AggregateRel n, Ctx c, List<(Value[] Keys, Value[][] Frame, AggState[] States)> groups) : RowCursor
    {
        int _i;

        public override bool Next()
        {
            if (_i >= groups.Count) return false;
            var g = groups[_i++];
            Frames.Restore(c, n.Save, g.Frame);
            var row = new Value[n.Keys.Length + n.Aggs.Length];
            g.Keys.CopyTo(row, 0);
            for (var i = 0; i < n.Aggs.Length; i++) row[n.Keys.Length + i] = g.States[i].Result();
            c.Rows[n.OutStream] = row;
            return true;
        }
    }
}

// ============================================================================ window functions

public enum WinKind { RowNumber, Rank, DenseRank, PercentRank, CumeDist, NTile, Lag, Lead, FirstValue, LastValue, NthValue, Aggregate }

public sealed class WinSpec
{
    public WinKind Kind;
    public AggSpec? Agg;
    public XNode[] Args = [];
    public XNode[] Partition = [];
    public SortKey[] Order = [];
    public Sql.FrameSpec? Frame;
    public XNode? FrameStartOffset, FrameEndOffset;
    public SqlType Type = SqlType.Null;
}

public sealed class WindowRel : RelNode
{
    public readonly RelNode Input;
    public readonly WinSpec[] Specs;
    public readonly int OutStream;
    public readonly int[] Save;

    public WindowRel(RelNode input, WinSpec[] specs, int outStream, int[] save)
    {
        Input = input;
        Specs = specs;
        OutStream = outStream;
        Save = save;
        Streams = [.. input.Streams, outStream];
    }

    public override RowCursor Open(Ctx c)
    {
        var frames = new List<Value[][]>();
        // per spec: partition keys, order keys, arg values
        var part = new List<Value[]>[Specs.Length];
        var ord = new List<Value[]>[Specs.Length];
        var args = new List<Value[]>[Specs.Length];
        for (var s = 0; s < Specs.Length; s++)
        {
            part[s] = new();
            ord[s] = new();
            args[s] = new();
        }
        var ic = Input.Open(c);
        while (ic.Next())
        {
            frames.Add(Frames.Save(c, Save));
            for (var s = 0; s < Specs.Length; s++)
            {
                var sp = Specs[s];
                part[s].Add(Eval(c, sp.Partition));
                var ok = new Value[sp.Order.Length];
                for (var i = 0; i < ok.Length; i++) ok[i] = sp.Order[i].Expr.Eval(c);
                ord[s].Add(ok);
                var av = new Value[sp.Kind == WinKind.Aggregate ? 1 : sp.Args.Length];
                if (sp.Kind == WinKind.Aggregate)
                {
                    if (sp.Agg!.Arg != null) av[0] = sp.Agg.Arg.Eval(c);
                }
                else for (var i = 0; i < sp.Args.Length; i++) av[i] = sp.Args[i].Eval(c);
                args[s].Add(av);
            }
        }
        var n = frames.Count;
        var results = new Value[n][];
        for (var r = 0; r < n; r++) results[r] = new Value[Specs.Length];
        for (var s = 0; s < Specs.Length; s++)
            Compute(c, Specs[s], s, part[s], ord[s], args[s], results);
        return new Cursor(this, c, frames, results);
    }

    static Value[] Eval(Ctx c, XNode[] nodes)
    {
        var v = new Value[nodes.Length];
        for (var i = 0; i < nodes.Length; i++) v[i] = nodes[i].Eval(c);
        return v;
    }

    void Compute(Ctx c, WinSpec sp, int s, List<Value[]> part, List<Value[]> ord, List<Value[]> args, Value[][] results)
    {
        var collations = sp.Partition.Select(p => p.Type.Collation).ToArray();
        var partitions = new Dictionary<IndexKey, List<int>>();
        var order = new List<List<int>>();
        for (var r = 0; r < part.Count; r++)
        {
            var k = new IndexKey(part[r], collations);
            if (!partitions.TryGetValue(k, out var l))
            {
                partitions[k] = l = new();
                order.Add(l);
            }
            l.Add(r);
        }
        foreach (var rowsList in order)
        {
            var rows = rowsList.ToArray();
            if (sp.Order.Length > 0)
                Array.Sort(rows, (x, y) =>
                {
                    var r = SortRel.CompareKeys(ord[x], ord[y], sp.Order);
                    return r != 0 ? r : x.CompareTo(y);
                });
            var cnt = rows.Length;
            bool Peer(int a, int b) => sp.Order.Length == 0 || SortRel.CompareKeys(ord[rows[a]], ord[rows[b]], sp.Order) == 0;
            switch (sp.Kind)
            {
                case WinKind.RowNumber:
                    for (var i = 0; i < cnt; i++) results[rows[i]][s] = Value.Int(i + 1);
                    break;
                case WinKind.Rank:
                case WinKind.DenseRank:
                {
                    long rank = 0, dense = 0;
                    for (var i = 0; i < cnt; i++)
                    {
                        if (i == 0 || !Peer(i, i - 1))
                        {
                            rank = i + 1;
                            dense++;
                        }
                        results[rows[i]][s] = Value.Int(sp.Kind == WinKind.Rank ? rank : dense);
                    }
                    break;
                }
                case WinKind.PercentRank:
                {
                    long rank = 0;
                    for (var i = 0; i < cnt; i++)
                    {
                        if (i == 0 || !Peer(i, i - 1)) rank = i + 1;
                        results[rows[i]][s] = Value.Dbl(cnt <= 1 ? 0 : (double)(rank - 1) / (cnt - 1));
                    }
                    break;
                }
                case WinKind.CumeDist:
                {
                    for (var i = 0; i < cnt; i++)
                    {
                        var j = i;
                        while (j + 1 < cnt && Peer(j + 1, i)) j++;
                        results[rows[i]][s] = Value.Dbl((double)(j + 1) / cnt);
                    }
                    break;
                }
                case WinKind.NTile:
                {
                    for (var i = 0; i < cnt; i++)
                    {
                        var buckets = Conv.ToInt64(args[rows[i]][0]);
                        if (buckets <= 0) throw Err.Random("Argument for NTILE must be positive");
                        var small = cnt / buckets;
                        var large = cnt % buckets;
                        long bucket;
                        var boundary = large * (small + 1);
                        bucket = i < boundary ? i / (small + 1) + 1 : (i - boundary) / Math.Max(1, small) + large + 1;
                        results[rows[i]][s] = Value.Int(bucket);
                    }
                    break;
                }
                case WinKind.Lag:
                case WinKind.Lead:
                    for (var i = 0; i < cnt; i++)
                    {
                        var a = args[rows[i]];
                        var off = a.Length > 1 ? Conv.ToInt64(a[1]) : 1;
                        if (off < 0) throw Err.Random("Argument for LAG/LEAD must be zero or positive");
                        var j = sp.Kind == WinKind.Lag ? i - off : i + off;
                        results[rows[i]][s] = j >= 0 && j < cnt
                            ? Coercion.ToType(args[rows[(int)j]][0], sp.Type)
                            : a.Length > 2 ? Coercion.ToType(a[2], sp.Type) : Value.Null;
                    }
                    break;
                default:
                    FrameFunctions(c, sp, s, rows, ord, args, results);
                    break;
            }
        }
    }

    void FrameFunctions(Ctx c, WinSpec sp, int s, int[] rows, List<Value[]> ord, List<Value[]> args, Value[][] results)
    {
        var cnt = rows.Length;
        bool Peer(int a, int b) => sp.Order.Length == 0 || SortRel.CompareKeys(ord[rows[a]], ord[rows[b]], sp.Order) == 0;
        for (var i = 0; i < cnt; i++)
        {
            // frame bounds [lo, hi]
            int lo, hi;
            if (sp.Frame == null)
            {
                lo = 0;
                if (sp.Order.Length == 0) hi = cnt - 1;
                else
                {
                    hi = i;
                    while (hi + 1 < cnt && Peer(hi + 1, i)) hi++;
                }
            }
            else
            {
                lo = Bound(c, sp.Frame.Start, i, cnt, sp, rows, ord, true);
                hi = Bound(c, sp.Frame.End, i, cnt, sp, rows, ord, false);
            }
            Value v;
            switch (sp.Kind)
            {
                case WinKind.FirstValue:
                    v = lo <= hi ? args[rows[lo]][0] : Value.Null;
                    break;
                case WinKind.LastValue:
                    v = lo <= hi ? args[rows[hi]][0] : Value.Null;
                    break;
                case WinKind.NthValue:
                {
                    var nth = Conv.ToInt64(args[rows[i]][1]);
                    var idx = lo + (int)nth - 1;
                    v = nth > 0 && idx <= hi ? args[rows[idx]][0] : Value.Null;
                    break;
                }
                default:
                {
                    // aggregate over the frame: evaluate with a constant node feeding values
                    var feeder = new FeedNode();
                    var spec = new AggSpec
                    {
                        Kind = sp.Agg!.Kind, Arg = sp.Agg.Kind == AggKind.CountStar ? null : feeder, Distinct = sp.Agg.Distinct,
                        Type = sp.Agg.Type, Collation = sp.Agg.Collation, Separator = sp.Agg.Separator,
                    };
                    var st = new AggState(spec);
                    for (var j = lo; j <= hi; j++)
                    {
                        feeder.Current = args[rows[j]][0];
                        st.Add(c);
                    }
                    v = st.Result();
                    break;
                }
            }
            results[rows[i]][s] = Coercion.ToType(v, sp.Type);
        }
    }

    static int Bound(Ctx c, Sql.FrameBound b, int i, int cnt, WinSpec sp, int[] rows, List<Value[]> ord, bool start)
    {
        switch (b.Kind)
        {
            case Sql.FrameBoundKind.UnboundedPreceding: return 0;
            case Sql.FrameBoundKind.UnboundedFollowing: return cnt - 1;
            case Sql.FrameBoundKind.CurrentRow:
                if (sp.Frame!.Unit == Sql.FrameUnit.Rows || sp.Order.Length == 0) return start ? i : sp.Order.Length == 0 ? cnt - 1 : i;
                {
                    var j = i;
                    if (start) { while (j > 0 && SortRel.CompareKeys(ord[rows[j - 1]], ord[rows[i]], sp.Order) == 0) j--; }
                    else { while (j + 1 < cnt && SortRel.CompareKeys(ord[rows[j + 1]], ord[rows[i]], sp.Order) == 0) j++; }
                    return j;
                }
            default:
            {
                if (sp.Frame!.Unit != Sql.FrameUnit.Rows) throw Err.Unsupported("RANGE frames with offsets");
                var offNode = (start ? sp.FrameStartOffset : sp.FrameEndOffset) ?? throw Err.Unsupported("frame offset");
                var off = (int)Conv.ToInt64(offNode.Eval(c));
                var j = b.Kind == Sql.FrameBoundKind.Preceding ? i - off : i + off;
                return Math.Clamp(j, start ? 0 : -1, start ? cnt : cnt - 1);
            }
        }
    }

    sealed class Cursor(WindowRel n, Ctx c, List<Value[][]> frames, Value[][] results) : RowCursor
    {
        int _i;

        public override bool Next()
        {
            if (_i >= frames.Count) return false;
            Frames.Restore(c, n.Save, frames[_i]);
            c.Rows[n.OutStream] = results[_i];
            _i++;
            return true;
        }
    }
}

/// <summary>Node returning a value set by the caller (used to feed aggregates).</summary>
public sealed class FeedNode : XNode
{
    public Value Current;
    public override Value Eval(Ctx c) => Current;
}
