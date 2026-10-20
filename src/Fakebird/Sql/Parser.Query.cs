namespace Fakebird.Sql;

public sealed partial class Parser
{
    // ======================================================================== queries

    /// <summary>Full query: [WITH ...] body [ORDER BY] [ROWS] [OFFSET/FETCH] [FOR UPDATE] [WITH LOCK].</summary>
    public SelectQuery SelectQueryExpr()
    {
        var q = new SelectQuery { Pos = Cur.Pos };
        if (Accept("WITH"))
        {
            q.Recursive = Accept("RECURSIVE");
            q.With = new List<CteDef>();
            do
            {
                var cte = new CteDef { Name = Ident() };
                if (IsOp("(")) cte.Columns = ParenIdentList();
                Expect("AS");
                ExpectOp("(");
                cte.Query = SelectQueryExpr();
                ExpectOp(")");
                q.With.Add(cte);
            } while (AcceptOp(","));
        }
        q.Body = QueryExpression();
        QueryTail(q);
        return q;
    }

    void QueryTail(SelectQuery q)
    {
        if (Accept("ORDER"))
        {
            Expect("BY");
            q.OrderBy = OrderList();
        }
        if (Accept("ROWS"))
        {
            q.Limit.RowsFrom = Additive();
            if (Accept("TO")) q.Limit.RowsTo = Additive();
        }
        if (Accept("OFFSET"))
        {
            q.Limit.Skip = Additive();
            if (!Accept("ROWS")) Expect("ROW");
        }
        if (Accept("FETCH"))
        {
            if (!Accept("FIRST")) Expect("NEXT");
            q.Limit.First = Is("ROW") || Is("ROWS") ? new LiteralExpr(Types.Value.Int(1), Types.SqlType.Integer) : Additive();
            if (!Accept("ROWS")) Expect("ROW");
            Expect("ONLY");
        }
        if (Accept("FOR"))
        {
            Expect("UPDATE");
            q.ForUpdate = true;
            if (Accept("OF")) IdentList();
        }
        if (Accept("WITH"))
        {
            Expect("LOCK");
            q.WithLock = true;
        }
    }

    QueryExpr QueryExpression()
    {
        var left = QueryTerm();
        while (Is("UNION"))
        {
            var pos = Advance().Pos;
            var kind = SetOpKind.Union;
            if (Accept("ALL")) kind = SetOpKind.UnionAll;
            else Accept("DISTINCT");
            var right = QueryTerm();
            left = new SetOpQuery(kind, left, right) { Pos = pos };
        }
        return left;
    }

    QueryExpr QueryTerm()
    {
        if (IsOp("("))
        {
            _i++;
            var q = SelectQueryExpr();
            ExpectOp(")");
            return q;
        }
        return QuerySpecification();
    }

    QuerySpec QuerySpecification()
    {
        var qs = new QuerySpec { Pos = Cur.Pos };
        Expect("SELECT");
        if (Accept("FIRST")) qs.Limit.First = LimitValue();
        if (Accept("SKIP")) qs.Limit.Skip = LimitValue();
        if (Accept("DISTINCT")) qs.Distinct = true;
        else Accept("ALL");
        do
        {
            var item = new SelectItem();
            if (AcceptOp("*")) item.Expr = new StarExpr(null);
            else
            {
                item.Expr = Expression();
                item.Alias = OptAlias();
            }
            qs.Items.Add(item);
        } while (AcceptOp(","));
        if (Accept("INTO")) qs.Into = TargetList();
        Expect("FROM");
        qs.From.Add(TableReference());
        while (AcceptOp(",")) qs.From.Add(TableReference());
        if (Accept("WHERE")) qs.Where = Expression();
        if (Accept("GROUP"))
        {
            Expect("BY");
            qs.GroupBy.Add(Expression());
            while (AcceptOp(",")) qs.GroupBy.Add(Expression());
        }
        if (Accept("HAVING")) qs.Having = Expression();
        if (Is("WINDOW")) throw Err.Unsupported("WINDOW clause");
        if (Accept("PLAN")) SkipPlan();
        return qs;
    }

    Expr LimitValue()
    {
        if (Cur.Kind == Tok.Integer) return NumberLiteral(false);
        if (Cur.Kind == Tok.Param) return Primary();
        if (Cur.Kind == Tok.Colon) return Primary();
        ExpectOp("(");
        var e = Expression();
        ExpectOp(")");
        return e;
    }

