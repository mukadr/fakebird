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

    /// <summary>Fields referenced while binding each enclosing query specification (see SortImage).</summary>
    readonly List<Dictionary<(int Stream, int Col), SqlType>> _fieldTrackers = new();

    ColNode Col(int stream, int index, SqlType type, string name = "", string? relation = null)
    {
        NoteStream(stream);
        foreach (var t in _fieldTrackers) t.TryAdd((stream, index), type);
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
        if (n == "UNICODE") return Collation.Unicode;
        return Collation.Binary;
    }

    /// <summary>The collations of UTF8 (other character sets are not checked).</summary>
    static readonly HashSet<string> Utf8Collations = ["UTF8", "UCS_BASIC", "UNICODE", "UNICODE_CI", "UNICODE_CI_AI"];

    static void CheckCollation(string name, Charset charset)
    {
        var n = name.ToUpperInvariant();
        if (charset == Charset.Utf8 && !Utf8Collations.Contains(n))
            throw new StatusBuilder().Gds(Err.isc_dsql_error).Gds(Err.isc_sqlerr).Num(-204)
                .Gds(FbMessages.collation_not_found).Str(n).Str("UTF8").Build();
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
        if (t.ByteLength > 32765 + (kind == TypeKind.Char ? 2 : 0))
            throw new StatusBuilder().Gds(Err.isc_dsql_error).Gds(Err.isc_sqlerr).Num(-204).Gds(Err.isc_dsql_datatype_err)
                .Gds(FbMessages.imp_exc).Gds(FbMessages.field_name).Str(ts.FieldName ?? "").Build();
        return t;
    }

    // ======================================================================== expressions

    static readonly HashSet<string> AggregateNames = new(StringComparer.Ordinal) { "COUNT", "SUM", "AVG", "MIN", "MAX", "LIST" };

    public static bool IsAggregate(FuncExpr f) => AggregateNames.Contains(f.Name) && f.Package == null;

    /// <summary>Binds an expression. <paramref name="expected"/> types parameter markers.</summary>
    public XNode Bind(Expr e, Scope s, SqlType? expected = null)
    {
        // in a grouped select list an expression equal to a GROUP BY key reads the key itself
        if (s is { AggMode: true, Agg: { } agg } && e is not (LiteralExpr or ColumnRefExpr or ParamExpr or VariableExpr)
            && agg.KeyCanon.Count > 0 && Canon(e, s) is { } canon)
        {
            var k = agg.KeyCanon.IndexOf(canon);
            if (k >= 0)
            {
                s.AggMode = false; // bound only for its describe name
                XNode named;
                try { named = Bind(e, s, expected); }
                finally { s.AggMode = true; }
                return new ColNode(agg.Stream, k, agg.KeyTypes[k]) { Name = named.Name, Field = named.Field, Relation = named.Relation };
            }
        }
        switch (e)
        {
            case LiteralExpr l:
            {
                // a negated literal is described with its positive literal's descriptor (see ConstNode.Literal)
                var k = new ConstNode(l.Value, l.Type) { Literal = l.Negated && l.Value.Tag == VTag.Int && l.Value.L != long.MinValue ? Value.Int(-l.Value.L, l.Value.Scale) : l.Value };
                // LiteralNode::genConstant sends an int64 literal that fits in 32 bits as blr_long
                if (l.Type is { Kind: TypeKind.BigInt } && l.Value.Tag == VTag.Int && l.Value.L is >= int.MinValue and <= int.MaxValue)
                    k.EngineType = TypeRules.Exact(TypeKind.Integer, l.Type.Scale, l.Type.SubType);
                return k;
            }
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
                        if (u.Operand is ParamExpr && expected == null)
                            throw new StatusBuilder().Gds(Err.isc_dsql_error).Gds(Err.isc_expression_eval_err).Gds(FbMessages.dsql_invalid_type_neg).Build();
                        var x = Bind(u.Operand, s, expected);
                        // NegateNode::make: NULL negates to INTEGER; strings, blobs and other non-numbers are rejected
                        if (x.Type.Kind == TypeKind.Null) return new ConstNode(Value.Null, SqlType.Integer) { Name = "CONSTANT" };
                        if (x.Type.IsString)
                            throw new StatusBuilder().Gds(Err.isc_dsql_error).Gds(Err.isc_expression_eval_err).Gds(FbMessages.dsql_nostring_neg_dial3).Build();
                        if (x.Type.Kind == TypeKind.Blob)
                            throw new StatusBuilder().Gds(Err.isc_dsql_error).Gds(Err.isc_sqlerr).Num(-607).Gds(FbMessages.dsql_no_blob_array).Build();
                        if (!x.Type.IsNumeric)
                            throw new StatusBuilder().Gds(Err.isc_dsql_error).Gds(Err.isc_expression_eval_err).Gds(FbMessages.dsql_invalid_type_neg).Build();
                        var neg = new NegNode(x) { Type = x.Type, Name = NegName(u), NotNull = x.NotNull, EngineType = x.EngineType };
                        if (x is ConstNode { Value.IsNull: false } k)
                        {
                            try { return new ConstNode(Arith.Neg(k.Value), k.Type) { Name = neg.Name, EngineType = k.EngineType, Literal = k.Literal }; }
                            catch (FbError) { return neg; } // raised when evaluated, like Firebird
                        }
                        return neg;
                    }
                    case UnaryOp.Plus: return Bind(u.Operand, s, expected);
                    default: return BindNot(u.Operand, s);
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
                var node = new ExistsNode { Singular = ex.Singular, Name = "" };
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
                if (x is ParamNode pn)
                {
                    pn.Name = "CAST";
                    return pn;
                }
                return Fold(new CastNode(x, t));
            }
            case CollateExpr co:
            {
                var x = Bind(co.Operand, s, expected);
                if (!x.Type.IsTextual)
                    throw new StatusBuilder().Gds(Err.isc_dsql_error).Gds(Err.isc_sqlerr).Num(-204)
                        .Gds(FbMessages.dsql_datatype_err).Gds(FbMessages.collation_requires_text).Build();
                CheckCollation(co.Collation, x.Type.Charset);
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
            throw new StatusBuilder().Gds(Err.isc_dsql_error).Gds(Err.isc_sqlerr).Num(-104).Gds(FbMessages.invalid_boolean_usage).Build();
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

    /// <summary>An expression with a parameter whose type must come from the context (not under a CAST).</summary>
    static bool HasUntypedParam(Expr e) => e switch
    {
        ParamExpr => true,
        UnaryExpr u => HasUntypedParam(u.Operand),
        BinaryExpr b => b.Op is not (BinaryOp.And or BinaryOp.Or) && (HasUntypedParam(b.Left) || HasUntypedParam(b.Right)),
        _ => false,
    };

    (XNode, XNode) BindPair(Expr l, Expr r, Scope s)
    {
        if (l is ParamExpr && r is ParamExpr) throw Err.DataTypeUnknown();
        // a side with parameters in an expression is typed from the other side (PASS1_set_parameter_type)
        if (l is not ParamExpr && HasUntypedParam(l) && !HasUntypedParam(r))
        {
            var rr = Bind(r, s);
            return (Bind(l, s, ParamTypeFor(rr.Type)), rr);
        }
        if (l is ParamExpr)
        {
            var rr = Bind(r, s);
            return (Bind(l, s, ParamTypeFor(rr.Type) ?? throw Err.DataTypeUnknown()), rr);
        }
        var ll = Bind(l, s);
        return (ll, Bind(r, s, ParamTypeFor(ll.Type) ?? (r is ParamExpr ? throw Err.DataTypeUnknown() : null)));
    }

    /// <summary>NegateNode::setParameterName: CONSTANT for negated literals, the label of a negated product or
    /// quotient, otherwise no name.</summary>
    static string NegName(UnaryExpr u)
    {
        Expr inner = u.Operand;
        var level = 0;
        while (inner is UnaryExpr { Op: UnaryOp.Neg } n)
        {
            inner = n.Operand;
            level++;
        }
        if (inner is LiteralExpr) return "CONSTANT";
        if (level == 0 && inner is BinaryExpr { Op: BinaryOp.Mul or BinaryOp.Div } b) return b.Op == BinaryOp.Mul ? "MULTIPLY" : "DIVIDE";
        return "";
    }

    /// <summary>
    /// NOT, the way Firebird's DSQL rewrites it: double negations cancel, NOT is pushed through AND/OR, and a
    /// negated quantified predicate becomes "is FALSE" (quantified predicates never yield UNKNOWN).
    /// </summary>
    XNode BindNot(Expr operand, Scope s)
    {
        switch (operand)
        {
            case UnaryExpr { Op: UnaryOp.Not } u:
                return BindBool(u.Operand, s);
            case BinaryExpr { Op: BinaryOp.And } b:
                return new OrNode(BindNot(b.Left, s), BindNot(b.Right, s));
            case BinaryExpr { Op: BinaryOp.Or } b:
                return new AndNode(BindNot(b.Left, s), BindNot(b.Right, s));
        }
        var x = BindBool(operand, s);
        if (x is QuantifiedNode q)
        {
            q.Negate = !q.Negate;
            return q;
        }
        return new NotNode(x);
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
                return new CmpNode(ToCmp(b.Op), l, r, MergeCollation(l.Type, r.Type));
            }
            case BinaryOp.Concat:
            {
                // parameters take the context's type; without one their type is unknown
                if ((b.Left is ParamExpr || b.Right is ParamExpr) && expected == null)
                {
                    UnknownParam = true;
                    expected = SqlType.VarChar(1);
                }
                var l = Bind(b.Left, s, expected);
                var r = Bind(b.Right, s, expected);
                var (t, nullable) = TypeRules.Concat(l.Type, !l.NotNull, r.Type, !r.NotNull);
                var node = new ArithNode(ArithOp.Concat, l, r) { Type = t, Name = "CONCATENATION", NotNull = !nullable };
                return Fold(node);
            }
            default:
                return BindArith(b, s, expected);
        }
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
            return new ConstNode(v, n.Type) { Name = n.Name, EngineType = n.EngineType };
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
        var op = b.Op switch
        {
            BinaryOp.Add => ArithOp.Add,
            BinaryOp.Sub => ArithOp.Sub,
            BinaryOp.Mul => ArithOp.Mul,
            _ => ArithOp.Div,
        };
        XNode l, r;
        if (b.Left is ParamExpr || b.Right is ParamExpr)
        {
            // PASS1_set_parameter_type: parameters take the type their context expects (assignment target,
            // other side of a comparison...). Without one, Firebird's ArithmeticNode::make fails on the unknown type.
            if (expected == null)
            {
                // the operation itself is fine; Firebird reports the parameter's unknown type at the end
                expected = UntypedArithParam(op, b, s);
                UnknownParam = true;
            }
            l = Bind(b.Left, s, expected);
            r = Bind(b.Right, s, expected);
        }
        else
        {
            l = Bind(b.Left, s, expected);
            r = Bind(b.Right, s, expected);
        }
        var (type, nullable) = TypeRules.Arith(op, l.Type, !l.NotNull, r.Type, !r.NotNull);
        var name = op switch { ArithOp.Add => "ADD", ArithOp.Sub => "SUBTRACT", ArithOp.Mul => "MULTIPLY", _ => "DIVIDE" };
        SqlType? engine = null;
        if (l.EngineType != null || r.EngineType != null)
        {
            var et = TypeRules.Arith(op, l.EvalType, false, r.EvalType, false).Type;
            if (!et.Equals(type)) engine = et;
        }
        return Fold(new ArithNode(op, l, r) { Type = type, Name = name, NotNull = !nullable, EngineType = engine });
    }

    /// <summary>A parameter's type stayed unknown (reported when the statement is complete).</summary>
    public bool UnknownParam;

    /// <summary>
    /// An operator on a parameter of unknown type: raises Firebird's ArithmeticNode::make error, or returns the
    /// other operand's type when the operation is valid (the parameter then stays of unknown type).
    /// </summary>
    SqlType UntypedArithParam(ArithOp op, BinaryExpr b, Scope s)
    {
        FbError Eval(int code) => new StatusBuilder().Gds(Err.isc_dsql_error).Gds(Err.isc_expression_eval_err).Gds(code).Build();
        if (op == ArithOp.Mul) throw Eval(FbMessages.dsql_invalid_type_multip_dial3);
        if (op == ArithOp.Div) throw Eval(FbMessages.dsql_invalid_type_div_dial3);
        if (b.Left is ParamExpr && b.Right is ParamExpr) throw Eval(FbMessages.dsql_invalid_type_addsub_dial3);
        var other = Bind(b.Left is ParamExpr ? b.Right : b.Left, s);
        if (other.Type.IsString) throw Eval(FbMessages.dsql_nostring_addsub_dial3);
        if (other.Type.IsDateTime)
            throw Eval(op == ArithOp.Add ? FbMessages.dsql_invalid_dateortime_add : FbMessages.dsql_invalid_datetime_subtract);
        return other.Type;
    }

    public static SqlType ArithType(ArithOp op, SqlType a, SqlType b)
    {
        if (a.Kind == TypeKind.Null && b.Kind == TypeKind.Null) return SqlType.Null;
        if (a.Kind == TypeKind.Null || b.Kind == TypeKind.Null)
        {
            var other = a.Kind == TypeKind.Null ? b : a;
            if (other.IsExact) return SqlType.ExactExpr(TypeKind.BigInt, other.Scale);
            return other.IsString ? SqlType.Double : other;
        }
        if (a.IsTextual || b.IsTextual)
            throw Err.Dsql(-104, op is ArithOp.Add or ArithOp.Sub
                ? "Strings cannot be added or subtracted in dialect 3" : "Strings cannot be multiplied or divided in dialect 3");
        if (a.IsDateTime || b.IsDateTime)
        {
            if (op == ArithOp.Add)
            {
                if (a.Kind == TypeKind.Date && b.Kind == TypeKind.Time || a.Kind == TypeKind.Time && b.Kind == TypeKind.Date)
                    return SqlType.Timestamp;
                if (a.IsDateTime && b.IsDateTime) throw Err.Dsql(-104, "Arithmetic on two date/time values is not allowed");
                return a.IsDateTime ? a : b;
            }
            if (op == ArithOp.Sub)
            {
                if (a.IsDateTime && b.IsDateTime)
                {
                    if (a.Kind == TypeKind.Date && b.Kind == TypeKind.Date) return SqlType.Integer;
                    if (a.Kind == TypeKind.Time && b.Kind == TypeKind.Time) return new SqlType(TypeKind.Integer, scale: 4, precision: 9, subType: 1);
                    if (a.Kind == TypeKind.Time || b.Kind == TypeKind.Time) throw Err.Dsql(-104, "Invalid date/time subtraction");
                    return new SqlType(TypeKind.BigInt, scale: 9, precision: 18, subType: 1);
                }
                if (a.IsDateTime) return a;
            }
            throw Err.Dsql(-104, "Invalid arithmetic operation on date/time values");
        }
        if (a.Kind == TypeKind.Boolean || b.Kind == TypeKind.Boolean) throw Err.Dsql(-104, "Invalid arithmetic on BOOLEAN");
        if (a.IsApprox || b.IsApprox) return SqlType.Double;
        if (a.Kind == TypeKind.Blob || b.Kind == TypeKind.Blob) throw Err.Dsql(-104, "Invalid arithmetic on BLOB");
        return op switch
        {
            ArithOp.Add or ArithOp.Sub => SqlType.ExactExpr(TypeKind.BigInt, Math.Max(a.Scale, b.Scale)),
            _ => SqlType.ExactExpr(TypeKind.BigInt, Math.Min(18, a.Scale + b.Scale)),
        };
    }

    XNode BindLike(LikeExpr lk, Scope s)
    {
        var x = Bind(lk.Operand, s, lk.Operand is ParamExpr ? SqlType.VarChar(8191) : null);
        var p = Bind(lk.Pattern, s, SqlType.VarChar(8191));
        var esc = lk.Escape == null ? null : Bind(lk.Escape, s, SqlType.VarChar(1));
        var col = MergeCollation(x.Type, p.Type);
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
        var branches = results.Select(r => bound.GetValueOrDefault(r)).ToList();
        if (ce.Else == null) branches.Add(new ConstNode(Value.Null, SqlType.Null)); // a CASE without ELSE can return NULL
        var (type, nullable) = TypeRules.FromList(branches, "CASE");
        if (bound.Count == 0) type = expected ?? throw Err.DataTypeUnknown();
        if (type.Kind == TypeKind.Null && results.Any(r => r is ParamExpr)) type = expected ?? throw Err.DataTypeUnknown();
        foreach (var r in results)
            if (r is ParamExpr) bound[r] = Bind(r, s, type);
        XNode? els = ce.Else == null ? null : bound[ce.Else];
        if (ce.Operand != null)
        {
            var op = Bind(ce.Operand, s);
            var whens = ce.Whens.Select(w => (Bind(w.When, s, ParamTypeFor(op.Type)), bound[w.Then])).ToArray();
            return new SimpleCaseNode(op, whens, els, MergeCollation(op.Type, op.Type), "CASE") { Type = type }; // a DecodeNode: always nullable
        }
        var sw = ce.Whens.Select(w => (BindBool(w.When, s), bound[w.Then])).ToArray();
        return new CaseNode(sw, els) { Type = type, NotNull = !nullable };
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
                var matching = sc.Items.Where(i => i.Matches(r.Qualifier) && (sc.Visible == null || sc.Visible.Contains(i))).ToList();
                if (matching.Count > 1 && matching.All(i => i.Find(r.Name) >= 0)) throw Ambiguous(r.Name, matching);
                foreach (var item in matching)
                {
                    var idx = item.Find(r.Name);
                    if (idx < 0 && r.Name == "RDB$DB_KEY" && item.Table is { IsSystem: false }) return new DbKeyNode(item.Stream);
                    if (idx < 0) throw Err.ColumnUnknown(r.Qualifier + "." + r.Name, r.Line, r.Col);
                    return MakeCol(s, sc, item, idx, qualified: true);
                }
            }
            else
            {
                var found = new List<(SourceItem Item, int Index)>();
                foreach (var item in sc.Items)
                {
                    if (item.QualifiedOnly || sc.Visible != null && !sc.Visible.Contains(item)) continue;
                    var idx = item.Find(r.Name);
                    if (idx < 0 || item.HiddenCols?.Contains(idx) == true) continue;
                    if (found.Count == 0 || found.All(f => f.Item.Stream != item.Stream)) found.Add((item, idx));
                }
                if (found.Count > 1) throw Ambiguous(r.Name, found.Select(f => f.Item).ToList());
                if (found.Count == 1) return MakeCol(s, sc, found[0].Item, found[0].Index);
                if (r.Name == "RDB$DB_KEY" && sc.Items.Count(i => !i.QualifiedOnly) == 1 && sc.Items.First(i => !i.QualifiedOnly) is { Table.IsSystem: false } only)
                    return new DbKeyNode(only.Stream);
            }
        }
        // a bare name is a variable only in PSQL expressions: inside a query or DML statement it must be :name
        if (Routine != null && ReferenceEquals(s, RootScope))
        {
            if (r.Qualifier == null && Routine.VarIndex.ContainsKey(r.Name)) return BindVariable(r.Name, r.Pos);
        }
        throw Err.ColumnUnknown(r.ToString(), r.Line, r.Col);
    }

    /// <summary>PASS1_ambiguity_check: the contexts, last one first, as "table R" and "table L and table M ".</summary>
    static FbError Ambiguous(string name, List<SourceItem> items)
    {
        var names = items.OrderByDescending(i => i.CtxOrder)
            .Select(i => i.Context + " " + (i.Context == "derived table" ? i.Alias ?? "" : i.Table?.Name ?? i.Name)).ToList();
        var rest = string.Concat(names.Skip(1).Select((n, k) => (k > 0 ? "and " : "") + n + " "));
        return new StatusBuilder().Gds(Err.isc_dsql_error).Gds(Err.isc_sqlerr).Num(-204)
            .Gds(FbMessages.dsql_ambiguous_field_name).Str(names[0]).Str(rest).Gds(Err.isc_random).Str(name).Build();
    }

    HashSet<(Table, int)>? _computing;

    /// <summary>
    /// COMPUTED BY columns are evaluated when read (like Firebird), against the row of their stream: inside
    /// the expression the bare table name is that row, even in subqueries over the same table (aliased).
    /// </summary>
    XNode BindComputed(SourceItem item, Table table, int idx, Expr computedBy, ColInfo ci)
    {
        _computing ??= new();
        if (!_computing.Add((table, idx)))
            throw Err.Dsql(-104, $"Column {table.Name}.{table.Columns[idx].Name} is computed from itself");
        try
        {
            var s = new Scope(null);
            s.Add(new SourceItem { Name = table.Name, Stream = item.Stream, Table = table, Cols = item.Cols });
            var node = Bind(computedBy, s);
            var type = table.Columns[idx].Type;
            if (!node.Type.Equals(type)) node = new CastNode(node, type);
            return new ComputedColNode(node, item.Stream, idx) { Type = type, Name = ci.Name, Field = ci.Field, Relation = ci.Relation };
        }
        finally
        {
            _computing.Remove((table, idx));
        }
    }

    XNode MakeCol(Scope from, Scope owner, SourceItem item, int idx, bool qualified = false)
    {
        if (!qualified && item.Merged != null && item.Merged.TryGetValue(idx, out var merged)) return merged(from, owner);
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
                throw owner.AggClause == "HAVING clause"
                    ? Err.Dsql104(FbMessages.dsql_agg_having_err, owner.AggClause)
                    : Err.Dsql104(FbMessages.dsql_agg_column_err, owner.AggClause);
        }
        var ci = item.Cols[idx];
        if (item.Table is { IsSystem: false } table && idx < table.Columns.Count && table.Columns[idx].ComputedBy is { } computedBy)
            return BindComputed(item, table, idx, computedBy, ci);
        var n = Col(item.Stream, idx, ci.Type, ci.Name, ci.Relation);
        n.Field = ci.Field;
        n.Relation = ci.Relation;
        n.NotNull = ci.NotNull && !item.OuterNullable;
        if (owner.AggMode && ci.ViaUnion)
        {
            n.Relation = "";
            n.Field = "";
        }
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
            case IsNullExpr isn:
                sb.Append(isn.Not ? "NN(" : "N(");
                if (!CanonInto(isn.Operand, s, sb)) return false;
                sb.Append(')');
                return true;
            case IsDistinctExpr d:
                sb.Append(d.Not ? "ND(" : "D(");
                if (!CanonInto(d.Left, s, sb)) return false;
                sb.Append(',');
                if (!CanonInto(d.Right, s, sb)) return false;
                sb.Append(')');
                return true;
            case IsBoolExpr ib:
                sb.Append("IS").Append(ib.Not ? "N" : "").Append(ib.Value?.ToString() ?? "U").Append('(');
                if (!CanonInto(ib.Operand, s, sb)) return false;
                sb.Append(')');
                return true;
            case BetweenExpr bt:
                sb.Append(bt.Not ? "NBT(" : "BT(");
                if (!CanonInto(bt.Operand, s, sb) || !CanonInto(bt.Low, s, sb.Append(',')) || !CanonInto(bt.High, s, sb.Append(','))) return false;
                sb.Append(')');
                return true;
            case LikeExpr lk:
                sb.Append("LK").Append(lk.Kind).Append(lk.Not ? "N(" : "(");
                if (!CanonInto(lk.Operand, s, sb) || !CanonInto(lk.Pattern, s, sb.Append(','))) return false;
                if (lk.Escape != null && !CanonInto(lk.Escape, s, sb.Append(','))) return false;
                sb.Append(')');
                return true;
            case InListExpr il:
                sb.Append(il.Not ? "NIN(" : "IN(");
                if (!CanonInto(il.Operand, s, sb)) return false;
                foreach (var x in il.List)
                    if (!CanonInto(x, s, sb.Append(','))) return false;
                sb.Append(')');
                return true;
            default:
                return false;
        }
    }
}

