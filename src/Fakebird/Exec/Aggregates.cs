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
    bool _nullSeparator;

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
            // the sort that removes duplicates compares VARCHARs with their trailing blanks, except under an ICU
            // collation, whose key drops them
            var dk = v.Tag == VTag.String && _spec.Arg!.Type.Kind == TypeKind.VarChar && _spec.Collation == Collation.Binary
                ? Value.Str(v.AsString + "\uFFFF") : v;
            if (!_distinct.Add(new IndexKey([dk], [_spec.Collation]))) return;
            // the distinct values reach SUM/AVG in sort order, which decides how doubles round
            if (_spec.Kind is AggKind.Sum or AggKind.Avg)
            {
                (_sorted ??= new()).Add(v);
                return;
            }
        }
        Accumulate(v, c);
    }

    List<Value>? _sorted;

    void Accumulate(Value v, Ctx c)
    {
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
                    // a NULL separator makes the whole LIST NULL
                    if (sv.IsNull) { _nullSeparator = true; break; }
                    sep = Conv.ToStr(sv);
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
                    // DISTINCT values come back from the sort record, where a multi-byte CHAR is padded to its byte length
                    if (_spec.Distinct && _spec.Arg!.Type is { Kind: TypeKind.Char, BytesPerChar: > 1 } ct)
                        _list.Append(' ', Math.Max(0, ct.Length * ct.BytesPerChar - Conv.Utf8.GetByteCount(str)));
                }
                _any = true;
                break;
            }
        }
    }

    public Value Result()
    {
        if (_sorted != null)
        {
            var values = _sorted;
            _sorted = null;
            values.Sort((x, y) => Conv.Compare(x, y, _spec.Collation));
            foreach (var v in values) Accumulate(v, null!);
        }
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
                if (!_any || _nullSeparator) return Value.Null;
                return _blist != null ? Value.Bytes(_blist.ToArray()) : Value.Str(_list!.ToString());
        }
        return Value.Null;
    }
}

/// <summary>A field carried in Firebird's sort records: a column of an input stream that the query references.</summary>
public readonly record struct SortField(int Stream, int Col, SqlType Type);

/// <summary>
/// Order of sort records whose keys are equal. Firebird's in-memory sort compares whole records as native (little
/// endian) 32-bit words: after the keys come the values carried along (the NULL flags, then every referenced field of
/// the input streams in field order, then record numbers), so rows with equal keys ('abc' and 'ABC' under
/// UNICODE_CI, 'abc' and 'abc ') are ordered by those bytes, and rows with the same bytes keep their input order.
/// GROUP BY feeds a group's rows to the aggregates in this order and shows its last row; DISTINCT keeps the last.
/// </summary>
public static class SortImage
{
    /// <summary>
    /// Whether a field key's value has to be carried as data too: an ICU collation key cannot be turned back into the
    /// text. Other text keys hold the text itself (a VARCHAR's length is stored after the record numbers).
    /// </summary>
    public static bool Volatile(SqlType t) => t.IsString && t.Collation != Collation.Binary;

    /// <summary>The data part of the current row's sort record (SortedStream: zeroed, then flags and fields).</summary>
    public static byte[] Build(Ctx c, SortField[] fields)
        => Build(fields.Select(f => f.Type).ToArray(), fields.Select(f => c.Rows[f.Stream]?[f.Col] ?? Value.Null).ToArray());

    public static byte[] Build(SqlType[] types, Value[] values)
    {
        var size = types.Length;
        foreach (var t in types) size = Align(size, t) + Length(t);
        var data = new byte[(size + 3) & ~3];
        var at = types.Length;
        for (var i = 0; i < types.Length; i++)
        {
            at = Align(at, types[i]);
            if (values[i].IsNull) data[i] = 1;
            else Write(data.AsSpan(at, Length(types[i])), values[i], types[i]);
            at += Length(types[i]);
        }
        return data;
    }

