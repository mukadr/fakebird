using Fakebird.Engine;
using Fakebird.Exec;
using Fakebird.Sql;
using Fakebird.Types;

namespace Fakebird.Compile;

internal sealed class CteInfo
{
    public CteDef Def = null!;
    public bool Recursive;
    public RecursiveCteQuery? InProgress;
    public List<ColInfo>? Cols;
    public Scope? Outer;
}

public sealed partial class Compiler
{
    // ======================================================================== functions

    static readonly HashSet<string> WindowOnly = new(StringComparer.Ordinal)
    {
        "ROW_NUMBER", "RANK", "DENSE_RANK", "PERCENT_RANK", "CUME_DIST", "NTILE", "LAG", "LEAD",
        "FIRST_VALUE", "LAST_VALUE", "NTH_VALUE",
    };

    XNode BindFunction(FuncExpr f, Scope s, SqlType? expected)
    {
        if (f.Package != null) throw Err.Unsupported("packages (" + f.Package + "." + f.Name + ")");
        if (IsAggregate(f)) return BindAggregateRef(f, s);
        if (WindowOnly.Contains(f.Name)) throw Err.Dsql(-104, f.Name + " requires an OVER clause");
        switch (f.Name)
        {
            case "COALESCE":
            {
                if (f.Args.Count < 2) throw Err.Dsql(-104, "COALESCE requires at least 2 arguments");
                var args = BindCommon(f.Args, s, expected, "COALESCE", out var type, out var nullable);
                return Fold(new CoalesceNode(args) { Type = type, NotNull = !nullable });
            }
            case "NULLIF":
            {
                if (f.Args.Count != 2) throw Err.Dsql(-104, "NULLIF requires 2 arguments");
                var (a, b) = BindPair(f.Args[0], f.Args[1], s);
                var col = MergeCollation(a.Type, b.Type);
                return new FuncNode("CASE", (c, args, fn) =>
                {
                    var x = args[0].Eval(c);
                    if (x.IsNull) return x;
                    var y = args[1].Eval(c);
                    if (!y.IsNull && Conv.Compare(x, y, col) == 0) return Value.Null;
                    return x;
                }, [a, b], a.Type.Kind == TypeKind.Null ? SqlType.Char(1, Charset.None) : a.Type); // the first argument's type, CHAR(1) for NULL
            }
            case "IIF":
            {
                if (f.Args.Count != 3) throw Err.Dsql(-104, "IIF requires 3 arguments");
                var cond = BindBool(f.Args[0], s);
                var results = BindCommon(f.Args.Skip(1).ToList(), s, expected, "IIF", out var type, out var nullable);
                return new CaseNode([(cond, results[0])], results[1]) { Type = type, Name = "CASE", NotNull = !nullable };
            }
            case "DECODE":
            {
                if (f.Args.Count < 3) throw Err.Dsql(-104, "DECODE requires at least 3 arguments");
                var op = Bind(f.Args[0], s);
                var pairs = new List<(XNode, Expr)>();
                var i = 1;
                for (; i + 1 < f.Args.Count; i += 2) pairs.Add((Bind(f.Args[i], s, ParamTypeFor(op.Type)), f.Args[i + 1]));
                var resultExprs = pairs.Select(p => p.Item2).ToList();
                if (i < f.Args.Count) resultExprs.Add(f.Args[i]);
                var results = BindCommon(resultExprs, s, expected, "DECODE", out var type, out var nullable);
                var whens = pairs.Select((p, k) => (p.Item1, results[k])).ToArray();
                XNode? els = i < f.Args.Count ? results[^1] : null;
                return new SimpleCaseNode(op, whens, els, op.Type.Collation, "DECODE") { Type = type }; // Firebird's DecodeNode is always nullable
            }
            case "GEN_ID":
            {
                if (f.Args.Count != 2 || f.Args[0] is not ColumnRefExpr g) throw Err.Dsql(-104, "GEN_ID requires a generator name and a step");
                var seq = Db.Sequences.TryGetValue(g.Name, out var sq) ? sq : throw Err.ObjectUnknown("Generator/Sequence", g.Name);
                var step = Bind(f.Args[1], s, SqlType.BigInt);
                return new FuncNode("GEN_ID", (c, a, fn) =>
                {
                    var st = a[0].Eval(c);
                    seq.Value += st.IsNull ? 0 : Conv.ToInt64(st);
                    return Value.Int(seq.Value);
                }, [step], SqlType.BigInt, deterministic: false);
            }
        }
        var def = Builtins.Find(f.Name);
        if (def != null)
        {
            if (f.Star || f.Distinct) throw Err.Dsql(-104, "Invalid use of DISTINCT or * in " + f.Name);
            if (f.Args.Count < def.MinArgs || f.Args.Count > def.MaxArgs)
                throw Err.Dsql(-170, $"Invalid number of arguments for function {f.Name}");
            var args = new XNode?[f.Args.Count];
            for (var i = 0; i < args.Length; i++)
                if (f.Args[i] is not ParamExpr) args[i] = Bind(f.Args[i], s, null);
            for (var i = 0; i < args.Length; i++)
                if (args[i] == null)
                {
                    args[i] = Bind(f.Args[i], s, def.ParamHint?.Invoke(i, args) ?? throw Err.DataTypeUnknown());
                    // setParams* make a fresh descriptor: the parameter is described as NOT NULL
                    args[i]!.NotNull = true;
                }
            var bound = args.Select(a => a!).ToArray();
            var node = new FuncNode(f.Name == "SUBSTRING_SIMILAR" ? "SUBSTRING" : f.Name, def.Impl, bound, def.ResultType(bound), def.Deterministic)
            {
                NotNull = !(def.Nullable?.Invoke(bound) ?? bound.Any(b => !b.NotNull)),
            };
            return Fold(node);
        }
        if (Routine?.SubRoutines != null && Routine.SubRoutines.TryGetValue(f.Name, out var sub) && sub.IsFunction)
            return BindSubFunctionCall(sub, f, s);
        if (Db.Functions.TryGetValue(f.Name, out var fn2))
            return BindFunctionCall(fn2, f, s);
        throw new StatusBuilder().Gds(Err.isc_dsql_error).Gds(Err.isc_sqlerr).Num(-804).Gds(FbMessages.dsql_function_err)
            .Gds(Err.isc_random).Str(f.Name).Build();
    }

    XNode[] BindCommon(List<Expr> exprs, Scope s, SqlType? expected, string name, out SqlType type, out bool nullable)
    {
        var bound = new XNode?[exprs.Count];
        for (var i = 0; i < exprs.Count; i++)
            if (exprs[i] is not ParamExpr) bound[i] = Bind(exprs[i], s);
        (type, nullable) = TypeRules.FromList(bound, name);
        if (bound.All(b => b == null)) type = expected ?? throw Err.DataTypeUnknown();
        if (type.Kind == TypeKind.Null && exprs.Any(e => e is ParamExpr)) type = expected ?? throw Err.DataTypeUnknown();
        for (var i = 0; i < exprs.Count; i++)
            bound[i] ??= Bind(exprs[i], s, type);
        return bound.Select(b => b!).ToArray();
    }

    XNode BindAggregateRef(FuncExpr f, Scope s)
    {
        if (s.Agg == null || !s.AggMode)
            throw Err.Dsql104(FbMessages.dsql_agg_where_err);
        if (!s.Agg.AggByNode.TryGetValue(f, out var idx)
            && !(Canon(f, s) is { } key && s.Agg.AggIndex.TryGetValue(key, out idx)))
            throw Err.Dsql(-104, "Invalid aggregate reference");
        var spec = s.Agg.Aggs[idx];
        var n = Col(s.Agg.Stream, s.Agg.KeyCount + idx, spec.Type, f.Name);
        n.NotNull = spec.Kind is AggKind.Count or AggKind.CountStar;
        return n;
    }