public static class SqlCodes
{
    /// <summary>SQLCODE like gds__sqlcode: the isc_sqlerr argument, else the primary code's SQLCODE, else -999.</summary>
    public static int SqlCodeOf(FbError e)
    {
        var st = e.Status;
        for (var i = 0; i < st.Count; i++)
            if (st[i].Type == Wire.Isc.arg_gds && st[i].Number == Err.isc_sqlerr && i + 1 < st.Count)
                return st[i + 1].Number;
        return FbMessages.ByCode.TryGetValue(e.GdsCode, out var m) && m.SqlCode != -999 ? m.SqlCode : -999;
    }

    static readonly Dictionary<string, int> GdsNames = FbMessages.ByCode
        .GroupBy(kv => kv.Value.Symbol, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.First().Key, StringComparer.OrdinalIgnoreCase);

    public static int GdsCodeByName(string name)
        => GdsNames.TryGetValue(name, out var c) ? c : throw Err.Dsql(-104, "Invalid GDSCODE name " + name);

    /// <summary>SQLSTATE like fb_sqlstate: the first code (skipping isc_random/isc_sqlerr) with a specific state.</summary>
    public static string SqlStateOf(IReadOnlyList<StatusArg> status, string? fallback = null)
    {
        string? state = null;
        foreach (var a in status)
        {
            if (a.Type != Wire.Isc.arg_gds || a.Number is 0 or Err.isc_random or Err.isc_sqlerr) continue;
            if (!FbMessages.ByCode.TryGetValue(a.Number, out var m) || m.SqlState is "00000" or "") continue;
            state = m.SqlState;
            if (state is not ("22000" or "42000" or "HY000")) return state;
        }
        // Fakebird-specific errors (isc_random only) keep the state their factory chose
        return state ?? fallback ?? "HY000";
    }
}

internal static class Num
{
    public static string I(long v) => v.ToString(CultureInfo.InvariantCulture);
}
