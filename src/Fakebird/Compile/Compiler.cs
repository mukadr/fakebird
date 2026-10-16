using System.Globalization;
using System.Text;
using Fakebird.Engine;
using Fakebird.Exec;
using Fakebird.Sql;
using Fakebird.Types;

namespace Fakebird.Compile;

/// <summary>
/// Binds parsed statements against the catalog and produces executable node trees. One compiler instance
/// compiles one request (statement, routine or trigger); stream numbers are allocated per request.
/// </summary>
public sealed partial class Compiler
{
    internal readonly Database Db;
    readonly List<int> _widths = new();
    public SqlType?[] ParamTypes;
    internal RoutineScope? Routine;
    /// <summary>Scope holding routine-level record contexts (NEW/OLD, cursors); parent of statement scopes.</summary>
    internal Scope? RootScope;
    readonly List<HashSet<int>> _trackers = new();
    readonly List<Dictionary<string, CteInfo>> _ctes = new();

    public Compiler(Database db, int paramCount)
    {
        Db = db;
        ParamTypes = new SqlType?[paramCount];
    }

    public int StreamCount => _widths.Count;

    public int NewStream(int width)
    {
        _widths.Add(width);
        return _widths.Count - 1;
    }

    public void SetWidth(int stream, int width) => _widths[stream] = width;

    public Value[][] NullRows(int[] streams)
    {
        var r = new Value[streams.Length][];
        for (var i = 0; i < streams.Length; i++) r[i] = new Value[_widths[streams[i]]];
        return r;
    }

    public SqlType[] FinalParamTypes()
    {
        var r = new SqlType[ParamTypes.Length];
        for (var i = 0; i < r.Length; i++) r[i] = ParamTypes[i] ?? throw Err.DataTypeUnknown();
        return r;
    }

    // ======================================================================== stream tracking

    /// <summary>Runs <paramref name="bind"/> collecting the streams referenced by the produced nodes.</summary>
    internal T Track<T>(Func<T> bind, out HashSet<int> streams)
    {
        streams = new HashSet<int>();
        _trackers.Add(streams);
        try { return bind(); }
        finally { _trackers.RemoveAt(_trackers.Count - 1); }
    }

    void NoteStream(int stream)
    {
        foreach (var t in _trackers) t.Add(stream);
    }

    ColNode Col(int stream, int index, SqlType type, string name = "", string? relation = null)
    {
        NoteStream(stream);
        return new ColNode(stream, index, type) { Name = name, Field = name, Relation = relation };
    }

    /// <summary>Streams referenced by a bound node (outer references of subqueries included).</summary>
    public static HashSet<int> StreamsOf(XNode n)
    {
        var set = new HashSet<int>();
        Collect(n, set);
        return set;

        static void Collect(XNode n, HashSet<int> set)
        {
            switch (n)
            {
                case ColNode c: set.Add(c.Stream); return;
                case SubqueryNode q:
                    if (q.OuterStreams != null) set.UnionWith(q.OuterStreams);
                    if (q is QuantifiedNode qn) Collect(qn.Left, set);
                    return;
            }
            foreach (var ch in Children(n)) Collect(ch, set);
        }
    }

    public static IEnumerable<XNode> Children(XNode n) => n switch
    {
        ArithNode a => [a.L, a.R],
        NegNode x => [x.X],
        CmpNode c => [c.L, c.R],
        AndNode a => [a.L, a.R],
        OrNode o => [o.L, o.R],
        NotNode x => [x.X],
        IsNullNode x => [x.X],
        IsDistinctNode d => [d.L, d.R],
        IsBoolNode b => [b.X],
        BetweenNode b => [b.X, b.Lo, b.Hi],
        InListNode i => [i.X, .. i.List],
        CaseNode cs => [.. cs.Whens.SelectMany(w => new[] { w.When, w.Then }), .. cs.Else == null ? [] : new[] { cs.Else }],
        SimpleCaseNode sc => [sc.Operand, .. sc.Whens.SelectMany(w => new[] { w.Value, w.Then }), .. sc.Else == null ? [] : new[] { sc.Else }],
        CoalesceNode co => co.Args,
        CastNode ca => [ca.X],
        FuncNode f => f.Args,
        LikeNode l => l.Escape == null ? [l.X, l.Pattern] : [l.X, l.Pattern, l.Escape],
        CallFunctionNode u => u.Args,
        _ => [],
    };

    // ======================================================================== types

    public static Collation CollationOf(string name)
    {
        var n = name.ToUpperInvariant();
        if (n.EndsWith("_CI_AI", StringComparison.Ordinal) || n == "PT_BR" || n.EndsWith("_AI", StringComparison.Ordinal)) return Collation.CaseAccentInsensitive;
        if (n.EndsWith("_CI", StringComparison.Ordinal)) return Collation.CaseInsensitive;
        return Collation.Binary;
    }