    /// <summary>Ascending sort-key order of rows of key values, NULLs first (blobs sort by their ids: input order).</summary>
    public static IComparer<Value[]> KeyOrder(SqlType[] types) => Comparer<Value[]>.Create((a, b) =>
    {
        for (var i = 0; i < types.Length; i++)
        {
            if (a[i].IsNull || b[i].IsNull)
            {
                if (a[i].IsNull != b[i].IsNull) return a[i].IsNull ? -1 : 1;
                continue;
            }
            if (types[i].Kind == TypeKind.Blob) continue;
            var r = Conv.Compare(a[i], b[i], types[i].Collation);
            if (r != 0) return r;
        }
        return 0;
    });

    /// <summary>Compares two records' data parts as unsigned little-endian 32-bit words.</summary>
    public static int Compare(byte[] a, byte[] b)
    {
        var n = Math.Max(a.Length, b.Length);
        for (var w = 0; w < n; w += 4)
        {
            uint x = Word(a, w), y = Word(b, w);
            if (x != y) return x.CompareTo(y);
        }
        return 0;

        static uint Word(byte[] d, int w) => w + 4 <= d.Length ? System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(w)) : 0;
    }

    static int Length(SqlType t) => t.Kind switch
    {
        TypeKind.SmallInt => 2,
        TypeKind.Integer or TypeKind.Float or TypeKind.Date or TypeKind.Time => 4,
        TypeKind.BigInt or TypeKind.Double or TypeKind.Timestamp or TypeKind.Blob => 8,
        TypeKind.Int128 => 16,
        TypeKind.Boolean => 1,
        TypeKind.Char => t.Length * t.BytesPerChar,
        TypeKind.VarChar => 2 + t.Length * t.BytesPerChar,
        _ => 8,
    };

    // type_alignments, applied to every dtype from dtype_varying on (CHAR is not aligned)
    static int Align(int at, SqlType t)
    {
        var a = t.Kind switch
        {
            TypeKind.Char or TypeKind.Boolean => 1,
            TypeKind.SmallInt or TypeKind.VarChar => 2,
            TypeKind.BigInt or TypeKind.Double or TypeKind.Int128 => 8,
            _ => 4,
        };
        return (at + a - 1) / a * a;
    }

    static void Write(Span<byte> to, in Value v, SqlType t)
    {
        var le = System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian;
        switch (t.Kind)
        {
            case TypeKind.SmallInt: System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(to, (short)v.L); break;
            case TypeKind.Integer: System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(to, (int)v.L); break;
            case TypeKind.BigInt: le(to, v.L); break;
            case TypeKind.Int128:
                System.Buffers.Binary.BinaryPrimitives.WriteInt128LittleEndian(to, v.AsInt128);
                break;
            case TypeKind.Float: System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(to, (float)Conv.ToDouble(v)); break;
            case TypeKind.Double: System.Buffers.Binary.BinaryPrimitives.WriteDoubleLittleEndian(to, Conv.ToDouble(v)); break;
            case TypeKind.Date:
            case TypeKind.Time: System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(to, (int)v.L); break;
            case TypeKind.Timestamp:
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(to, (int)Math.Floor((double)v.L / Value.TicksPerDay));
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(to[4..], (uint)(((v.L % Value.TicksPerDay) + Value.TicksPerDay) % Value.TicksPerDay));
                break;
            case TypeKind.Boolean: to[0] = (byte)(v.AsBool ? 1 : 0); break;
            case TypeKind.Char:
            case TypeKind.VarChar:
            {
                var bytes = v.Tag == VTag.Bytes ? v.AsBytes : TextBytes(v.AsString, t.Charset);
                if (t.Kind == TypeKind.VarChar)
                {
                    var n = Math.Min(bytes.Length, to.Length - 2);
                    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(to, (ushort)n);
                    bytes.AsSpan(0, n).CopyTo(to[2..]);
                }
                else
                {
                    // CHAR is padded to its byte length: NUL for OCTETS, blanks otherwise
                    to.Fill(t.Charset == Charset.Octets ? (byte)0 : (byte)' ');
                    bytes.AsSpan(0, Math.Min(bytes.Length, to.Length)).CopyTo(to);
                }
                break;
            }
        }
    }

    static byte[] TextBytes(string s, Charset cs) => cs is Charset.Utf8 or Charset.UnicodeFss or Charset.None
        ? Conv.Utf8.GetBytes(s)
        : System.Text.Encoding.Latin1.GetBytes(s);
}