    AggSpec MakeAggSpec(FuncExpr f, Scope s)
    {
        var spec = new AggSpec { Distinct = f.Distinct };
        switch (f.Name)
        {
            case "COUNT":
                spec.Kind = f.Star ? AggKind.CountStar : AggKind.Count;
                spec.Type = SqlType.BigInt;
                if (!f.Star)
                {
                    if (f.Args.Count != 1) throw Err.Dsql(-104, "COUNT requires 1 argument");
                    spec.Arg = Bind(f.Args[0], s, SqlType.VarChar(8191));
                    spec.Collation = spec.Arg.Type.Collation;
                }
                return spec;
            case "SUM":
            case "AVG":
            {
                spec.Kind = f.Name == "SUM" ? AggKind.Sum : AggKind.Avg;
                if (f.Star || f.Args.Count != 1) throw Err.Dsql(-104, f.Name + " requires 1 argument");
                spec.Arg = Bind(f.Args[0], s, SqlType.Double);
                var t = spec.Arg.Type;
                // SumAggNode/AvgAggNode::make: SUM of BIGINT/INT128 is INT128, AVG of INT128 is INT128, other exact
                // values give BIGINT, all keeping scale and subtype
                if (t.IsExact)
                {
                    var wide = t.Kind == TypeKind.Int128 || (spec.Kind == AggKind.Sum && t.Kind == TypeKind.BigInt);
                    spec.Type = TypeRules.Exact(wide ? TypeKind.Int128 : TypeKind.BigInt, t.Scale, t.SubType);
                }
                else if (t.IsApprox) spec.Type = SqlType.Double;
                else if (t.Kind == TypeKind.Null) spec.Type = SqlType.Char(1, Charset.None); // the NULL literal's own descriptor
                else
                    throw new StatusBuilder().Gds(Err.isc_dsql_error).Gds(Err.isc_expression_eval_err)
                        .Gds(FbMessages.dsql_agg2_wrongarg).Str(f.Name).Build();
                return spec;
            }
            case "MIN":
            case "MAX":
                spec.Kind = f.Name == "MIN" ? AggKind.Min : AggKind.Max;
                if (f.Star || f.Args.Count != 1) throw Err.Dsql(-104, f.Name + " requires 1 argument");
                spec.Arg = Bind(f.Args[0], s, SqlType.VarChar(8191));
                spec.Type = spec.Arg.Type;
                spec.Collation = spec.Arg.Type.Collation;
                return spec;
            case "LIST":
                spec.Kind = AggKind.List;
                if (f.Star || f.Args.Count is < 1 or > 2) throw Err.Dsql(-104, "LIST requires 1 or 2 arguments");
                spec.Arg = Bind(f.Args[0], s, SqlType.VarChar(8191));
                if (f.Args.Count > 1) spec.Separator = Bind(f.Args[1], s, SqlType.VarChar(8191));
                // ListAggNode::make: a blob keeps its subtype, any string (OCTETS too) gives a text blob in its charset
                var at = spec.Arg.Type;
                spec.Type = at.Kind == TypeKind.Blob ? at
                    : new SqlType(TypeKind.Blob, subType: 1, charset: at.IsString ? at.Charset : Charset.Ascii);
                spec.Collation = spec.Arg.Type.Collation;
                return spec;
        }
        throw Err.Unsupported("aggregate " + f.Name);
    }

    /// <summary>Collects aggregate calls (not inside subqueries and not window aggregates themselves).</summary>
    static void CollectAggregates(Expr? e, List<FuncExpr> into)
    {
        switch (e)
        {
            case null: return;
            case FuncExpr f:
                if (IsAggregate(f)) { into.Add(f); return; }
                foreach (var a in f.Args) CollectAggregates(a, into);
                return;
            case WindowExpr w:
                foreach (var a in w.Func.Args) CollectAggregates(a, into);
                foreach (var p in w.Window.PartitionBy ?? []) CollectAggregates(p, into);
                foreach (var o in w.Window.OrderBy) CollectAggregates(o.Expr, into);
                return;
            case SubqueryExpr or ExistsExpr:
                return;
            case InQueryExpr iq: CollectAggregates(iq.Operand, into); return;
            case QuantifiedExpr q: CollectAggregates(q.Left, into); return;
        }
        foreach (var ch in AstChildren(e)) CollectAggregates(ch, into);
    }

    static void CollectWindows(Expr? e, List<WindowExpr> into)
    {
        switch (e)
        {
            case null: return;
            case WindowExpr w: into.Add(w); return;
            case SubqueryExpr or ExistsExpr: return;
            case InQueryExpr iq: CollectWindows(iq.Operand, into); return;
            case QuantifiedExpr q: CollectWindows(q.Left, into); return;
        }
        foreach (var ch in AstChildren(e)) CollectWindows(ch, into);
    }

    static IEnumerable<Expr> AstChildren(Expr e) => e switch
    {
        UnaryExpr u => [u.Operand],
        BinaryExpr b => [b.Left, b.Right],
        IsNullExpr n => [n.Operand],
        IsDistinctExpr d => [d.Left, d.Right],
        IsBoolExpr ib => [ib.Operand],
        BetweenExpr bt => [bt.Operand, bt.Low, bt.High],
        LikeExpr l => l.Escape == null ? [l.Operand, l.Pattern] : [l.Operand, l.Pattern, l.Escape],
        InListExpr il => [il.Operand, .. il.List],
        CaseExpr c => [.. c.Operand == null ? [] : new[] { c.Operand }, .. c.Whens.SelectMany(w => new[] { w.When, w.Then }), .. c.Else == null ? [] : new[] { c.Else }],
        CastExpr ca => [ca.Operand],
        CollateExpr co => [co.Operand],
        FuncExpr f => f.Args,
        _ => [],
    };

    static bool ContainsAggregate(Expr? e)
    {
        var l = new List<FuncExpr>();
        CollectAggregates(e, l);
        return l.Count > 0;
    }

    // ======================================================================== subqueries

    void BindSubquery(SubqueryNode node, QueryExpr q, Scope s, int expectedColumns)
    {
        var boundary = new Scope(s);
        node.Query = CompileQueryExpr(q, boundary);
        if (expectedColumns > 0 && node.Query.Columns.Length != expectedColumns)
            throw CountMismatch();
        node.Uncorrelated = !boundary.Correlated;
        node.OuterStreams = boundary.OuterStreams;
        if (boundary.OuterStreams != null)
            foreach (var st in boundary.OuterStreams) NoteStream(st);
        if (boundary.Correlated && s.Parent != null)
        {
            // propagate correlation to enclosing scopes that own the referenced streams
        }
    }

    // ======================================================================== queries

    public QueryNode CompileQueryExpr(QueryExpr q, Scope? outer) => q switch
    {
        SelectQuery sq => CompileSelect(sq, outer),
        QuerySpec qs => CompileSpec(qs, outer, null, null),
        SetOpQuery so => CompileUnion(so, outer),
        _ => throw Err.Unsupported(q.GetType().Name),
    };

    public QueryNode CompileSelect(SelectQuery q, Scope? outer)
    {
        if (q.With != null) PushCtes(q, outer);
        try
        {
            if (q.Body is QuerySpec qs)
            {
                if (!qs.Limit.IsEmpty && !q.Limit.IsEmpty) throw Err.Dsql104(FbMessages.dsql_firstskip_rows);
                var spec = CompileSpec(qs, outer, q.OrderBy, q.Limit, q.WithLock);
                return spec;
            }
            var body = CompileQueryExpr(q.Body, outer);
            if (q.OrderBy.Count > 0)
            {
                var keys = new List<SortKey>();
                var cols = new List<int>();
                foreach (var o in q.OrderBy)
                {
                    var idx = OutputColumnIndex(o.Expr, body.Columns);
                    cols.Add(idx);
                    keys.Add(MakeSortKey(o, body.Columns[idx].Type));
                }
                body = new SortQuery { Input = body, KeyColumns = cols.ToArray(), Keys = keys.ToArray(), Columns = body.Columns };
            }
            return WrapLimit(body, q.Limit, outer);
        }
        finally
        {
            if (q.With != null) _ctes.RemoveAt(_ctes.Count - 1);
        }
    }

    static SortKey MakeSortKey(OrderItem o, SqlType t) => new()
    {
        Descending = o.Descending,
        NullsFirst = o.NullsFirst ?? !o.Descending,
        // sort keys of the ICU collations keep case and accents (tertiary strength): 'aB' sorts before 'Ab'
        Collation = t.Collation is Collation.CaseInsensitive or Collation.CaseAccentInsensitive ? Collation.Unicode : t.Collation,
        IsBlob = t.Kind == TypeKind.Blob,
    };