    public static Charset CharsetOf(string? name) => name?.ToUpperInvariant() switch
    {
        null => Charset.Utf8,
        "UTF8" or "UTF-8" => Charset.Utf8,
        "UNICODE_FSS" => Charset.UnicodeFss,
        "OCTETS" or "BINARY" => Charset.Octets,
        "NONE" => Charset.None,
        "ASCII" => Charset.Ascii,
        "WIN1252" => Charset.Win1252,
        "WIN1250" => Charset.Win1250,
        "WIN1251" => Charset.Win1251,
        "ISO8859_1" or "LATIN1" => Charset.Iso8859_1,
        _ => Charset.None,
    };

    public SqlType ResolveType(TypeSpec ts) => ResolveType(ts, out _);

    public SqlType ResolveType(TypeSpec ts, out Domain? domain)
    {
        domain = null;
        SqlType t;
        if (ts.TypeOfTable != null)
        {
            var table = Db.GetTable(ts.TypeOfTable);
            var ci = table.FindColumn(ts.TypeOfColumn!);
            if (ci < 0) throw Err.ColumnUnknown(ts.TypeOfTable + "." + ts.TypeOfColumn);
            t = table.Columns[ci].Type;
        }
        else if (ts.Domain != null)
        {
            if (!Db.Domains.TryGetValue(ts.Domain, out domain)) throw Err.Dsql(-607, "Specified domain or source column " + ts.Domain + " does not exist");
            t = domain.Type;
            if (ts.TypeOf) domain = null;
        }
        else
        {
            t = ts.BaseType switch
            {
                "SMALLINT" => SqlType.SmallInt,
                "INTEGER" => SqlType.Integer,
                "BIGINT" => SqlType.BigInt,
                "FLOAT" => SqlType.Float,
                "DOUBLE" => SqlType.Double,
                "DATE" => SqlType.Date,
                "TIME" => SqlType.Time,
                "TIMESTAMP" => SqlType.Timestamp,
                "BOOLEAN" => SqlType.Boolean,
                "NUMERIC" => SqlType.Numeric(ts.Precision ?? 9, ts.Scale ?? 0),
                "DECIMAL" => SqlType.Numeric(ts.Precision ?? 9, ts.Scale ?? 0, isDecimal: true),
                "CHAR" => CharType(TypeKind.Char, ts.Length ?? 1, ts),
                "VARCHAR" => CharType(TypeKind.VarChar, ts.Length ?? throw Err.Dsql(-104, "VARCHAR length required"), ts),
                "BLOB" => ts.SubType == 1
                    ? new SqlType(TypeKind.Blob, subType: 1, charset: CharsetOf(ts.Charset) == Charset.Octets ? Charset.Utf8 : CharsetOf(ts.Charset))
                    : SqlType.BlobBinary,
                _ => throw Err.Unsupported("data type " + ts.BaseType),
            };
        }
        if (ts.Collate != null && t.IsTextual) t = t.WithCollation(CollationOf(ts.Collate));
        return t;
    }

    static SqlType CharType(TypeKind kind, int len, TypeSpec ts)
    {
        if (len < 1 || len > 32767) throw Err.Dsql(-842, "Length out of range");
        var cs = CharsetOf(ts.Charset);
        var t = new SqlType(kind, length: len, charset: cs);
        if (t.ByteLength > 32765 + (kind == TypeKind.Char ? 2 : 0)) throw Err.Dsql(-204, "Implementation limit exceeded: column too long");
        return t;
    }

    // ======================================================================== expressions

    static readonly HashSet<string> AggregateNames = new(StringComparer.Ordinal) { "COUNT", "SUM", "AVG", "MIN", "MAX", "LIST" };

    public static bool IsAggregate(FuncExpr f) => AggregateNames.Contains(f.Name) && f.Package == null;

