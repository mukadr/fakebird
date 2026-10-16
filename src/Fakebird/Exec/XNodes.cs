using System.Runtime.CompilerServices;
using Fakebird.Engine;
using Fakebird.Types;

namespace Fakebird.Exec;

/// <summary>Compiled expression.</summary>
public abstract class XNode
{
    public SqlType Type = SqlType.Null;
    /// <summary>Describe info: default column name, source relation/field.</summary>
    public string Name = "";
    public string? Relation;
    public string? Field;
    public bool NotNull;

    public abstract Value Eval(Ctx c);

    /// <summary>True when the node never depends on rows, params or state (can be folded).</summary>
    public virtual bool IsConstant => false;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool? EvalBool(Ctx c)
    {
        var v = Eval(c);
        if (v.IsNull) return null;
        if (v.Tag == VTag.Bool) return v.L != 0;
        return Conv.Cast(v, SqlType.Boolean).L != 0;
    }

    /// <summary>WHERE semantics: NULL/unknown is false.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsTrue(Ctx c)
    {
        var v = Eval(c);
        return v.Tag == VTag.Bool ? v.L != 0 : !v.IsNull && Conv.Cast(v, SqlType.Boolean).L != 0;
    }
}

public sealed class ConstNode : XNode
{
    public readonly Value Value;
    public ConstNode(Value v, SqlType t)
    {
        Value = v;
        Type = t;
        Name = "CONSTANT";
        NotNull = !v.IsNull;
    }
    public override Value Eval(Ctx c) => Value;
    public override bool IsConstant => true;
}

/// <summary>Reads column <see cref="Index"/> of the current row of stream <see cref="Stream"/>.</summary>
public sealed class ColNode : XNode
{
    public readonly int Stream;
    public readonly int Index;
    public ColNode(int stream, int index, SqlType type)
    {
        Stream = stream;
        Index = index;
        Type = type;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override Value Eval(Ctx c) => c.Rows[Stream][Index];
}

public sealed class ParamNode : XNode
{
    public readonly int Index;
    public ParamNode(int index, SqlType type)
    {
        Index = index;
        Type = type;
        Name = "";
    }
    public override Value Eval(Ctx c) => c.Params[Index];
}

public enum ArithOp { Add, Sub, Mul, Div, Concat }

public sealed class ArithNode(ArithOp op, XNode l, XNode r) : XNode
{
    public readonly ArithOp Op = op;
    public readonly XNode L = l, R = r;

    public override Value Eval(Ctx c)
    {
        var a = L.Eval(c);
        if (a.IsNull) return a;
        var b = R.Eval(c);
        return Op switch
        {
            ArithOp.Add => Arith.Add(a, b),
            ArithOp.Sub => Arith.Sub(a, b),
            ArithOp.Mul => Arith.Mul(a, b),
            ArithOp.Div => Arith.Div(a, b),
            _ => ConcatTyped(a, b),
        };
    }

    Value ConcatTyped(in Value a, in Value b)
    {
        var r = Arith.Concat(a, b);
        if (!r.IsNull && Type.IsString && r.Tag == VTag.String && Conv.CharLength(r.AsString) > 32765)
            throw Err.StringTruncation(32765, Conv.CharLength(r.AsString));
        return r;
    }

    public override bool IsConstant => L.IsConstant && R.IsConstant;
}

public sealed class NegNode(XNode x) : XNode
{
    public readonly XNode X = x;
    public override Value Eval(Ctx c) => Arith.Neg(X.Eval(c));
    public override bool IsConstant => X.IsConstant;
}

public enum CmpOp { Eq, Ne, Lt, Le, Gt, Ge }

public sealed class CmpNode : XNode
{
    public readonly CmpOp Op;
    public readonly XNode L, R;
    public readonly Collation Collation;

    public CmpNode(CmpOp op, XNode l, XNode r, Collation collation)
    {
        Op = op;
        L = l;
        R = r;
        Collation = collation;
        Type = SqlType.Boolean;
    }