    /// <summary>ORDER BY of a UNION: only positions of the select list.</summary>
    static int OutputColumnIndex(Expr e, OutCol[] cols)
    {
        if (IsPosition(e, out var i) && i >= 1 && i <= cols.Length) return i - 1;
        throw new StatusBuilder().Gds(Err.isc_dsql_error).Gds(Err.isc_sqlerr).Num(-104)
            .Gds(FbMessages.dsql_command_err).Gds(FbMessages.order_by_err).Build();
    }

    /// <summary>An unsigned integer literal: a select list position in ORDER BY / GROUP BY (-1 is an expression).</summary>
    static bool IsPosition(Expr e, out int position)
    {
        position = 0;
        if (e is not LiteralExpr { Value.Tag: VTag.Int, Negated: false } lit || lit.Value.Scale != 0) return false;
        position = lit.Value.L > int.MaxValue ? int.MaxValue : (int)lit.Value.L;
        return true;
    }

    QueryNode WrapLimit(QueryNode body, LimitSpec limit, Scope? outer)
    {
        if (limit.IsEmpty) return body;
        var (first, skip) = BindLimit(limit, outer ?? new Scope(null));
        return new LimitQuery { Input = body, First = first, Skip = skip, Columns = body.Columns };
    }

    (XNode? First, XNode? Skip) BindLimit(LimitSpec limit, Scope s)
    {
        if (limit.RowsFrom != null)
        {
            var from = Bind(limit.RowsFrom, s, SqlType.BigInt);
            if (limit.RowsTo == null) return (from, null);
            var to = Bind(limit.RowsTo, s, SqlType.BigInt);
            var one = new ConstNode(Value.Int(1), SqlType.BigInt);
            var skip = new ArithNode(ArithOp.Sub, from, one) { Type = SqlType.BigInt };
            // ROWS m TO n is SKIP m - 1 FIRST n - m + 1
            var count = new ArithNode(ArithOp.Add, new ArithNode(ArithOp.Sub, to, from) { Type = SqlType.BigInt }, one) { Type = SqlType.BigInt };
            return (count, skip);
        }
        return (limit.First == null ? null : Bind(limit.First, s, SqlType.BigInt),
            limit.Skip == null ? null : Bind(limit.Skip, s, SqlType.BigInt));
    }

    QueryNode CompileUnion(SetOpQuery so, Scope? outer)
    {
        var selectListTypes = _selectListTypes;
        var l = CompileQueryExpr(so.Left, outer);
        _selectListTypes = selectListTypes;
        var r = CompileQueryExpr(so.Right, outer);
        if (l.Columns.Length != r.Columns.Length)
            throw CountMismatch();
        var cols = new OutCol[l.Columns.Length];
        for (var i = 0; i < cols.Length; i++)
        {
            var t = Builtins.CommonType([TypeOf(l.Columns[i]), TypeOf(r.Columns[i])], "UNION");
            var lc = l.Columns[i];
            // the first branch describes the column; when it must be cast to the union's type, only an alias or a
            // column name survives (as the name), like Firebird's pass1 union processing
            var cast = !SameDescriptor(lc.Type, t);
            cols[i] = new OutCol
            {
                Name = cast && lc.Derived ? "" : lc.Name, Field = cast ? "" : lc.Field, Relation = cast ? "" : lc.Relation, Type = t,
                Nullable = l.Columns[i].Nullable || r.Columns[i].Nullable,
            };
        }
        return new UnionQuery { Left = l, Right = r, All = so.Kind == SetOpKind.UnionAll, Columns = cols };

        static SqlType TypeOf(OutCol c) => c.NullLiteral ? SqlType.Null : c.Type;
    }

    static bool SameDescriptor(SqlType a, SqlType b)
        => a.Kind == b.Kind && a.Length == b.Length && a.Scale == b.Scale && a.Charset == b.Charset
            && (a.IsExact ? a.SubType == b.SubType : a.Kind != TypeKind.Blob || a.SubType == b.SubType);

    void PushCtes(SelectQuery q, Scope? outer)
    {
        var frame = new Dictionary<string, CteInfo>(StringComparer.Ordinal);
        _ctes.Add(frame);
        foreach (var def in q.With!)
        {
            // the first of two CTEs with one name is never used: Firebird warns about it with the error
            if (frame.ContainsKey(def.Name))
                throw new StatusBuilder().Gds(Err.isc_dsql_error).Gds(Err.isc_sqlerr).Num(-204)
                    .Gds(FbMessages.alias_conflict_err).Str(def.Name)
                    .Warn(FbMessages.sqlwarn).Num(-104).Warn(FbMessages.dsql_cte_not_used).Str(def.Name).Build();
            frame[def.Name] = new CteInfo { Def = def, Recursive = q.Recursive, Outer = outer };
        }
    }

    CteInfo? FindCte(string name)
    {
        for (var i = _ctes.Count - 1; i >= 0; i--)
            if (_ctes[i].TryGetValue(name, out var c)) return c;
        return null;
    }

    static bool References(QueryExpr q, string name)
    {
        switch (q)
        {
            case SelectQuery sq:
                if (sq.With != null && sq.With.Any(w => w.Name == name)) return false;
                return References(sq.Body, name);
            case SetOpQuery so: return References(so.Left, name) || References(so.Right, name);
            case QuerySpec qs:
                return qs.From.Any(f => RefIn(f, name));
        }
        return false;

        static bool RefIn(TableRef t, string name) => t switch
        {
            NamedTableRef n => n.Name == name,
            JoinRef j => RefIn(j.Left, name) || RefIn(j.Right, name),
            DerivedTableRef d => References(d.Query, name),
            _ => false,
        };
    }

    /// <summary>Compiles a CTE reference into a derived relation positioned in a new stream.</summary>
    (RelNode Rel, List<ColInfo> Cols) CompileCteRef(CteInfo cte)
    {
        if (cte.InProgress != null)
        {
            var cols = cte.Cols!;
            var st = NewStream(cols.Count);
            var rq = cte.InProgress;
            return (new FuncRel(c => rq.Working, st), cols);
        }
        var def = cte.Def;
        QueryNode node;
        if (cte.Recursive && def.Query.Body is SetOpQuery so && References(so.Right, def.Name) && !References(so.Left, def.Name))
        {
            var rq = new RecursiveCteQuery { All = so.Kind == SetOpKind.UnionAll };
            var anchor = CompileQueryExpr(so.Left, cte.Outer);
            rq.Anchor = anchor;
            var cols = ColsFor(def.Columns, anchor.Columns, def.Name);
            rq.Columns = anchor.Columns.Select((c, i) => new OutCol { Name = cols[i].Name, Field = c.Field, Relation = c.Relation, Type = c.Type, Nullable = c.Nullable }).ToArray();
            cte.InProgress = rq;
            cte.Cols = cols;
            try
            {
                rq.Recursive = CompileQueryExpr(so.Right, cte.Outer);
            }
            finally
            {
                cte.InProgress = null;
            }
            if (rq.Recursive.Columns.Length != anchor.Columns.Length) throw Err.Dsql(-104, "Column count mismatch in recursive CTE");
            for (var i = 0; i < rq.Columns.Length; i++)
                if (rq.Columns[i].Type.Kind == TypeKind.Null) // otherwise the anchor's types rule, like Firebird
                    rq.Columns[i].Type = rq.Recursive.Columns[i].Type;
            node = rq;
            if (def.Query.OrderBy.Count > 0 || !def.Query.Limit.IsEmpty) throw Err.Unsupported("ORDER BY / limits on recursive CTE");
        }
        else
        {
            var saved = _ctes.Count;
            node = CompileSelect(def.Query, cte.Outer);
        }
        var colInfos = ColsFor(def.Columns, node.Columns, def.Name);
        var stream = NewStream(colInfos.Count);
        var rel = new DerivedRel(node, stream, []) { Cacheable = cte.Outer == null };
        return (rel, colInfos);
    }