    /// <summary>Binds an expression. <paramref name="expected"/> types parameter markers.</summary>
    public XNode Bind(Expr e, Scope s, SqlType? expected = null)
    {
        switch (e)
        {
            case LiteralExpr l:
                return new ConstNode(l.Value, l.Type);
            case ParamExpr p:
                return BindParam(p, expected);
            case VariableExpr v:
                return BindVariable(v.Name, v.Pos);
            case ColumnRefExpr c:
                return ResolveColumn(c, s);
            case StarExpr:
                throw Err.Dsql(-104, "Invalid use of *");
            case UnaryExpr u:
                switch (u.Op)
                {
                    case UnaryOp.Neg:
                    {
                        var x = Bind(u.Operand, s, expected);
                        if (x is ConstNode k && !k.Value.IsNull) return new ConstNode(Arith.Neg(k.Value), k.Type);
                        return new NegNode(x) { Type = x.Type, Name = "NEGATE" };
                    }
                    case UnaryOp.Plus: return Bind(u.Operand, s, expected);
                    default: return new NotNode(BindBool(u.Operand, s));
                }
            case BinaryExpr b:
                return BindBinary(b, s, expected);
            case QuantifiedExpr q:
            {
                var left = Bind(q.Left, s, null);
                var node = new QuantifiedNode
                {
                    Left = left, Op = ToCmp(q.Op), All = q.Quantifier == Quantifier.All,
                };
                BindSubquery(node, q.Query, s, 1);
                node.Collation = MergeCollation(left.Type, node.Query.Columns[0].Type);
                return node;
            }
            case IsNullExpr n:
                return new IsNullNode(Bind(n.Operand, s, SqlType.Null), n.Not);
            case IsDistinctExpr d:
            {
                var (l, r) = BindPair(d.Left, d.Right, s);
                return new IsDistinctNode(l, r, d.Not, MergeCollation(l.Type, r.Type));
            }
            case IsBoolExpr ib:
                return new IsBoolNode(BindBool(ib.Operand, s), ib.Value, ib.Not);
            case BetweenExpr bt:
            {
                XNode x;
                if (bt.Operand is ParamExpr)
                {
                    var lo0 = Bind(bt.Low, s);
                    x = Bind(bt.Operand, s, lo0.Type);
                    var hi0 = Bind(bt.High, s, x.Type);
                    return new BetweenNode(x, lo0, hi0, bt.Not, MergeCollation(x.Type, lo0.Type));
                }
                x = Bind(bt.Operand, s);
                var lo = Bind(bt.Low, s, x.Type);
                var hi = Bind(bt.High, s, x.Type);
                return new BetweenNode(x, lo, hi, bt.Not, MergeCollation(x.Type, lo.Type));
            }
            case LikeExpr lk:
                return BindLike(lk, s);
            case InListExpr il:
            {
                XNode x;
                XNode[] list;
                if (il.Operand is ParamExpr)
                {
                    var first = il.List.FirstOrDefault(i => i is not ParamExpr) ?? throw Err.DataTypeUnknown();
                    var ft = Bind(first, s);
                    x = Bind(il.Operand, s, ft.Type);
                }
                else x = Bind(il.Operand, s);
                list = il.List.Select(i => Bind(i, s, ParamTypeFor(x.Type))).ToArray();
                return new InListNode(x, list, il.Not, MergeCollation(x.Type, list.Length > 0 ? list[0].Type : x.Type));
            }
            case InQueryExpr iq:
            {
                var left = Bind(iq.Operand, s, null);
                var node = new QuantifiedNode { Left = left, Op = CmpOp.Eq, All = false, Negate = iq.Not };
                BindSubquery(node, iq.Query, s, 1);
                node.Collation = MergeCollation(left.Type, node.Query.Columns[0].Type);
                return node;
            }
            case ExistsExpr ex:
            {
                var node = new ExistsNode { Singular = ex.Singular, Name = ex.Singular ? "SINGULAR" : "EXISTS" };
                BindSubquery(node, ex.Query, s, -1);
                return node;
            }
            case SubqueryExpr sq:
            {
                var node = new ScalarSubqueryNode();
                BindSubquery(node, sq.Query, s, 1);
                var col = node.Query.Columns[0];
                node.Type = col.Type;
                node.Name = col.Name;
                node.Field = col.Field;
                node.Relation = col.Relation;
                return node;
            }
            case CaseExpr ce:
                return BindCase(ce, s, expected);
            case CastExpr ca:
            {
                var t = ResolveType(ca.Type);
                var x = Bind(ca.Operand, s, t);
                if (x is ParamNode) return x;
                if (x is ConstNode k) return new ConstNode(Conv.Cast(k.Value, t), t) { Name = "CAST" };
                return new CastNode(x, t);
            }
            case CollateExpr co:
            {
                var x = Bind(co.Operand, s, expected);
                if (!x.Type.IsTextual) throw Err.Dsql(-204, "COLLATE applies only to character types");
                x.Type = x.Type.WithCollation(CollationOf(co.Collation));
                return x;
            }
            case FuncExpr f:
                return BindFunction(f, s, expected);
            case WindowExpr w:
                if (s.Windows != null && s.Windows.TryGetValue(w, out var wn)) return wn;
                throw Err.Dsql(-104, "Window functions are not allowed in this context");
            case NextValueExpr nv:
            {
                var seq = Db.Sequences.TryGetValue(nv.Sequence, out var sq2) ? sq2 : throw Err.ObjectUnknown("Generator/Sequence", nv.Sequence);
                return new CtxFuncNode(c =>
                {
                    seq.Value += seq.Increment;
                    return Value.Int(seq.Value);
                }, SqlType.BigInt, "NEXT_VALUE") { NotNull = true };
            }
            case ContextVarExpr cv:
                return BindContextVar(cv);
            case KeywordArg ka:
                if (ka.Keyword == "DEFAULT") throw Err.Dsql(-104, "DEFAULT is not allowed here");
                return new ConstNode(Value.Str(ka.Keyword), SqlType.VarChar(Math.Max(1, ka.Keyword.Length)));
        }
        throw Err.Unsupported("expression " + e.GetType().Name);
    }