/// <summary>GROUP BY / aggregate operator. Output stream = [group keys..., aggregate results...].</summary>
public sealed class AggregateRel : RelNode
{
    public readonly RelNode Input;
    public readonly XNode[] Keys;
    public readonly Collation[] KeyCollations;
    public readonly AggSpec[] Aggs;
    public readonly int OutStream;
    /// <summary>Streams of the input whose rows are restored from the group's last row.</summary>
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

    /// <summary>The fields carried in the sort records of the grouping sort (see SortImage).</summary>
    public SortField[] SortFields = [];

    static readonly IComparer<byte[]> ImageOrder = Comparer<byte[]>.Create(SortImage.Compare);

    public override RowCursor Open(Ctx c)
    {
        var groups = new Dictionary<IndexKey, int>();
        var rows = new List<List<(Value[] Keys, byte[] Image, Frame Frame)>>();
        var ic = Input.Open(c);
        while (ic.Next())
        {
            var kv = new Value[Keys.Length];
            for (var i = 0; i < Keys.Length; i++) kv[i] = Keys[i].Eval(c);
            var key = new IndexKey(kv, KeyCollations);
            if (!groups.TryGetValue(key, out var gi))
            {
                gi = rows.Count;
                groups[key] = gi;
                rows.Add(new());
            }
            rows[gi].Add((kv, Keys.Length > 0 ? SortImage.Build(c, SortFields) : [], Frames.Save(c, Save)));
        }
        var list = new List<(Value[] Keys, Frame Frame, AggState[] States)>();
        foreach (var g in rows)
        {
            // Firebird groups sorted rows: a group's rows reach the aggregates in sort-record order, and the group
            // shows the values of its last row (e.g. which spelling of a _CI key)
            var ordered = Keys.Length > 0 ? g.OrderBy(r => r.Image, ImageOrder).ToList() : g;
            var states = new AggState[Aggs.Length];
            for (var i = 0; i < states.Length; i++) states[i] = new AggState(Aggs[i]);
            foreach (var r in ordered)
            {
                Frames.Restore(c, Save, r.Frame);
                foreach (var st in states) st.Add(c);
            }
            list.Add((ordered[^1].Keys, ordered[^1].Frame, states));
        }
        // the groups come out of the sort in key order (NULLs first)
        if (Keys.Length > 0) list = list.OrderBy(g => g.Keys, SortImage.KeyOrder(Keys.Select(k => k.Type).ToArray())).ToList();
        if (list.Count == 0 && Keys.Length == 0)
        {
            var states = new AggState[Aggs.Length];
            for (var i = 0; i < states.Length; i++) states[i] = new AggState(Aggs[i]);
            list.Add(([], Frames.Null(SaveNullRows), states));
        }
        return new Cursor(this, c, list);
    }

    sealed class Cursor(AggregateRel n, Ctx c, List<(Value[] Keys, Frame Frame, AggState[] States)> groups) : RowCursor
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
    /// <summary>NTH_VALUE ... FROM LAST.</summary>
    public bool FromLast;
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
        var frames = new List<Frame>();
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
                        if (buckets <= 0) throw ArgError(FbMessages.sysf_argnmustbe_positive, 1, "NTILE");
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
                        if (off < 0) throw ArgError(FbMessages.sysf_argnmustbe_nonneg, 2, sp.Kind == WinKind.Lag ? "LAG" : "LEAD");
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
                    if (nth <= 0) throw ArgError(FbMessages.sysf_argnmustbe_positive, 2, "NTH_VALUE");
                    var idx = sp.FromLast ? hi - (int)nth + 1 : lo + (int)nth - 1;
                    v = idx >= lo && idx <= hi ? args[rows[idx]][0] : Value.Null;
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

    static FbError ArgError(int code, int position, string function)
        => new StatusBuilder().Gds(code).Num(position).Str(function).Build();