    public override Value Eval(Ctx c)
    {
        var a = L.Eval(c);
        if (a.IsNull) return Value.Null;
        var b = R.Eval(c);
        if (b.IsNull) return Value.Null;
        return Value.Bool(Test(Op, Conv.Compare(a, b, Collation)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Test(CmpOp op, int cmp) => op switch
    {
        CmpOp.Eq => cmp == 0,
        CmpOp.Ne => cmp != 0,
        CmpOp.Lt => cmp < 0,
        CmpOp.Le => cmp <= 0,
        CmpOp.Gt => cmp > 0,
        _ => cmp >= 0,
    };

    public override bool IsConstant => L.IsConstant && R.IsConstant;
}

public sealed class AndNode : XNode
{
    public readonly XNode L, R;
    public AndNode(XNode l, XNode r) { L = l; R = r; Type = SqlType.Boolean; }

    public override Value Eval(Ctx c)
    {
        var a = L.EvalBool(c);
        if (a == false) return Value.False;
        var b = R.EvalBool(c);
        if (b == false) return Value.False;
        if (a == null || b == null) return Value.Null;
        return Value.True;
    }
}

public sealed class OrNode : XNode
{
    public readonly XNode L, R;
    public OrNode(XNode l, XNode r) { L = l; R = r; Type = SqlType.Boolean; }

    public override Value Eval(Ctx c)
    {
        var a = L.EvalBool(c);
        if (a == true) return Value.True;
        var b = R.EvalBool(c);
        if (b == true) return Value.True;
        if (a == null || b == null) return Value.Null;
        return Value.False;
    }
}

public sealed class NotNode : XNode
{
    public readonly XNode X;
    public NotNode(XNode x) { X = x; Type = SqlType.Boolean; }

    public override Value Eval(Ctx c)
    {
        var a = X.EvalBool(c);
        return a == null ? Value.Null : Value.Bool(!a.Value);
    }
}

public sealed class IsNullNode : XNode
{
    public readonly XNode X;
    public readonly bool Not;
    public IsNullNode(XNode x, bool not) { X = x; Not = not; Type = SqlType.Boolean; NotNull = true; }
    public override Value Eval(Ctx c) => Value.Bool(X.Eval(c).IsNull != Not);
}

public sealed class IsDistinctNode : XNode
{
    public readonly XNode L, R;
    public readonly bool Not;
    public readonly Collation Collation;
    public IsDistinctNode(XNode l, XNode r, bool not, Collation col) { L = l; R = r; Not = not; Collation = col; Type = SqlType.Boolean; NotNull = true; }

    public override Value Eval(Ctx c)
    {
        var a = L.Eval(c);
        var b = R.Eval(c);
        bool distinct;
        if (a.IsNull || b.IsNull) distinct = a.IsNull != b.IsNull;
        else distinct = Conv.Compare(a, b, Collation) != 0;
        return Value.Bool(distinct != Not);
    }
}

public sealed class IsBoolNode : XNode
{
    public readonly XNode X;
    public readonly bool? Expected;
    public readonly bool Not;
    public IsBoolNode(XNode x, bool? expected, bool not) { X = x; Expected = expected; Not = not; Type = SqlType.Boolean; NotNull = true; }

    public override Value Eval(Ctx c)
    {
        var v = X.EvalBool(c);
        return Value.Bool((v == Expected) != Not);
    }
}

public sealed class BetweenNode : XNode
{
    public readonly XNode X, Lo, Hi;
    public readonly bool Not;
    public readonly Collation Collation;
    public BetweenNode(XNode x, XNode lo, XNode hi, bool not, Collation col) { X = x; Lo = lo; Hi = hi; Not = not; Collation = col; Type = SqlType.Boolean; }

    public override Value Eval(Ctx c)
    {
        var v = X.Eval(c);
        if (v.IsNull) return Value.Null;
        var lo = Lo.Eval(c);
        var hi = Hi.Eval(c);
        bool? geLo = lo.IsNull ? null : Conv.Compare(v, lo, Collation) >= 0;
        bool? leHi = hi.IsNull ? null : Conv.Compare(v, hi, Collation) <= 0;
        bool? r = geLo == false || leHi == false ? false : geLo == null || leHi == null ? null : true;
        if (r == null) return Value.Null;
        return Value.Bool(r.Value != Not);
    }
}

public sealed class InListNode : XNode
{
    public readonly XNode X;
    public readonly XNode[] List;
    public readonly bool Not;
    public readonly Collation Collation;
    public InListNode(XNode x, XNode[] list, bool not, Collation col) { X = x; List = list; Not = not; Collation = col; Type = SqlType.Boolean; }

    public override Value Eval(Ctx c)
    {
        var v = X.Eval(c);
        if (v.IsNull) return Value.Null;
        var sawNull = false;
        foreach (var e in List)
        {
            var w = e.Eval(c);
            if (w.IsNull) { sawNull = true; continue; }
            if (Conv.Compare(v, w, Collation) == 0) return Value.Bool(!Not);
        }
        if (sawNull) return Value.Null;
        return Value.Bool(Not);
    }
}

public sealed class CaseNode : XNode
{
    /// <summary>Searched CASE: (condition, result) pairs.</summary>
    public readonly (XNode When, XNode Then)[] Whens;
    public readonly XNode? Else;
    public CaseNode((XNode, XNode)[] whens, XNode? els) { Whens = whens; Else = els; Name = "CASE"; }

    public override Value Eval(Ctx c)
    {
        foreach (var (w, t) in Whens)
            if (w.IsTrue(c)) return Coerce(t.Eval(c));
        return Else == null ? Value.Null : Coerce(Else.Eval(c));
    }

    Value Coerce(in Value v) => Coercion.ToType(v, Type);
}

/// <summary>Simple CASE / DECODE: compares operand with each value.</summary>
public sealed class SimpleCaseNode : XNode
{
    public readonly XNode Operand;
    public readonly (XNode Value, XNode Then)[] Whens;
    public readonly XNode? Else;
    public readonly Collation Collation;
    public SimpleCaseNode(XNode operand, (XNode, XNode)[] whens, XNode? els, Collation col, string name)
    {
        Operand = operand; Whens = whens; Else = els; Collation = col; Name = name;
    }

    public override Value Eval(Ctx c)
    {
        var v = Operand.Eval(c);
        if (!v.IsNull)
            foreach (var (w, t) in Whens)
            {
                var x = w.Eval(c);
                if (!x.IsNull && Conv.Compare(v, x, Collation) == 0) return Coercion.ToType(t.Eval(c), Type);
            }
        return Else == null ? Value.Null : Coercion.ToType(Else.Eval(c), Type);
    }
}

public sealed class CoalesceNode : XNode
{
    public readonly XNode[] Args;
    public CoalesceNode(XNode[] args) { Args = args; Name = "COALESCE"; }

    public override Value Eval(Ctx c)
    {
        foreach (var a in Args)
        {
            var v = a.Eval(c);
            if (!v.IsNull) return Coercion.ToType(v, Type);
        }
        return Value.Null;
    }
}

public sealed class CastNode : XNode
{
    public readonly XNode X;
    public CastNode(XNode x, SqlType t) { X = x; Type = t; Name = "CAST"; NotNull = x.NotNull; }
    public override Value Eval(Ctx c) => Conv.Cast(X.Eval(c), Type);
    public override bool IsConstant => X.IsConstant;
}

/// <summary>Built-in function implemented by a delegate receiving the unevaluated argument nodes.</summary>
public sealed class FuncNode : XNode
{
    public readonly Func<Ctx, XNode[], FuncNode, Value> Impl;
    public readonly XNode[] Args;
    public object? State;
    public readonly bool Deterministic;

    public FuncNode(string name, Func<Ctx, XNode[], FuncNode, Value> impl, XNode[] args, SqlType type, bool deterministic = true)
    {
        Name = name;
        Impl = impl;
        Args = args;
        Type = type;
        Deterministic = deterministic;
    }

    public override Value Eval(Ctx c) => Impl(c, Args, this);

    public override bool IsConstant
    {
        get
        {
            if (!Deterministic) return false;
            foreach (var a in Args) if (!a.IsConstant) return false;
            return true;
        }
    }
}

/// <summary>Evaluated once per request (e.g. CURRENT_TIMESTAMP) or per call (context variables).</summary>
public sealed class CtxFuncNode : XNode
{
    readonly Func<Ctx, Value> _impl;

    public CtxFuncNode(Func<Ctx, Value> impl, SqlType type, string name)
    {
        _impl = impl;
        Type = type;
        Name = name;
    }

    public override Value Eval(Ctx c) => _impl(c);
}

public static class Coercion
{
    /// <summary>Converts a value to the static type of an expression if the representation differs.</summary>
    public static Value ToType(in Value v, SqlType t)
    {
        if (v.IsNull) return v;
        switch (t.Kind)
        {
            case TypeKind.SmallInt:
            case TypeKind.Integer:
            case TypeKind.BigInt:
                if (v.Tag == VTag.Int && v.Scale == t.Scale) return v;
                break;
            case TypeKind.Float:
            case TypeKind.Double:
                if (v.Tag == VTag.Double) return v;
                break;
            case TypeKind.Char:
            case TypeKind.VarChar:
                if (t.Charset == Charset.Octets ? v.Tag == VTag.Bytes : v.Tag == VTag.String) return v;
                break;
            case TypeKind.Blob:
                if (t.SubType == 1 ? v.Tag == VTag.String : v.Tag == VTag.Bytes) return v;
                break;
            case TypeKind.Date: if (v.Tag == VTag.Date) return v; break;
            case TypeKind.Time: if (v.Tag == VTag.Time) return v; break;
            case TypeKind.Timestamp: if (v.Tag == VTag.Timestamp) return v; break;
            case TypeKind.Boolean: if (v.Tag == VTag.Bool) return v; break;
            case TypeKind.Null: return v;
        }
        return Conv.Cast(v, t.IsString ? t.WithLength(Math.Max(t.Length, 32765 / Math.Max(1, t.BytesPerChar))) : t);
    }
}