    public XNode BindBool(Expr e, Scope s)
    {
        var x = Bind(e, s, SqlType.Boolean);
        if (x.Type.Kind is not (TypeKind.Boolean or TypeKind.Null))
            throw Err.Dsql(-104, "Invalid usage of boolean expression");
        return x;
    }

    static SqlType? ParamTypeFor(SqlType t) => t.Kind == TypeKind.Null ? null : t;

    XNode BindParam(ParamExpr p, SqlType? expected)
    {
        if (expected == null) throw Err.DataTypeUnknown();
        var t = expected;
        if (t.Kind == TypeKind.Char && t.Charset != Charset.Octets) t = SqlType.VarChar(Math.Max(t.Length, 1), t.Charset, t.Collation);
        ParamTypes[p.Index] = t;
        return new ParamNode(p.Index, t);
    }

    XNode BindVariable(string name, int pos)
    {
        for (var r = Routine; r != null; r = null)
        {
            if (r.VarIndex.TryGetValue(name, out var i))
            {
                NoteStream(r.VarStream);
                var v = r.Vars[i];
                return new ColNode(r.VarStream, i, v.Type) { Name = name };
            }
        }
        throw Err.Dsql(-206, "Variable " + name + " is not defined", "42S22");
    }

    static CmpOp ToCmp(BinaryOp op) => op switch
    {
        BinaryOp.Eq => CmpOp.Eq,
        BinaryOp.Ne => CmpOp.Ne,
        BinaryOp.Lt => CmpOp.Lt,
        BinaryOp.Le => CmpOp.Le,
        BinaryOp.Gt => CmpOp.Gt,
        _ => CmpOp.Ge,
    };

    static Collation MergeCollation(SqlType a, SqlType b)
        => a.IsTextual && a.Collation != Collation.Binary ? a.Collation : b.IsTextual ? b.Collation : Collation.Binary;

    (XNode, XNode) BindPair(Expr l, Expr r, Scope s)
    {
        if (l is ParamExpr && r is ParamExpr) throw Err.DataTypeUnknown();
        if (l is ParamExpr)
        {
            var rr = Bind(r, s);
            return (Bind(l, s, ParamTypeFor(rr.Type) ?? throw Err.DataTypeUnknown()), rr);
        }
        var ll = Bind(l, s);
        return (ll, Bind(r, s, ParamTypeFor(ll.Type) ?? (r is ParamExpr ? throw Err.DataTypeUnknown() : null)));
    }

    XNode BindBinary(BinaryExpr b, Scope s, SqlType? expected)
    {
        switch (b.Op)
        {
            case BinaryOp.And:
            {
                var l = BindBool(b.Left, s);
                var r = BindBool(b.Right, s);
                return new AndNode(l, r);
            }
            case BinaryOp.Or:
            {
                var l = BindBool(b.Left, s);
                var r = BindBool(b.Right, s);
                return new OrNode(l, r);
            }
            case BinaryOp.Eq:
            case BinaryOp.Ne:
            case BinaryOp.Lt:
            case BinaryOp.Le:
            case BinaryOp.Gt:
            case BinaryOp.Ge:
            {
                var (l, r) = BindPair(b.Left, b.Right, s);
                CheckComparable(l.Type, r.Type);
                return new CmpNode(ToCmp(b.Op), l, r, MergeCollation(l.Type, r.Type));
            }
            case BinaryOp.Concat:
            {
                var l = Bind(b.Left, s, b.Left is ParamExpr ? SqlType.VarChar(8191) : null);
                var r = Bind(b.Right, s, b.Right is ParamExpr ? SqlType.VarChar(8191) : null);
                SqlType t;
                if (l.Type.Kind == TypeKind.Blob || r.Type.Kind == TypeKind.Blob) t = SqlType.BlobText;
                else
                {
                    var len = Math.Min(DisplayLength(l.Type) + DisplayLength(r.Type), 32765);
                    var cs = l.Type.IsString ? l.Type.Charset : r.Type.IsString ? r.Type.Charset : Charset.Utf8;
                    if (l.Type.IsString && r.Type.IsString && l.Type.Charset != r.Type.Charset)
                        cs = l.Type.Charset == Charset.Octets ? r.Type.Charset : l.Type.Charset;
                    if (cs == Charset.Octets && !(l.Type.IsOctets && r.Type.IsOctets)) cs = Charset.Utf8;
                    len = Math.Min(len, 32765 / (cs == Charset.Utf8 ? 4 : cs == Charset.UnicodeFss ? 3 : 1));
                    t = SqlType.VarChar(Math.Max(1, len), cs, MergeCollation(l.Type, r.Type));
                }
                var node = new ArithNode(ArithOp.Concat, l, r) { Type = t, Name = "CONCATENATION" };
                return Fold(node);
            }
            default:
                return BindArith(b, s, expected);
        }
    }