    /// <summary>
    /// Columns of a derived table or CTE: they describe like the inner select list (its relation, sqlname and
    /// nullability), while view columns (<paramref name="view"/>) describe as the view's own, always nullable.
    /// </summary>
    static List<ColInfo> ColsFor(List<string>? names, OutCol[] cols, string relation, bool view = false, bool viaUnion = false)
    {
        if (names != null && names.Count != cols.Length)
        {
            if (view) throw Err.Dsql(-104, "Column list count mismatch for " + relation);
            throw new StatusBuilder().Gds(Err.isc_dsql_error).Gds(Err.isc_sqlerr).Num(-104).Gds(FbMessages.dsql_command_err)
                .Gds(names.Count < cols.Length ? FbMessages.dsql_derived_table_less_columns : FbMessages.dsql_derived_table_more_columns)
                .Str(relation).Build();
        }
        var list = new List<ColInfo>();
        for (var i = 0; i < cols.Length; i++)
        {
            var name = names?[i] ?? cols[i].Name;
            if (!view && list.Any(c => c.Name == name))
                throw new StatusBuilder().Gds(Err.isc_dsql_error).Gds(Err.isc_sqlerr).Num(-104).Gds(FbMessages.dsql_command_err)
                    .Gds(FbMessages.dsql_derived_field_dup_name).Str(name).Str(relation).Build();
            list.Add(view
                ? new ColInfo { Name = name, Type = cols[i].Type, NotNull = false, Relation = relation, Field = name }
                : new ColInfo { Name = name, Type = cols[i].Type, NotNull = !cols[i].Nullable, Relation = cols[i].Relation, Field = cols[i].Field, ViaUnion = viaUnion });
        }
        return list;
    }

    static bool IsUnion(QueryNode q) => q is UnionQuery || q is LimitQuery { Input: UnionQuery };

    /// <summary>[dsql_error, sqlerr -104, dsql_command_err, dsql_count_mismatch]: a column count that does not fit.</summary>
    static FbError CountMismatch() => new StatusBuilder().Gds(Err.isc_dsql_error).Gds(Err.isc_sqlerr).Num(-104)
        .Gds(FbMessages.dsql_command_err).Gds(FbMessages.dsql_count_mismatch).Build();

    // ======================================================================== query specification

    /// <summary>
    /// Types of the target columns of INSERT ... SELECT: they type parameter markers in the select list
    /// (SELECT ?, ? FROM RDB$DATABASE). Consumed by the next top-level query specification.
    /// </summary>
    SqlType[]? _selectListTypes;
    /// <summary>The next query spec compiled is the statement's top-level one (see PreFilterRel.AtExecute).</summary>
    bool _openAtExecute;

    QueryNode CompileSpec(QuerySpec qs, Scope? outer, List<OrderItem>? orderBy, LimitSpec? queryLimit, bool withLock = false)
    {
        var refs = new Dictionary<(int Stream, int Col), SqlType>();
        _fieldTrackers.Add(refs);
        try { return CompileSpec(qs, outer, orderBy, queryLimit, withLock, refs); }
        finally { _fieldTrackers.Remove(refs); }
    }

    /// <summary>The referenced fields of the given streams, as Firebird's sort records carry them.</summary>
    static SortField[] SortFieldsOf(Dictionary<(int Stream, int Col), SqlType> refs, int[] streams, IEnumerable<XNode> keys)
        => refs.Where(r => streams.Contains(r.Key.Stream))
            .OrderBy(r => Array.IndexOf(streams, r.Key.Stream)).ThenBy(r => r.Key.Col)
            .Select(r => new SortField(r.Key.Stream, r.Key.Col, r.Value))
            // a field that is itself a sort key is not repeated, unless its key cannot be turned back into the value
            .Where(f => SortImage.Volatile(f.Type) || !keys.Any(k => k is ColNode cn && cn.Stream == f.Stream && cn.Index == f.Col))
            .ToArray();

    QueryNode CompileSpec(QuerySpec qs, Scope? outer, List<OrderItem>? orderBy, LimitSpec? queryLimit, bool withLock,
        Dictionary<(int Stream, int Col), SqlType> refs)
    {
        var selectListTypes = _selectListTypes;
        _selectListTypes = null;
        var atExecute = _openAtExecute;
        _openAtExecute = false;
        var s = new Scope(outer ?? RootScope);
        var tree = BuildFrom(qs.From, s);

        // WHERE conjuncts
        var pool = new List<Conj>();
        if (qs.Where != null)
        {
            if (ContainsAggregate(qs.Where)) throw Err.Dsql104(FbMessages.dsql_agg_where_err);
            foreach (var c in SplitAnd(qs.Where)) pool.Add(MakeConj(c, s));
        }
        BindJoinConditions(tree, s);
        // conjuncts that reference none of this query's streams are tested once, when the query opens
        var invariant = pool.Where(c => !c.Streams.Any(s.Streams.Contains)).ToList();
        foreach (var c in invariant) c.Used = true;
        var rel = PlanTree(tree, pool, s);
        if (invariant.Count > 0) rel = new PreFilterRel(rel, AndAll(invariant.Select(c => c.Node).ToList())!) { AtExecute = atExecute };
        foreach (var c in pool)
            if (!c.Used)
            {
                rel = new FilterRel(rel, c.Node);
                c.Used = true;
            }

        // WITH LOCK: every row that passes WHERE is locked like a dummy update (single base table only)
        if (withLock)
        {
            var tables = s.Items.Where(i => !i.Hidden && !i.QualifiedOnly).ToList();
            if (tables is not [{ Table: { IsSystem: false } lockTable } only] || qs.GroupBy.Count > 0 || qs.Distinct)
                throw Err.Dsql(-104, "WITH LOCK can be used only with a single physical table");
            rel = new LockRel(rel, lockTable, only.Stream);
        }

        // aggregation
        var aggs = new List<FuncExpr>();
        foreach (var item in qs.Items) CollectAggregates(item.Expr, aggs);
        CollectAggregates(qs.Having, aggs);
        if (orderBy != null) foreach (var o in orderBy) CollectAggregates(o.Expr, aggs);
        var grouped = qs.GroupBy.Count > 0 || aggs.Count > 0 || qs.Having != null;
        var saveStreams = s.Streams.ToArray();
        AggregateRel? aggRel = null;
        if (grouped)
        {
            var ab = new AggBinding();
            var keys = new List<XNode>();
            foreach (var g in qs.GroupBy)
            {
                Expr ge = g;
                // GROUP BY may reference select list positions/aliases (an alias wins over a column, like Firebird)
                if (g is ColumnRefExpr { Qualifier: null } byName
                    && qs.Items.Where(i => i.Alias == byName.Name && i.Expr is not StarExpr).Take(2).ToList() is [var aliased])
                    ge = aliased.Expr;
                if (IsPosition(g, out var p))
                {
                    if (p < 1 || p > qs.Items.Count) throw Err.Dsql104(FbMessages.dsql_column_pos_err, "GROUP BY");
                    ge = qs.Items[p - 1].Expr;
                }
                if (ContainsAggregate(ge)) throw Err.Dsql104(FbMessages.dsql_agg_group_err);
                var kn = Bind(ge, s);
                keys.Add(kn);
                ab.KeyCanon.Add(Canon(ge, s) ?? Guid.NewGuid().ToString());
                ab.KeyTypes.Add(kn.Type);
                if (kn is ColNode cn) ab.GroupedColumns.Add((cn.Stream, cn.Index));
                else if (kn is CoalesceNode { Field: "COALESCE" } merged) // a USING/NATURAL column
                    foreach (var a in merged.Args.OfType<ColNode>()) ab.GroupedColumns.Add((a.Stream, a.Index));
                else if (kn is ComputedColNode cc) ab.GroupedColumns.Add((cc.Stream, cc.Index));
            }
            ab.KeyCount = keys.Count;
            foreach (var a in aggs)
            {
                var key = Canon(a, s);
                if (key != null && ab.AggIndex.TryGetValue(key, out var same))
                {
                    ab.AggByNode[a] = same;
                    continue;
                }
                if (ContainsAggregate(a.Args.Count > 0 ? new FuncExpr("X", a.Args) : null))
                    throw Err.Dsql104(FbMessages.dsql_agg_nested_err);
                if (key != null) ab.AggIndex[key] = ab.Aggs.Count;
                ab.AggByNode[a] = ab.Aggs.Count;
                ab.Aggs.Add(MakeAggSpec(a, s));
                ab.AggExprs.Add(a);
            }
            ab.Stream = NewStream(keys.Count + ab.Aggs.Count);
            aggRel = new AggregateRel(rel, keys.ToArray(), ab.Aggs.ToArray(), ab.Stream, saveStreams, NullRows(saveStreams));
            rel = aggRel;
            s.Agg = ab;
            s.Streams.Add(ab.Stream);
            s.AggMode = true;
            if (qs.Having != null)
            {
                s.AggClause = "HAVING clause";
                rel = new FilterRel(rel, BindBool(qs.Having, s));
                s.AggClause = "select list";
            }
        }

        // window functions
        var wins = new List<WindowExpr>();
        foreach (var item in qs.Items) CollectWindows(item.Expr, wins);
        if (orderBy != null) foreach (var o in orderBy) CollectWindows(o.Expr, wins);
        if (wins.Count > 0)
        {
            var specs = wins.Select(w => MakeWinSpec(w, s)).ToArray();
            var ws = NewStream(specs.Length);
            s.Windows = new Dictionary<WindowExpr, XNode>(ReferenceEqualityComparer.Instance);
            for (var i = 0; i < wins.Count; i++)
                s.Windows[wins[i]] = new ColNode(ws, i, specs[i].Type)
                {
                    Name = wins[i].Func.Name,
                    // ranking functions describe as NOT NULL; value functions and aggregates (but COUNT) as nullable
                    NotNull = specs[i].Kind is WinKind.RowNumber or WinKind.Rank or WinKind.DenseRank or WinKind.PercentRank
                        or WinKind.CumeDist or WinKind.NTile || specs[i].Agg?.Kind is AggKind.Count or AggKind.CountStar,
                };
            rel = new WindowRel(rel, specs, ws, s.Streams.ToArray());
            s.Streams.Add(ws);
        }

        // select list
        var proj = new List<XNode>();
        var outCols = new List<OutCol>();
        var aliasIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in qs.Items)
        {
            if (item.Expr is StarExpr star)
            {
                if (grouped) throw Err.Dsql104(FbMessages.dsql_agg_column_err, "select list");
                var any = false;
                foreach (var src in s.Items)
                {
                    if (src.Hidden || src.QualifiedOnly) continue;
                    if (star.Qualifier != null && !src.Matches(star.Qualifier)) continue;
                    any = true;
                    for (var i = 0; i < src.Cols.Count; i++)
                    {
                        // t.* lists all of t's columns; * shows a USING/NATURAL column once, merged
                        if (star.Qualifier == null && src.HiddenCols?.Contains(i) == true) continue;
                        var n = MakeCol(s, s, src, i, qualified: star.Qualifier != null);
                        proj.Add(n);
                        outCols.Add(OutColOf(n, null));
                    }
                }
                if (!any) throw Err.TableUnknown(star.Qualifier ?? "*");
                continue;
            }
            var expected = selectListTypes != null && proj.Count < selectListTypes.Length ? selectListTypes[proj.Count] : null;
            var node = Bind(item.Expr, s, expected);
            if (item.Alias != null && !aliasIndex.TryAdd(item.Alias, proj.Count)) aliasIndex[item.Alias] = -1; // ambiguous
            proj.Add(node);
            outCols.Add(OutColOf(node, item.Alias));
        }