    static int Bound(Ctx c, Sql.FrameBound b, int i, int cnt, WinSpec sp, int[] rows, List<Value[]> ord, bool start)
    {
        switch (b.Kind)
        {
            case Sql.FrameBoundKind.UnboundedPreceding: return 0;
            case Sql.FrameBoundKind.UnboundedFollowing: return cnt - 1;
            case Sql.FrameBoundKind.CurrentRow:
                if (sp.Frame!.Unit == Sql.FrameUnit.Rows) return i;
                // RANGE: the current row's peers (without ORDER BY, the whole partition)
                if (sp.Order.Length == 0) return start ? 0 : cnt - 1;
                {
                    var j = i;
                    if (start) { while (j > 0 && SortRel.CompareKeys(ord[rows[j - 1]], ord[rows[i]], sp.Order) == 0) j--; }
                    else { while (j + 1 < cnt && SortRel.CompareKeys(ord[rows[j + 1]], ord[rows[i]], sp.Order) == 0) j++; }
                    return j;
                }
            default:
            {
                var offNode = (start ? sp.FrameStartOffset : sp.FrameEndOffset) ?? throw Err.Unsupported("frame offset");
                if (sp.Frame!.Unit == Sql.FrameUnit.Range) return RangeBound(offNode.Eval(c), b.Kind, i, cnt, sp, rows, ord, start);
                var off = (int)Conv.ToInt64(offNode.Eval(c));
                var j = b.Kind == Sql.FrameBoundKind.Preceding ? i - off : i + off;
                return Math.Clamp(j, start ? 0 : -1, start ? cnt : cnt - 1);
            }
        }
    }

    /// <summary>
    /// RANGE n PRECEDING/FOLLOWING: the limit is the current row's (single) ORDER BY value moved by n, earlier in sort
    /// order for PRECEDING and later for FOLLOWING; a frame starts at the first row at or after its limit and ends at
    /// the last row at or before it. A NULL value frames its NULL peers.
    /// </summary>
    static int RangeBound(Value offset, Sql.FrameBoundKind kind, int i, int cnt, WinSpec sp, int[] rows, List<Value[]> ord, bool start)
    {
        var key = sp.Order[0];
        var cur = ord[rows[i]][0];
        if (cur.IsNull)
        {
            var j = i;
            if (start) { while (j > 0 && ord[rows[j - 1]][0].IsNull) j--; }
            else { while (j + 1 < cnt && ord[rows[j + 1]][0].IsNull) j++; }
            return j;
        }
        var limit = Shift(cur, offset, down: (kind == Sql.FrameBoundKind.Preceding) != key.Descending);
        var sign = key.Descending ? -1 : 1;
        int Side(int j) => sign * Conv.Compare(ord[rows[j]][0], limit, key.Collation);
        if (start)
        {
            var j = 0;
            while (j < cnt && (ord[rows[j]][0].IsNull || Side(j) < 0)) j++;
            return j;
        }
        var k = cnt - 1;
        while (k >= 0 && (ord[rows[k]][0].IsNull || Side(k) > 0)) k--;
        return k;
    }

    /// <summary>The order value moved by the frame offset (numbers at their own scale, days for dates and timestamps).</summary>
    static Value Shift(Value v, Value offset, bool down)
    {
        var dir = down ? -1 : 1;
        switch (v.Tag)
        {
            case VTag.Date: return Value.Date(v.L + dir * Conv.ToInt64(offset));
            case VTag.Timestamp: return Value.Timestamp(v.L + dir * (long)Math.Round(Conv.ToDouble(offset) * Value.TicksPerDay));
            case VTag.Time: return Value.Time(v.L + dir * (long)Math.Round(Conv.ToDouble(offset) * Value.TicksPerSecond));
            case VTag.Int or VTag.Int128:
            {
                var step = (Int128)Math.Round(Conv.ToDecimal(offset) * (decimal)Math.Pow(10, v.Scale));
                return Value.Wide(v.AsInt128 + dir * step, v.Scale);
            }
            default: return Value.Dbl(Conv.ToDouble(v) + dir * Conv.ToDouble(offset));
        }
    }

    sealed class Cursor(WindowRel n, Ctx c, List<Frame> frames, Value[][] results) : RowCursor
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