    static void CheckComparable(SqlType a, SqlType b)
    {
        if (a.Kind == TypeKind.Null || b.Kind == TypeKind.Null) return;
        if (a.Kind == TypeKind.Boolean != (b.Kind == TypeKind.Boolean) && !(a.IsString || b.IsString))
            throw Err.Dsql(-104, "Datatypes are not comparable in expression");
    }

    public static int DisplayLength(SqlType t) => t.Kind switch
    {
        TypeKind.Char or TypeKind.VarChar => t.Length,
        TypeKind.SmallInt => 6 + (t.Scale > 0 ? 1 : 0),
        TypeKind.Integer => 11 + (t.Scale > 0 ? 1 : 0),
        TypeKind.BigInt => 20 + (t.Scale > 0 ? 1 : 0),
        TypeKind.Float => 15,
        TypeKind.Double => 24,
        TypeKind.Date => 10,
        TypeKind.Time => 13,
        TypeKind.Timestamp => 24,
        TypeKind.Boolean => 5,
        TypeKind.Null => 0,
        _ => 255,
    };

    XNode Fold(XNode n)
    {
        if (!n.IsConstant || n is ConstNode) return n;
        try
        {
            var v = n.Eval(null!);
            return new ConstNode(v, n.Type) { Name = n.Name };
        }
        catch (FbError)
        {
            return n; // raise at execution time like Firebird
        }
        catch (NullReferenceException)
        {
            return n;
        }
    }

    XNode BindArith(BinaryExpr b, Scope s, SqlType? expected)
    {
        XNode l, r;
        if (b.Left is ParamExpr && b.Right is ParamExpr)
        {
            var t = expected ?? throw Err.DataTypeUnknown();
            l = Bind(b.Left, s, t);
            r = Bind(b.Right, s, t);
        }
        else if (b.Left is ParamExpr)
        {
            r = Bind(b.Right, s);
            l = Bind(b.Left, s, ArithParamType(r.Type, b.Op, expected));
        }
        else
        {
            l = Bind(b.Left, s);
            r = Bind(b.Right, s, b.Right is ParamExpr ? ArithParamType(l.Type, b.Op, expected) : null);
        }
        var op = b.Op switch
        {
            BinaryOp.Add => ArithOp.Add,
            BinaryOp.Sub => ArithOp.Sub,
            BinaryOp.Mul => ArithOp.Mul,
            _ => ArithOp.Div,
        };
        var type = ArithType(op, l.Type, r.Type);
        var name = op switch { ArithOp.Add => "ADD", ArithOp.Sub => "SUBTRACT", ArithOp.Mul => "MULTIPLY", _ => "DIVIDE" };
        return Fold(new ArithNode(op, l, r) { Type = type, Name = name });
    }

    static SqlType ArithParamType(SqlType other, BinaryOp op, SqlType? expected)
    {
        if (other.Kind == TypeKind.Null) return expected ?? throw Err.DataTypeUnknown();
        if (other.IsDateTime)
        {
            if (op == BinaryOp.Sub && expected is { IsNumeric: true }) return other;
            return other.Kind == TypeKind.Date ? SqlType.Integer : SqlType.Double;
        }
        if (other.IsString) return SqlType.Double;
        if (other.IsExact && op is BinaryOp.Mul or BinaryOp.Div) return SqlType.Exact(TypeKind.BigInt, other.Scale);
        return other;
    }

    public static SqlType ArithType(ArithOp op, SqlType a, SqlType b)
    {
        if (a.Kind == TypeKind.Null) return b.Kind == TypeKind.Null ? SqlType.Null : b.IsString ? SqlType.Double : b;
        if (b.Kind == TypeKind.Null) return a.IsString ? SqlType.Double : a;
        if (a.IsDateTime || b.IsDateTime)
        {
            if (op == ArithOp.Add)
            {
                if (a.IsDateTime && b.IsDateTime) throw Err.Dsql(-104, "Arithmetic on two date/time values is not allowed");
                return a.IsDateTime ? a : b;
            }
            if (op == ArithOp.Sub)
            {
                if (a.IsDateTime && b.IsDateTime)
                {
                    if (a.Kind == TypeKind.Date && b.Kind == TypeKind.Date) return SqlType.BigInt;
                    if (a.Kind == TypeKind.Time && b.Kind == TypeKind.Time) return new SqlType(TypeKind.Integer, scale: 4, precision: 9, subType: 1);
                    if (a.Kind == TypeKind.Time || b.Kind == TypeKind.Time) throw Err.Dsql(-104, "Invalid date/time subtraction");
                    return new SqlType(TypeKind.BigInt, scale: 9, precision: 18, subType: 1);
                }
                if (a.IsDateTime) return a;
            }
            throw Err.Dsql(-104, "Invalid arithmetic operation on date/time values");
        }
        if (a.Kind == TypeKind.Boolean || b.Kind == TypeKind.Boolean) throw Err.Dsql(-104, "Invalid arithmetic on BOOLEAN");
        if (a.IsApprox || b.IsApprox || a.IsTextual || b.IsTextual) return SqlType.Double;
        if (a.Kind == TypeKind.Blob || b.Kind == TypeKind.Blob) throw Err.Dsql(-104, "Invalid arithmetic on BLOB");
        return op switch
        {
            ArithOp.Add or ArithOp.Sub => SqlType.Exact(TypeKind.BigInt, Math.Max(a.Scale, b.Scale)),
            _ => SqlType.Exact(TypeKind.BigInt, Math.Min(18, a.Scale + b.Scale)),
        };
    }

