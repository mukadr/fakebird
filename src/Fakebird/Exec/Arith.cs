using Fakebird.Types;

namespace Fakebird.Exec;

/// <summary>Dialect 3 arithmetic on runtime values.</summary>
public static class Arith
{
    static Value Num(in Value v) => v.Tag == VTag.String ? Conv.ParseNumber(v.AsString) : v;

    static long Checked(Int128 v)
    {
        if (v > long.MaxValue || v < long.MinValue) throw Err.NumericOverflow();
        return (long)v;
    }

    public static Value Add(in Value a, in Value b)
    {
        if (a.IsNull || b.IsNull) return Value.Null;
        if (a.Tag == VTag.Int && b.Tag == VTag.Int)
        {
            if (a.Scale == b.Scale)
            {
                var r = a.L + b.L;
                if (((a.L ^ r) & (b.L ^ r)) < 0) throw Err.NumericOverflow();
                return Value.Int(r, a.Scale);
            }
            var s = Math.Max(a.Scale, b.Scale);
            return Value.Int(Checked((Int128)Conv.Rescale(a.L, a.Scale, s) + Conv.Rescale(b.L, b.Scale, s)), s);
        }
        switch (a.Tag)
        {
            case VTag.Date: return Value.Date(a.L + DaysOf(b));
            case VTag.Timestamp: return Value.Timestamp(a.L + TicksOf(b));
            case VTag.Time: return Value.Time(WrapTime(a.L + SecondsTicks(b)));
        }
        switch (b.Tag)
        {
            case VTag.Date: return Value.Date(b.L + DaysOf(a));
            case VTag.Timestamp: return Value.Timestamp(b.L + TicksOf(a));
            case VTag.Time: return Value.Time(WrapTime(b.L + SecondsTicks(a)));
        }
        var x = Num(a);
        var y = Num(b);
        if (x.Tag == VTag.Int && y.Tag == VTag.Int) return Add(x, y);
        CheckNumeric(x);
        CheckNumeric(y);
        return Value.Dbl(Conv.ToDouble(x) + Conv.ToDouble(y));
    }

    static void CheckNumeric(in Value v)
    {
        if (v.Tag is not (VTag.Int or VTag.Double)) throw Err.Conversion(Conv.ToStr(v));
    }

    static long DaysOf(in Value v)
    {
        var n = Num(v);
        CheckNumeric(n);
        return n.Tag == VTag.Int ? (n.Scale == 0 ? n.L : (long)Math.Truncate(Conv.ToDouble(n))) : (long)Math.Truncate(n.AsDouble);
    }

    static long TicksOf(in Value v)
    {
        var n = Num(v);
        CheckNumeric(n);
        if (n.Tag == VTag.Int && n.Scale == 0) return n.L * Value.TicksPerDay;
        return (long)Math.Round(Conv.ToDouble(n) * Value.TicksPerDay);
    }

    static long SecondsTicks(in Value v)
    {
        var n = Num(v);
        CheckNumeric(n);
        return (long)Math.Round(Conv.ToDouble(n) * Value.TicksPerSecond);
    }

    static long WrapTime(long t)
    {
        t %= Value.TicksPerDay;
        return t < 0 ? t + Value.TicksPerDay : t;
    }

    public static Value Neg(in Value a)
    {
        if (a.IsNull) return a;
        var x = Num(a);
        if (x.Tag == VTag.Int)
        {
            if (x.L == long.MinValue) throw Err.NumericOverflow();
            return Value.Int(-x.L, x.Scale);
        }
        CheckNumeric(x);
        return Value.Dbl(-x.AsDouble);
    }