        // ORDER BY
        SortRel? sortRel = null;
        if (orderBy != null && orderBy.Count > 0)
        {
            s.AggClause = "ORDER BY clause";
            var keys = new List<SortKey>();
            foreach (var o in orderBy)
            {
                XNode keyNode;
                if (IsPosition(o.Expr, out var p))
                {
                    if (p < 1 || p > proj.Count) throw Err.Dsql104(FbMessages.dsql_column_pos_err, "ORDER BY");
                    keyNode = proj[p - 1];
                }
                else if (o.Expr is ColumnRefExpr { Qualifier: null } cr && aliasIndex.TryGetValue(cr.Name, out var ai))
                {
                    if (ai < 0)
                        throw new StatusBuilder().Gds(Err.isc_dsql_error).Gds(Err.isc_sqlerr).Num(-204)
                            .Gds(FbMessages.dsql_ambiguous_field_name).Str("an alias").Str("an alias in the select list with name")
                            .Gds(Err.isc_random).Str(cr.Name).Build();
                    keyNode = proj[ai];
                }
                else keyNode = Bind(o.Expr, s, null);
                var sk = MakeSortKey(o, keyNode.Type);
                sk.Expr = keyNode;
                keys.Add(sk);
            }
            sortRel = new SortRel(rel, keys.ToArray(), s.Streams.ToArray());
            rel = sortRel;
        }