    XNode BindLike(LikeExpr lk, Scope s)
    {
        var x = Bind(lk.Operand, s, lk.Operand is ParamExpr ? SqlType.VarChar(8191) : null);
        var p = Bind(lk.Pattern, s, SqlType.VarChar(8191));
        var esc = lk.Escape == null ? null : Bind(lk.Escape, s, SqlType.VarChar(1));
        var col = MergeCollation(x.Type, p.Type);
        if (lk.Kind == LikeKind.Containing) col = col == Collation.Binary ? Collation.CaseInsensitive : col;
        return new LikeNode(lk.Kind, x, p, esc, lk.Not, col);
    }

    XNode BindCase(CaseExpr ce, Scope s, SqlType? expected)
    {
        // result type: bind non-parameter results first
        var results = new List<Expr>(ce.Whens.Select(w => w.Then));
        if (ce.Else != null) results.Add(ce.Else);
        var bound = new Dictionary<Expr, XNode>(ReferenceEqualityComparer.Instance);
        foreach (var r in results)
            if (r is not ParamExpr) bound[r] = Bind(r, s);
        var type = bound.Count > 0 ? Builtins.CommonType(bound.Values) : expected ?? throw Err.DataTypeUnknown();
        if (type.Kind == TypeKind.Null && results.Any(r => r is ParamExpr)) type = expected ?? throw Err.DataTypeUnknown();
        foreach (var r in results)
            if (r is ParamExpr) bound[r] = Bind(r, s, type);
        XNode? els = ce.Else == null ? null : bound[ce.Else];
        if (ce.Operand != null)
        {
            var op = Bind(ce.Operand, s);
            var whens = ce.Whens.Select(w => (Bind(w.When, s, ParamTypeFor(op.Type)), bound[w.Then])).ToArray();
            return new SimpleCaseNode(op, whens, els, MergeCollation(op.Type, op.Type), "CASE") { Type = type };
        }
        var sw = ce.Whens.Select(w => (BindBool(w.When, s), bound[w.Then])).ToArray();
        return new CaseNode(sw, els) { Type = type };
    }

    XNode BindContextVar(ContextVarExpr cv)
    {
        switch (cv.Name)
        {
            case "CURRENT_DATE":
                return new CtxFuncNode(c => Value.Date(Conv.SplitTimestamp(c.Now.L).day), SqlType.Date, "CURRENT_DATE") { NotNull = true };
            case "CURRENT_TIME":
            case "LOCALTIME":
            {
                var prec = cv.Precision < 0 ? 0 : cv.Precision;
                var div = Value.Pow10[4 - Math.Min(prec, 3)];
                return new CtxFuncNode(c =>
                {
                    var t = Conv.SplitTimestamp(c.Now.L).time;
                    return Value.Time(t - t % div);
                }, SqlType.Time, cv.Name) { NotNull = true };
            }
            case "CURRENT_TIMESTAMP":
            case "LOCALTIMESTAMP":
            {
                var prec = cv.Precision < 0 ? 3 : cv.Precision;
                var div = Value.Pow10[4 - Math.Min(prec, 3)];
                return new CtxFuncNode(c => Value.Timestamp(c.Now.L - (c.Now.L % div + div) % div), SqlType.Timestamp, cv.Name) { NotNull = true };
            }
            case "CURRENT_USER":
            case "USER":
                return new CtxFuncNode(c => Value.Str(c.Att.User), SqlType.VarChar(63), "USER") { NotNull = true };
            case "CURRENT_ROLE":
                return new CtxFuncNode(c => Value.Str(c.Att.Role), SqlType.VarChar(63), "ROLE") { NotNull = true };
            case "CURRENT_CONNECTION":
                return new CtxFuncNode(c => Value.Int(c.Att.Id), SqlType.BigInt, "CURRENT_CONNECTION") { NotNull = true };
            case "CURRENT_TRANSACTION":
                return new CtxFuncNode(c => Value.Int(c.Tx.Id), SqlType.BigInt, "CURRENT_TRANSACTION") { NotNull = true };
            case "ROW_COUNT":
                return new CtxFuncNode(c => Value.Int(c.RowCount), SqlType.Integer, "ROW_COUNT") { NotNull = true };
            case "SQLCODE":
                return new CtxFuncNode(c => Value.Int(c.CurrentError == null ? 0 : SqlCodes.SqlCodeOf(c.CurrentError)), SqlType.Integer, "SQLCODE");
            case "GDSCODE":
                return new CtxFuncNode(c => Value.Int(c.CurrentError?.GdsCode ?? 0), SqlType.Integer, "GDSCODE");
            case "SQLSTATE":
                return new CtxFuncNode(c => Value.Str(c.CurrentError?.SqlState ?? "00000"), SqlType.Char(5), "SQLSTATE");
            case "INSERTING":
                RequireTrigger(cv.Name);
                return new CtxFuncNode(c => Value.Bool(c.TriggerAction == TriggerEvent.Insert), SqlType.Boolean, cv.Name);
            case "UPDATING":
                RequireTrigger(cv.Name);
                return new CtxFuncNode(c => Value.Bool(c.TriggerAction == TriggerEvent.Update), SqlType.Boolean, cv.Name);
            case "DELETING":
                RequireTrigger(cv.Name);
                return new CtxFuncNode(c => Value.Bool(c.TriggerAction == TriggerEvent.Delete), SqlType.Boolean, cv.Name);
        }
        throw Err.Unsupported("context variable " + cv.Name);
    }