    public static Value Sub(in Value a, in Value b)
    {
        if (a.IsNull || b.IsNull) return Value.Null;
        if (a.Tag == VTag.Int && b.Tag == VTag.Int)
        {
            if (a.Scale == b.Scale)
            {
                var r = a.L - b.L;
                if (((a.L ^ b.L) & (a.L ^ r)) < 0) throw Err.NumericOverflow();
                return Value.Int(r, a.Scale);
            }
            var s = Math.Max(a.Scale, b.Scale);
            return Value.Int(Checked((Int128)Conv.Rescale(a.L, a.Scale, s) - Conv.Rescale(b.L, b.Scale, s)), s);
        }
        switch (a.Tag)
        {
            case VTag.Date when b.Tag == VTag.Date:
                return Value.Int(a.L - b.L);
            case VTag.Date when b.Tag == VTag.Timestamp:
                throw Err.ExpressionEval("Invalid date/timestamp subtraction");
            case VTag.Timestamp when b.Tag == VTag.Timestamp:
                return TimestampDiff(a.L, b.L);
            case VTag.Timestamp when b.Tag == VTag.Date:
                throw Err.ExpressionEval("Invalid date/timestamp subtraction");
            case VTag.Time when b.Tag == VTag.Time:
                return Value.Int(a.L - b.L, 4);
            case VTag.Date: return Value.Date(a.L - DaysOf(b));
            case VTag.Timestamp: return Value.Timestamp(a.L - TicksOf(b));
            case VTag.Time: return Value.Time(WrapTime(a.L - SecondsTicks(b)));
        }
        var x = Num(a);
        var y = Num(b);
        if (x.Tag == VTag.Int && y.Tag == VTag.Int) return Sub(x, y);
        CheckNumeric(x);
        CheckNumeric(y);
        return Value.Dbl(Conv.ToDouble(x) - Conv.ToDouble(y));
    }

    /// <summary>Difference in days as NUMERIC(18,9).</summary>
    static Value TimestampDiff(long a, long b)
    {
        Int128 ticks = (Int128)a - b;
        // days * 1e9 = ticks * 1e9 / 864e6 = ticks * 1000 / 864
        var num = ticks * 1000;
        var q = num / 864;
        var r = num % 864;
        if (Int128.Abs(r) * 2 >= 864) q += num < 0 ? -1 : 1;
        return Value.Int(Checked(q), 9);
    }

    public static Value Mul(in Value a, in Value b)
    {
        if (a.IsNull || b.IsNull) return Value.Null;
        var x = Num(a);
        var y = Num(b);
        if (x.Tag == VTag.Int && y.Tag == VTag.Int)
        {
            var s = x.Scale + y.Scale;
            Int128 r = (Int128)x.L * y.L;
            if (s > 18)
            {
                var d = (Int128)Value.Pow10[s - 18];
                var q = r / d;
                if (Int128.Abs(r % d) * 2 >= d) q += r < 0 ? -1 : 1;
                r = q;
                s = 18;
            }
            return Value.Int(Checked(r), s);
        }
        CheckNumeric(x);
        CheckNumeric(y);
        return Value.Dbl(Conv.ToDouble(x) * Conv.ToDouble(y));
    }

    public static Value Div(in Value a, in Value b)
    {
        if (a.IsNull || b.IsNull) return Value.Null;
        var x = Num(a);
        var y = Num(b);
        if (x.Tag == VTag.Int && y.Tag == VTag.Int)
        {
            if (y.L == 0) throw Err.DivideByZero();
            if (x.Scale == 0 && y.Scale == 0)
            {
                if (x.L == long.MinValue && y.L == -1) throw Err.NumericOverflow();
                return Value.Int(x.L / y.L);
            }
            var s = x.Scale + y.Scale;
            var factorExp = 2 * y.Scale;
            Int128 num = x.L;
            if (factorExp > 0)
            {
                if (factorExp > 36) return Value.Dbl(Conv.ToDouble(x) / Conv.ToDouble(y));
                var f = Int128.One;
                for (var i = 0; i < factorExp; i++) f *= 10;
                // detect overflow of num * f
                if (Int128.Abs(num) > Int128.MaxValue / f) return Value.Dbl(Conv.ToDouble(x) / Conv.ToDouble(y));
                num *= f;
            }
            var q = num / y.L;
            if (s > 18)
            {
                var d = (Int128)Value.Pow10[s - 18];
                q /= d;
                s = 18;
            }
            return Value.Int(Checked(q), s);
        }
        CheckNumeric(x);
        CheckNumeric(y);
        var dy = Conv.ToDouble(y);
        if (dy == 0) throw Err.DivideByZero();
        return Value.Dbl(Conv.ToDouble(x) / dy);
    }

    public static Value Concat(in Value a, in Value b)
    {
        if (a.IsNull || b.IsNull) return Value.Null;
        if (a.Tag == VTag.Bytes && b.Tag == VTag.Bytes)
        {
            var x = a.AsBytes;
            var y = b.AsBytes;
            var r = new byte[x.Length + y.Length];
            x.CopyTo(r, 0);
            y.CopyTo(r, x.Length);
            return Value.Bytes(r);
        }
        return Value.Str(string.Concat(Conv.ToStr(a), Conv.ToStr(b)));
    }
}