        var specQuery = new SpecQuery
        {
            Source = rel, Projection = proj.ToArray(), Distinct = qs.Distinct, Columns = outCols.ToArray(),
        };
        if (!qs.Limit.IsEmpty)
        {
            var (first, skip) = BindLimit(qs.Limit, s);
            specQuery.First = first;
            specQuery.Skip = skip;
        }
        s.AggMode = false;
        if (sortRel != null)
            sortRel.SortFields = SortFieldsOf(refs, aggRel != null ? [aggRel.OutStream] : saveStreams, sortRel.Keys.Select(k => k.Expr));
        if (aggRel != null) aggRel.SortFields = SortFieldsOf(refs, saveStreams, aggRel.Keys);
        else if (qs.Distinct) specQuery.SortFields = SortFieldsOf(refs, saveStreams, proj);
        specQuery.Ordered = sortRel != null;
        if (queryLimit != null && !queryLimit.IsEmpty)
        {
            var (first, skip) = BindLimit(queryLimit, s);
            return new LimitQuery { Input = specQuery, First = first, Skip = skip, Columns = specQuery.Columns };
        }
        return specQuery;
    }

    static OutCol OutColOf(XNode n, string? alias) => new()
    {
        Name = alias ?? (n.Name is { Length: > 0 } ? n.Name : n.Field ?? ""),
        Field = n.Field ?? n.Name, // the describe's sqlname: the column or the expression's name, even when aliased
        Relation = n.Relation ?? "",
        // a bare NULL is described as CHAR(1) like Firebird (clients cannot handle SQL_NULL columns)
        Type = n.Type.Kind == TypeKind.Null ? SqlType.Char(1, Charset.None) : n.Type,
        NullLiteral = n.Type.Kind == TypeKind.Null,
        Nullable = !n.NotNull,
        Derived = alias == null && n.Field is not { Length: > 0 },
    };

    WinSpec MakeWinSpec(WindowExpr w, Scope s)
    {
        if (w.Window.PartitionBy == null) throw Err.Unsupported("named windows");
        var f = w.Func;
        var spec = new WinSpec
        {
            Partition = w.Window.PartitionBy.Select(p => Bind(p, s)).ToArray(),
            Order = w.Window.OrderBy.Select(o =>
            {
                var n = Bind(o.Expr, s);
                var k = MakeSortKey(o, n.Type);
                k.Expr = n;
                return k;
            }).ToArray(),
            Frame = w.Window.Frame,
        };
        if (spec.Frame != null)
        {
            if (spec.Frame.Start.Offset != null) spec.FrameStartOffset = Bind(spec.Frame.Start.Offset, s, SqlType.BigInt);
            if (spec.Frame.End.Offset != null) spec.FrameEndOffset = Bind(spec.Frame.End.Offset, s, SqlType.BigInt);
        }
        switch (f.Name)
        {
            case "ROW_NUMBER": spec.Kind = WinKind.RowNumber; spec.Type = SqlType.BigInt; break;
            case "RANK": spec.Kind = WinKind.Rank; spec.Type = SqlType.BigInt; break;
            case "DENSE_RANK": spec.Kind = WinKind.DenseRank; spec.Type = SqlType.BigInt; break;
            case "PERCENT_RANK": spec.Kind = WinKind.PercentRank; spec.Type = SqlType.Double; break;
            case "CUME_DIST": spec.Kind = WinKind.CumeDist; spec.Type = SqlType.Double; break;
            case "NTILE":
                spec.Kind = WinKind.NTile;
                spec.Type = SqlType.BigInt;
                spec.Args = [Bind(f.Args[0], s, SqlType.BigInt)];
                break;
            case "LAG":
            case "LEAD":
            {
                spec.Kind = f.Name == "LAG" ? WinKind.Lag : WinKind.Lead;
                var a0 = Bind(f.Args[0], s);
                var args = new List<XNode> { a0 };
                if (f.Args.Count > 1) args.Add(Bind(f.Args[1], s, SqlType.BigInt));
                if (f.Args.Count > 2) args.Add(Bind(f.Args[2], s, a0.Type));
                spec.Args = args.ToArray();
                spec.Type = f.Args.Count > 2 ? Builtins.CommonType([a0.Type, args[2].Type]) : a0.Type;
                break;
            }
            case "FIRST_VALUE":
            case "LAST_VALUE":
                spec.Kind = f.Name == "FIRST_VALUE" ? WinKind.FirstValue : WinKind.LastValue;
                spec.Args = [Bind(f.Args[0], s)];
                spec.Type = spec.Args[0].Type;
                break;
            case "NTH_VALUE":
                spec.Kind = WinKind.NthValue;
                spec.Args = [Bind(f.Args[0], s), Bind(f.Args[1], s, SqlType.BigInt)];
                spec.Type = spec.Args[0].Type;
                break;
            default:
                if (!IsAggregate(f)) throw Err.Dsql(-104, f.Name + " is not a window function");
                spec.Kind = WinKind.Aggregate;
                var saveMode = s.AggMode;
                var saveAgg = s.Agg;
                // window aggregate arguments are evaluated per row (after grouping when grouped)
                spec.Agg = MakeAggSpecForWindow(f, s);
                spec.Type = spec.Agg.Type;
                break;
        }
        return spec;
    }

    AggSpec MakeAggSpecForWindow(FuncExpr f, Scope s)
    {
        var saveMode = s.AggMode;
        try
        {
            return MakeAggSpec(f, s);
        }
        finally
        {
            s.AggMode = saveMode;
        }
    }

    // ======================================================================== FROM clause

    abstract class JT { }

    sealed class JLeaf : JT
    {
        public SourceItem Item = null!;
        public Table? Table;
        public RelNode? Fixed;
    }

    sealed class JJoin : JT
    {
        public JoinKind Kind;
        public JT L = null!, R = null!;
        public JoinRef Ast = null!;
        public List<Conj> On = new();
    }

    sealed class Conj
    {
        public XNode Node = null!;
        public HashSet<int> Streams = null!;
        public bool Used;
    }

    static IEnumerable<Expr> SplitAnd(Expr e)
    {
        if (e is BinaryExpr { Op: BinaryOp.And } b)
        {
            foreach (var x in SplitAnd(b.Left)) yield return x;
            foreach (var x in SplitAnd(b.Right)) yield return x;
        }
        else yield return e;
    }

    Conj MakeConj(Expr e, Scope s)
    {
        var node = Track(() => BindBool(e, s), out var streams);
        return new Conj { Node = node, Streams = streams };
    }

    JT BuildFrom(List<TableRef> from, Scope s)
    {
        JT? acc = null;
        foreach (var t in from)
        {
            var j = BuildRef(t, s);
            acc = acc == null ? j : new JJoin { Kind = JoinKind.Cross, L = acc, R = j, Ast = new JoinRef() };
        }
        return acc!;
    }

    JT BuildRef(TableRef t, Scope s)
    {
        switch (t)
        {
            case NamedTableRef n:
                return BuildNamed(n, s);
            case DerivedTableRef d:
            {
                var boundary = new Scope(d.Lateral ? s : s.Parent);
                var q = CompileSelect(d.Query, boundary);
                var cols = ColsFor(d.Columns, q.Columns, d.Alias ?? "", viaUnion: IsUnion(q));
                var stream = NewStream(cols.Count);
                var rel = new DerivedRel(q, stream, []) { Cacheable = !boundary.Correlated };
                var item = new SourceItem { Alias = d.Alias, Name = d.Alias ?? "", Stream = stream, Cols = cols };
                AddSource(s, item);
                if (boundary.Correlated && boundary.OuterStreams != null && s.Parent != null)
                    foreach (var st in boundary.OuterStreams)
                        if (!s.Streams.Contains(st)) { s.Correlated = true; (s.OuterStreams ??= new()).Add(st); }
                return new JLeaf { Item = item, Fixed = rel };
            }
            case JoinRef j:
            {
                var l = BuildRef(j.Left, s);
                var r = BuildRef(j.Right, s);
                if (j.Kind is JoinKind.Right or JoinKind.Full)
                {
                    foreach (var i in ItemsOf(l)) i.OuterNullable = true;
                    // Firebird turns them around (r LEFT JOIN l): the right side's contexts come first
                    var li = ItemsOf(l);
                    var ri = ItemsOf(r);
                    var orders = li.Concat(ri).Select(i => i.CtxOrder).Order().ToList();
                    var k = 0;
                    foreach (var i in ri.Concat(li)) i.CtxOrder = orders[k++];
                }
                if (j.Kind is JoinKind.Left or JoinKind.Full) foreach (var i in ItemsOf(r)) i.OuterNullable = true;
                return new JJoin { Kind = j.Kind, L = l, R = r, Ast = j };
            }
        }
        throw Err.Unsupported(t.GetType().Name);
    }

    JT BuildNamed(NamedTableRef n, Scope s)
    {
        if (n.Args == null)
        {
            var cte = FindCte(n.Name);
            if (cte != null)
            {
                var (rel, cols) = CompileCteRef(cte);
                var stream = rel is DerivedRel dr ? dr.Stream : ((FuncRel)rel).Stream;
                var item = new SourceItem { Alias = n.Alias, Name = n.Name, Stream = stream, Cols = cols };
                AddSource(s, item);
                return new JLeaf { Item = item, Fixed = rel };
            }
            if (Db.Tables.TryGetValue(n.Name, out var table))
            {
                var stream = NewStream(table.Columns.Count);
                var item = new SourceItem
                {
                    Alias = n.Alias, Name = n.Name, Stream = stream, Table = table, Context = "table",
                    Cols = table.Columns.Select(c => new ColInfo { Name = c.Name, Type = c.Type, NotNull = c.NotNull, Relation = table.Name, Field = c.Name }).ToList(),
                };
                AddSource(s, item);
                return new JLeaf { Item = item, Table = table };
            }
            if (Db.Views.TryGetValue(n.Name, out var view))
            {
                var savedCtes = _ctes.ToList();
                _ctes.Clear();
                QueryNode q;
                try { q = CompileSelect(view.Query, null); }
                finally { _ctes.AddRange(savedCtes); }
                var cols = ColsFor(view.ColumnNames, q.Columns, view.Name, view: true);
                for (var i = 0; i < cols.Count; i++)
                {
                    cols[i].Relation = view.Name;
                    cols[i].Field = cols[i].Name;
                }
                var stream = NewStream(cols.Count);
                var rel = new DerivedRel(q, stream, []) { Cacheable = true };
                var item = new SourceItem { Alias = n.Alias, Name = n.Name, Stream = stream, Cols = cols, Context = "view" };
                AddSource(s, item);
                return new JLeaf { Item = item, Fixed = rel };
            }
        }
        if (Db.Procedures.TryGetValue(n.Name, out var proc))
        {
            var args = BindProcArgs(proc.Inputs, n.Args ?? new List<Expr>(), s, proc.Name);
            var stream = NewStream(proc.Outputs.Count);
            var p = proc;
            var rel = new FuncRel(c => Routines.CallSelectable(c, p, args), stream);
            var item = new SourceItem
            {
                Alias = n.Alias, Name = n.Name, Stream = stream, Context = "procedure",
                Cols = proc.Outputs.Select(o => new ColInfo { Name = o.Name, Type = o.Type, Relation = proc.Name, Field = o.Name }).ToList(),
            };
            AddSource(s, item);
            return new JLeaf { Item = item, Fixed = rel };
        }
        if (Routine?.SubRoutines != null && Routine.SubRoutines.TryGetValue(n.Name, out var sub) && !sub.IsFunction)
            return BuildSubProcRef(sub, n, s);
        throw Err.TableUnknown(n.Name, n.Line, n.Col);
    }

    /// <summary>Adds a FROM source; two sources of one query cannot share an alias.</summary>
    static void AddSource(Scope s, SourceItem item)
    {
        if (item.Alias != null && s.Items.Any(i => !i.QualifiedOnly && i.Alias == item.Alias))
            throw new StatusBuilder().Gds(Err.isc_dsql_error).Gds(Err.isc_sqlerr).Num(-204)
                .Gds(FbMessages.alias_conflict_err).Str(item.Alias).Build();
        s.Add(item);
    }

    internal XNode[] BindProcArgs(List<RoutineParam> inputs, List<Expr> args, Scope s, string name, bool function = false)
    {
        FbError Mismatch() => new StatusBuilder().Gds(Err.isc_dsql_error)
            .Gds(function ? FbMessages.fun_param_mismatch : FbMessages.prcmismat).Str(name).Build();
        if (args.Count > inputs.Count) throw Mismatch();
        var result = new XNode[inputs.Count];
        for (var i = 0; i < inputs.Count; i++)
        {
            if (i < args.Count) result[i] = Bind(args[i], s, inputs[i].Type);
            else if (inputs[i].Default != null) result[i] = Bind(inputs[i].Default!, new Scope(null), inputs[i].Type);
            else throw Mismatch();
        }
        return result;
    }

    void BindJoinConditions(JT t, Scope s)
    {
        if (t is not JJoin j) return;
        BindJoinConditions(j.L, s);
        BindJoinConditions(j.R, s);
        var ast = j.Ast;
        if (ast.On != null)
        {
            if (ContainsAggregate(ast.On)) throw Err.Dsql(-104, "Aggregates are not allowed in ON clause");
            var saved = s.Visible;
            s.Visible = ItemsOf(j).ToHashSet();
            try { foreach (var c in SplitAnd(ast.On)) j.On.Add(MakeConj(c, s)); }
            finally { s.Visible = saved; }
        }
        else if (ast.Using != null || ast.Natural)
        {
            var leftItems = ItemsOf(j.L);
            var rightItems = ItemsOf(j.R);
            List<string> names;
            if (ast.Using != null) names = ast.Using;
            else
            {
                names = new List<string>();
                foreach (var li in leftItems)
                    foreach (var c in li.Cols)
                        if (rightItems.Any(r => r.Find(c.Name) >= 0) && !names.Contains(c.Name)) names.Add(c.Name);
            }
            for (var n = 0; n < names.Count; n++)
            {
                var name = names[n];
                if (names.IndexOf(name) != n)
                    throw Err.Dsql104(FbMessages.dsql_col_more_than_once_using, name);
                var (line, col) = ast.UsingPos?[n] ?? (0, 0);
                var li = leftItems.FirstOrDefault(i => i.Find(name) >= 0) ?? throw Err.ColumnUnknown("<left side of USING>." + name, line, col);
                var ri = rightItems.FirstOrDefault(i => i.Find(name) >= 0) ?? throw Err.ColumnUnknown("<right side of USING>." + name, line, col);
                var lc = li.Find(name);
                var rc = ri.Find(name);
                var ln = MakeColRaw(li, lc);
                var rn = MakeColRaw(ri, rc);
                j.On.Add(new Conj
                {
                    Node = new CmpNode(CmpOp.Eq, ln, rn, MergeCollation(ln.Type, rn.Type)),
                    Streams = [li.Stream, ri.Stream],
                });
                (ri.HiddenCols ??= new()).Add(rc);
                // an unqualified reference is COALESCE(left, right), named after the column
                Func<Scope, Scope, XNode>? previous = null;
                li.Merged?.TryGetValue(lc, out previous);
                var columnName = name;
                (li.Merged ??= new())[lc] = (from, owner) =>
                {
                    var x = previous != null ? previous(from, owner) : MakeCol(from, owner, li, lc, qualified: true);
                    var y = MakeCol(from, owner, ri, rc, qualified: true);
                    var (type, nullable) = TypeRules.FromList([x, y], "COALESCE");
                    return new CoalesceNode([x, y]) { Type = type, Name = columnName, Field = "COALESCE", Relation = "", NotNull = !nullable };
                };
            }
        }
    }

    ColNode MakeColRaw(SourceItem item, int idx)
    {
        var ci = item.Cols[idx];
        return new ColNode(item.Stream, idx, ci.Type) { Name = ci.Name, Field = ci.Field, Relation = ci.Relation };
    }

    static List<SourceItem> ItemsOf(JT t) => t switch
    {
        JLeaf l => [l.Item],
        JJoin j => [.. ItemsOf(j.L), .. ItemsOf(j.R)],
        _ => [],
    };

    static IEnumerable<int> StreamsOfTree(JT t) => ItemsOf(t).Select(i => i.Stream);

    RelNode PlanTree(JT tree, List<Conj> pool, Scope s)
    {
        var bound = new HashSet<int>();
        return Plan(tree, pool, bound, s);
    }

    bool Ready(Conj c, HashSet<int> bound, Scope s, int extra = -1)
    {
        foreach (var st in c.Streams)
            if (!bound.Contains(st) && st != extra && s.Streams.Contains(st)) return false;
        return true;
    }

    RelNode Plan(JT t, List<Conj> pool, HashSet<int> bound, Scope s)
    {
        var operands = new List<JT>();
        Flatten(t, operands, pool);
        RelNode? acc = null;
        var accStreams = new HashSet<int>(bound);
        foreach (var op in operands)
        {
            if (op is JLeaf leaf)
            {
                acc = PlanLeaf(leaf, pool, accStreams, acc, s);
            }
            else
            {
                var j = (JJoin)op;
                var rel = PlanOuter(j, pool, accStreams, s);
                acc = acc == null ? rel : new NestedLoopJoinRel(acc, rel, false, null, []);
            }
            accStreams.UnionWith(acc.Streams);
            acc = ApplyReady(acc, pool, accStreams, s);
        }
        return acc!;
    }

    void Flatten(JT t, List<JT> operands, List<Conj> pool)
    {
        if (t is JJoin { Kind: JoinKind.Inner or JoinKind.Cross } j)
        {
            Flatten(j.L, operands, pool);
            Flatten(j.R, operands, pool);
            pool.AddRange(j.On);
            j.On.Clear();
        }
        else operands.Add(t);
    }

    RelNode ApplyReady(RelNode acc, List<Conj> pool, HashSet<int> bound, Scope s)
    {
        foreach (var c in pool)
        {
            if (c.Used || !Ready(c, bound, s)) continue;
            c.Used = true;
            acc = new FilterRel(acc, c.Node);
        }
        return acc;
    }

    static XNode? AndAll(List<XNode> nodes)
    {
        XNode? r = null;
        foreach (var n in nodes) r = r == null ? n : new AndNode(r, n);
        return r;
    }

    RelNode PlanLeaf(JLeaf leaf, List<Conj> pool, HashSet<int> accStreams, RelNode? acc, Scope s)
    {
        var st = leaf.Item.Stream;
        var applicable = pool.Where(c => !c.Used && Ready(c, accStreams, s, st)).ToList();
        if (leaf.Fixed != null)
        {
            var filters = applicable.Where(c => c.Streams.Contains(st) || acc == null).ToList();
            foreach (var c in filters) c.Used = true;
            var f = AndAll(filters.Select(c => c.Node).ToList());
            RelNode rel = leaf.Fixed;
            if (f != null)
            {
                switch (rel)
                {
                    case DerivedRel d: d.Filter = f; break;
                    case FuncRel fr: fr.Filter = f; break;
                    default: rel = new FilterRel(rel, f); break;
                }
            }
            return acc == null ? rel : new NestedLoopJoinRel(acc, rel, false, null, []);
        }

        var table = leaf.Table!;
        // index selection on equality conjuncts whose other side is already bound
        var eq = new Dictionary<int, (Conj Conj, XNode Other)>();
        foreach (var c in applicable)
            if (c.Node is CmpNode { Op: CmpOp.Eq } cmp && EqualitySide(cmp, st, table) is { } side && !eq.ContainsKey(side.Col))
                eq[side.Col] = (c, side.Other);
        Engine.Index? best = null;
        if (table.Virtual == null)
            foreach (var ixName in table.Indexes)
            {
                var ix = Db.Indexes[ixName];
                if (!ix.Active || !ix.Columns.All(eq.ContainsKey)) continue;
                if (best == null || (ix.Unique && !best.Unique) || (ix.Unique == best.Unique && ix.Columns.Length > best.Columns.Length)) best = ix;
            }
        RelNode access;
        List<Conj> rest;
        if (best != null)
        {
            var keyNodes = best.Columns.Select(col => eq[col].Other).ToArray();
            foreach (var col in best.Columns) eq[col].Conj.Used = true;
            rest = applicable.Where(c => !c.Used).ToList();
            foreach (var c in rest) c.Used = true;
            access = new IndexLookupRel(table, st, best, keyNodes) { Filter = AndAll(rest.Select(c => c.Node).ToList()) };
            return acc == null ? access : new NestedLoopJoinRel(acc, access, false, null, []);
        }

        // hash join on equi-conditions between this leaf and already joined streams of this query
        if (acc != null)
        {
            var leftKeys = new List<XNode>();
            var rightKeys = new List<XNode>();
            var cols = new List<Collation>();
            var used = new List<Conj>();
            foreach (var c in applicable)
            {
                if (c.Node is not CmpNode { Op: CmpOp.Eq } cmp) continue;
                var ls = StreamsOf(cmp.L);
                var rs = StreamsOf(cmp.R);
                bool OnlyLeaf(HashSet<int> x) => x.Count > 0 && x.All(v => v == st || !s.Streams.Contains(v));
                bool OnlyAcc(HashSet<int> x) => x.Count > 0 && !x.Contains(st) && x.Any(v => acc.Streams.Contains(v));
                if (!SameCategory(cmp.L.Type, cmp.R.Type)) continue;
                if (OnlyLeaf(ls) && OnlyAcc(rs)) { rightKeys.Add(cmp.L); leftKeys.Add(cmp.R); }
                else if (OnlyLeaf(rs) && OnlyAcc(ls)) { rightKeys.Add(cmp.R); leftKeys.Add(cmp.L); }
                else continue;
                cols.Add(cmp.Collation);
                used.Add(c);
            }
            if (used.Count > 0)
            {
                foreach (var c in used) c.Used = true;
                var leafOnly = applicable.Where(c => !c.Used && c.Streams.All(v => v == st || !s.Streams.Contains(v))).ToList();
                foreach (var c in leafOnly) c.Used = true;
                var scan = new TableScanRel(table, st) { Filter = AndAll(leafOnly.Select(c => c.Node).ToList()) };
                var others = applicable.Where(c => !c.Used).ToList();
                foreach (var c in others) c.Used = true;
                return new HashJoinRel(acc, scan, leftKeys.ToArray(), rightKeys.ToArray(), cols.ToArray(), false,
                    AndAll(others.Select(c => c.Node).ToList()), []);
            }
        }
        foreach (var c in applicable) c.Used = true;
        access = new TableScanRel(table, st) { Filter = AndAll(applicable.Select(c => c.Node).ToList()) };
        return acc == null ? access : new NestedLoopJoinRel(acc, access, false, null, []);
    }

    static bool SameCategory(SqlType a, SqlType b)
    {
        if (a.IsString && b.IsString) return a.IsOctets == b.IsOctets;
        if (a.IsExact && b.IsExact) return true;
        if (a.IsApprox && b.IsApprox) return true;
        return a.Kind == b.Kind && a.Kind is TypeKind.Date or TypeKind.Time or TypeKind.Timestamp or TypeKind.Boolean;
    }

    /// <summary>For col = expr where col belongs to stream and expr does not, returns the column and other side.</summary>
    (int Col, XNode Other)? EqualitySide(CmpNode cmp, int stream, Table table)
    {
        (int, XNode)? Try(XNode a, XNode b)
        {
            if (a is not ColNode c || c.Stream != stream) return null;
            if (StreamsOf(b).Contains(stream)) return null;
            var colType = table.Columns[c.Index].Type;
            if (!SameCategory(colType, b.Type)) return null;
            if (colType.IsExact && b.Type.IsExact && b.Type.Scale > colType.Scale) return null;
            if (colType.IsTextual && cmp.Collation != colType.Collation) return null;
            return (c.Index, b);
        }
        return Try(cmp.L, cmp.R) ?? Try(cmp.R, cmp.L);
    }

    RelNode PlanOuter(JJoin j, List<Conj> pool, HashSet<int> accStreams, Scope s)
    {
        var kind = j.Kind;
        JT l = j.L, r = j.R;
        if (kind == JoinKind.Right)
        {
            (l, r) = (r, l);
            kind = JoinKind.Left;
        }
        if (kind == JoinKind.Full)
        {
            var lr = Plan(l, new List<Conj>(), accStreams, s);
            var rr = Plan(r, new List<Conj>(), accStreams, s);
            var cond = AndAll(j.On.Select(c => c.Node).ToList());
            return new FullJoinRel(lr, rr, cond, NullRows(lr.Streams), NullRows(rr.Streams));
        }
        var leftRel = Plan(l, pool, accStreams, s);
        var inner = new HashSet<int>(accStreams);
        inner.UnionWith(leftRel.Streams);
        var onPool = j.On;

        // hash join for single-table right side with equi ON conditions and no usable index
        if (r is JLeaf { Table: { Virtual: null } table } leaf)
        {
            var st = leaf.Item.Stream;
            var hasIndex = false;
            var eqCols = new HashSet<int>();
            foreach (var c in onPool)
                if (c.Node is CmpNode { Op: CmpOp.Eq } cmp && EqualitySide(cmp, st, table) is { } side) eqCols.Add(side.Col);
            foreach (var ixName in table.Indexes)
            {
                var ix = Db.Indexes[ixName];
                if (ix.Active && ix.Columns.All(eqCols.Contains)) hasIndex = true;
            }
            if (!hasIndex)
            {
                var leftKeys = new List<XNode>();
                var rightKeys = new List<XNode>();
                var cols = new List<Collation>();
                var used = new List<Conj>();
                foreach (var c in onPool)
                {
                    if (c.Node is not CmpNode { Op: CmpOp.Eq } cmp || !SameCategory(cmp.L.Type, cmp.R.Type)) continue;
                    var ls = StreamsOf(cmp.L);
                    var rs = StreamsOf(cmp.R);
                    bool OnlyLeaf(HashSet<int> x) => x.Count > 0 && x.All(v => v == st || !s.Streams.Contains(v));
                    bool OnlyLeft(HashSet<int> x) => x.Count > 0 && !x.Contains(st) && x.Any(v => leftRel.Streams.Contains(v));
                    if (OnlyLeaf(ls) && OnlyLeft(rs)) { rightKeys.Add(cmp.L); leftKeys.Add(cmp.R); }
                    else if (OnlyLeaf(rs) && OnlyLeft(ls)) { rightKeys.Add(cmp.R); leftKeys.Add(cmp.L); }
                    else continue;
                    cols.Add(cmp.Collation);
                    used.Add(c);
                }
                if (used.Count > 0)
                {
                    foreach (var c in used) c.Used = true;
                    var leafOnly = onPool.Where(c => !c.Used && c.Streams.All(v => v == st || !s.Streams.Contains(v))).ToList();
                    foreach (var c in leafOnly) c.Used = true;
                    var scan = new TableScanRel(table, st) { Filter = AndAll(leafOnly.Select(c => c.Node).ToList()) };
                    var rest = onPool.Where(c => !c.Used).ToList();
                    foreach (var c in rest) c.Used = true;
                    return new HashJoinRel(leftRel, scan, leftKeys.ToArray(), rightKeys.ToArray(), cols.ToArray(), true,
                        AndAll(rest.Select(c => c.Node).ToList()), NullRows([st]));
                }
            }
        }

        var rightRel = Plan(r, onPool, inner, s);
        var remaining = onPool.Where(c => !c.Used).ToList();
        foreach (var c in remaining) c.Used = true;
        return new NestedLoopJoinRel(leftRel, rightRel, true, AndAll(remaining.Select(c => c.Node).ToList()), NullRows(rightRel.Streams));
    }
}