    void RequireTrigger(string name)
    {
        if (Routine is not { IsTrigger: true }) throw Err.Dsql(-104, name + " is allowed only in triggers");
    }

    // ======================================================================== column resolution

    XNode ResolveColumn(ColumnRefExpr r, Scope s)
    {
        for (var sc = s; sc != null; sc = sc.Parent)
        {
            if (r.Qualifier != null)
            {
                foreach (var item in sc.Items)
                {
                    if (!item.Matches(r.Qualifier)) continue;
                    var idx = item.Find(r.Name);
                    if (idx < 0) throw Err.ColumnUnknown(r.Qualifier + "." + r.Name);
                    return MakeCol(s, sc, item, idx);
                }
            }
            else
            {
                SourceItem? found = null;
                var fi = -1;
                foreach (var item in sc.Items)
                {
                    if (item.QualifiedOnly) continue;
                    var idx = item.Find(r.Name);
                    if (idx < 0 || item.HiddenCols?.Contains(idx) == true) continue;
                    if (found != null && found.Stream != item.Stream)
                        throw Err.Dsql(-204, $"Ambiguous field name between {Describe(found)} and {Describe(item)}");
                    found = item;
                    fi = idx;
                }
                if (found != null) return MakeCol(s, sc, found, fi);
            }
        }
        if (Routine != null)
        {
            if (r.Qualifier == null && Routine.VarIndex.ContainsKey(r.Name)) return BindVariable(r.Name, r.Pos);
        }
        throw Err.ColumnUnknown(r.ToString());
    }

    static string Describe(SourceItem i) => (i.Table != null ? "table " : "") + (i.Alias ?? i.Name);

    XNode MakeCol(Scope from, Scope owner, SourceItem item, int idx)
    {
        if (!ReferenceEquals(from, owner))
        {
            for (var sc = from; sc != null && !ReferenceEquals(sc, owner); sc = sc.Parent)
            {
                sc.Correlated = true;
                (sc.OuterStreams ??= new()).Add(item.Stream);
            }
        }
        else if (owner.AggMode && owner.Agg != null && owner.Streams.Contains(item.Stream) && item.Stream != owner.Agg.Stream)
        {
            if (!owner.Agg.GroupedColumns.Contains((item.Stream, idx)))
                throw Err.Dsql(-104, "Invalid expression in the select list (not contained in either an aggregate function or the GROUP BY clause)");
        }
        var ci = item.Cols[idx];
        var n = Col(item.Stream, idx, ci.Type, ci.Name, ci.Relation);
        n.Field = ci.Field;
        n.Relation = ci.Relation;
        n.NotNull = ci.NotNull;
        return n;
    }

    // ======================================================================== canonical keys

    /// <summary>Canonical text of an expression with columns resolved (for GROUP BY matching).</summary>
    internal string? Canon(Expr e, Scope s)
    {
        var sb = new StringBuilder();
        return CanonInto(e, s, sb) ? sb.ToString() : null;
    }

