using Fakebird.Types;

namespace Fakebird.Exec;

/// <summary>Base for subquery expressions. Uncorrelated subqueries are evaluated once per request.</summary>
public abstract class SubqueryNode : XNode
{
    public QueryNode Query = null!;
    public bool Uncorrelated;
    public HashSet<int>? OuterStreams;

    protected List<Value[]> Rows(Ctx c)
    {
        if (!Uncorrelated) return Query.ToList(c);
        c.Cache ??= new();
        if (c.Cache.TryGetValue(this, out var r)) return (List<Value[]>)r;
        var list = Query.ToList(c);
        c.Cache[this] = list;
        return list;
    }
}

public sealed class ScalarSubqueryNode : SubqueryNode
{
    public override Value Eval(Ctx c)
    {
        if (Uncorrelated)
        {
            var rows = Rows(c);
            if (rows.Count == 0) return Value.Null;
            if (rows.Count > 1) throw Err.MultipleRows();
            return Coercion.ToType(rows[0][0], Type);
        }
        var qc = Query.Open(c);
        if (!qc.Next()) return Value.Null;
        var v = qc.Current[0];
        if (qc.Next()) throw Err.MultipleRows();
        return Coercion.ToType(v, Type);
    }
}

public sealed class ExistsNode : SubqueryNode
{
    public bool Singular;

    public ExistsNode() => Type = SqlType.Boolean;

    public override Value Eval(Ctx c)
    {
        if (Uncorrelated)
        {
            var rows = Rows(c);
            return Value.Bool(Singular ? rows.Count == 1 : rows.Count > 0);
        }
        var qc = Query.Open(c);
        if (!qc.Next()) return Value.False;
        if (!Singular) return Value.True;
        return Value.Bool(!qc.Next());
    }
}

/// <summary>x [NOT] IN (subquery) and x op ANY/ALL (subquery).</summary>
public sealed class QuantifiedNode : SubqueryNode
{
    public XNode Left = null!;
    public CmpOp Op;
    public bool All;
    public bool Negate;
    public Collation Collation;

    public QuantifiedNode() => Type = SqlType.Boolean;

    public override Value Eval(Ctx c)
    {
        var v = Left.Eval(c);
        bool? r;
        IEnumerable<Value[]> rows = Uncorrelated ? Rows(c) : Enumerate(c);
        if (All)
        {
            r = true;
            foreach (var row in rows)
            {
                if (v.IsNull) { r = null; break; }
                var w = row[0];
                if (w.IsNull) { r = null; continue; }
                if (!CmpNode.Test(Op, Conv.Compare(v, w, Collation))) { r = false; break; }
            }
        }
        else
        {
            r = false;
            foreach (var row in rows)
            {
                if (v.IsNull) { r = null; break; }
                var w = row[0];
                if (w.IsNull) { r = null; continue; }
                if (CmpNode.Test(Op, Conv.Compare(v, w, Collation))) { r = true; break; }
            }
        }
        // Firebird's ANY/ALL streams never leave the result UNKNOWN: they answer "is TRUE", or "is FALSE" when negated
        if (r == null) return Value.Bool(false);
        return Value.Bool(r.Value != Negate);
    }

    IEnumerable<Value[]> Enumerate(Ctx c)
    {
        var qc = Query.Open(c);
        while (qc.Next()) yield return qc.Current;
    }
}