    void SkipPlan()
    {
        while (Is("JOIN") || Is("SORT") || Is("MERGE") || Is("HASH")) _i++;
        ExpectOp("(");
        var depth = 1;
        while (depth > 0)
        {
            if (Cur.Kind == Tok.Eof) throw Unexpected();
            if (IsOp("(")) depth++;
            else if (IsOp(")")) depth--;
            _i++;
        }
    }

    internal List<OrderItem> OrderList()
    {
        var l = new List<OrderItem>();
        do
        {
            var item = new OrderItem { Expr = Expression() };
            if (Accept("DESC") || Accept("DESCENDING")) item.Descending = true;
            else if (!Accept("ASC")) Accept("ASCENDING");
            if (Accept("NULLS"))
            {
                if (Accept("FIRST")) item.NullsFirst = true;
                else
                {
                    Expect("LAST");
                    item.NullsFirst = false;
                }
            }
            l.Add(item);
        } while (AcceptOp(","));
        return l;
    }

    TableRef TableReference()
    {
        var left = TablePrimary();
        while (true)
        {
            var pos = Cur.Pos;
            var natural = Accept("NATURAL");
            JoinKind kind;
            if (Accept("CROSS"))
            {
                Expect("JOIN");
                left = new JoinRef { Kind = JoinKind.Cross, Left = left, Right = TablePrimary(), Pos = pos };
                continue;
            }
            if (Accept("JOIN")) kind = JoinKind.Inner;
            else if (Accept("INNER")) { Expect("JOIN"); kind = JoinKind.Inner; }
            else if (Accept("LEFT")) { Accept("OUTER"); Expect("JOIN"); kind = JoinKind.Left; }
            else if (Accept("RIGHT")) { Accept("OUTER"); Expect("JOIN"); kind = JoinKind.Right; }
            else if (Accept("FULL")) { Accept("OUTER"); Expect("JOIN"); kind = JoinKind.Full; }
            else
            {
                if (natural) throw Unexpected();
                return left;
            }
            var j = new JoinRef { Kind = kind, Left = left, Right = TablePrimary(), Natural = natural, Pos = pos };
            if (!natural)
            {
                if (Accept("USING")) j.Using = ParenIdentList();
                else
                {
                    Expect("ON");
                    j.On = Expression();
                }
            }
            left = j;
        }
    }

    TableRef TablePrimary()
    {
        var pos = Cur.Pos;
        var lateral = Accept("LATERAL");
        if (IsOp("("))
        {
            _i++;
            if (Is("SELECT") || Is("WITH"))
            {
                var q = SelectQueryExpr();
                ExpectOp(")");
                var d = new DerivedTableRef { Query = q, Pos = pos, Lateral = lateral };
                d.Alias = OptAlias();
                if (d.Alias != null && IsOp("(")) d.Columns = ParenIdentList();
                return d;
            }
            var inner = TableReference();
            ExpectOp(")");
            return inner;
        }
        var t = At(new NamedTableRef { Name = Ident() }, pos);
        if (IsOp("("))
        {
            _i++;
            t.Args = new List<Expr>();
            if (!IsOp(")"))
            {
                t.Args.Add(Expression());
                while (AcceptOp(",")) t.Args.Add(Expression());
            }
            ExpectOp(")");
        }
        t.Alias = OptAlias();
        return t;
    }

    /// <summary>INTO :a, :b (PSQL targets; the colon is optional for NEW.x).</summary>
    List<Expr> TargetList()
    {
        var l = new List<Expr>();
        do l.Add(Target()); while (AcceptOp(","));
        return l;
    }

    Expr Target()
    {
        var pos = Cur.Pos;
        if (Cur.Kind == Tok.Colon)
        {
            _i++;
            var name = AnyIdent();
            if (AcceptOp(".")) return At(new ColumnRefExpr(name, AnyIdent()), pos);
            return new VariableExpr(name) { Pos = pos };
        }
        var n = AnyIdent();
        if (AcceptOp(".")) return At(new ColumnRefExpr(n, AnyIdent()), pos);
        return new VariableExpr(n) { Pos = pos };
    }
}