    bool CanonInto(Expr e, Scope s, StringBuilder sb)
    {
        switch (e)
        {
            case LiteralExpr l:
                sb.Append("L(").Append(l.Value.Tag).Append(':').Append(l.Value.IsNull ? "null" : Conv.ToStr(l.Value)).Append(')');
                return true;
            case ColumnRefExpr c:
            {
                var saveMode = s.AggMode;
                s.AggMode = false;
                try
                {
                    var n = ResolveColumn(c, s) as ColNode;
                    if (n == null) return false;
                    sb.Append("C(").Append(n.Stream).Append(',').Append(n.Index).Append(')');
                    return true;
                }
                catch (FbError) { return false; }
                finally { s.AggMode = saveMode; }
            }
            case ParamExpr p:
                sb.Append("P").Append(p.Index);
                return true;
            case VariableExpr v:
                sb.Append("V(").Append(v.Name).Append(')');
                return true;
            case UnaryExpr u:
                sb.Append("U").Append(u.Op).Append('(');
                if (!CanonInto(u.Operand, s, sb)) return false;
                sb.Append(')');
                return true;
            case BinaryExpr b:
                sb.Append("B").Append(b.Op).Append('(');
                if (!CanonInto(b.Left, s, sb)) return false;
                sb.Append(',');
                if (!CanonInto(b.Right, s, sb)) return false;
                sb.Append(')');
                return true;
            case FuncExpr f:
                sb.Append("F").Append(f.Name).Append(f.Distinct ? "D" : "").Append(f.Star ? "*" : "").Append('(');
                foreach (var a in f.Args)
                {
                    if (!CanonInto(a, s, sb)) return false;
                    sb.Append(',');
                }
                sb.Append(')');
                return true;
            case KeywordArg k:
                sb.Append("K").Append(k.Keyword);
                return true;
            case CastExpr ca:
                sb.Append("CAST(");
                if (!CanonInto(ca.Operand, s, sb)) return false;
                sb.Append(" AS ").Append(ResolveType(ca.Type)).Append(')');
                return true;
            case CollateExpr co:
                sb.Append("COLL(");
                if (!CanonInto(co.Operand, s, sb)) return false;
                sb.Append(co.Collation).Append(')');
                return true;
            case CaseExpr ce:
                sb.Append("CASE(");
                if (ce.Operand != null && !CanonInto(ce.Operand, s, sb)) return false;
                foreach (var (w, t) in ce.Whens)
                {
                    sb.Append('W');
                    if (!CanonInto(w, s, sb)) return false;
                    sb.Append('T');
                    if (!CanonInto(t, s, sb)) return false;
                }
                if (ce.Else != null)
                {
                    sb.Append('E');
                    if (!CanonInto(ce.Else, s, sb)) return false;
                }
                sb.Append(')');
                return true;
            case ContextVarExpr cv:
                sb.Append("CV").Append(cv.Name);
                return true;
            default:
                return false;
        }
    }
}

public static class SqlCodes
{
    public static int SqlCodeOf(FbError e)
    {
        foreach (var a in e.Status)
        {
            if (a.Type != Wire.Isc.arg_gds) continue;
            switch (a.Number)
            {
                case Err.isc_unique_key_violation: return -803;
                case Err.isc_foreign_key: return -530;
                case Err.isc_check_constraint: return -297;
                case Err.isc_not_valid: return -625;
                case Err.isc_except: return -836;
                case Err.isc_convert_error: return -413;
                case Err.isc_arith_except: return -802;
                case Err.isc_deadlock:
                case Err.isc_update_conflict: return -913;
                case Err.isc_sing_select_err: return -811;
                case Err.isc_dsql_error: return -104;
            }
        }
        return -901;
    }

    static readonly Dictionary<string, int> GdsNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["arith_except"] = Err.isc_arith_except,
        ["convert_error"] = Err.isc_convert_error,
        ["deadlock"] = Err.isc_deadlock,
        ["not_valid"] = Err.isc_not_valid,
        ["update_conflict"] = Err.isc_update_conflict,
        ["foreign_key"] = Err.isc_foreign_key,
        ["except"] = Err.isc_except,
        ["check_constraint"] = Err.isc_check_constraint,
        ["sing_select_err"] = Err.isc_sing_select_err,
        ["unique_key_violation"] = Err.isc_unique_key_violation,
        ["no_dup"] = 335544349,
        ["lock_conflict"] = 335544345,
        ["string_truncation"] = Err.isc_string_truncation,
        ["exception_integer_divide_by_zero"] = Err.isc_divide_by_zero,
        ["exception_integer_overflow"] = Err.isc_integer_overflow,
        ["numeric_out_of_range"] = Err.isc_numeric_out_of_range,
        ["random"] = Err.isc_random,
    };

    public static int GdsCodeByName(string name)
        => GdsNames.TryGetValue(name, out var c) ? c : throw Err.Unsupported("GDSCODE " + name);
}

internal static class Num
{
    public static string I(long v) => v.ToString(CultureInfo.InvariantCulture);
}
