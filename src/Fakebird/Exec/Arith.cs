using Fakebird.Types;

namespace Fakebird.Exec;

/// <summary>Dialect 3 arithmetic on runtime values.</summary>
public static class Arith
{
    const long MaxInt64Limit = long.MaxValue / 10;
    const long MinInt64Limit = long.MinValue / 10;

    /// <summary>
    /// Evaluates an operator whose result type was decided at compile time, like Firebird's ArithmeticNode:
    /// BIGINT (int64 arithmetic at the result's scale), INT128, DOUBLE; date/time operands use their own rules.
    /// </summary>
    public static Value Eval(ArithOp op, SqlType type, in Value a, in Value b)
    {
        if (a.IsNull || b.IsNull) return Value.Null;
        switch (type.Kind)
        {
            case TypeKind.BigInt when a.Tag is not (VTag.Date or VTag.Time or VTag.Timestamp) && b.Tag is not (VTag.Date or VTag.Time or VTag.Timestamp):
                return op switch
                {
                    ArithOp.Add => AddI64(a, b, type.Scale, false),
                    ArithOp.Sub => AddI64(a, b, type.Scale, true),
                    ArithOp.Mul => MulI64(a, b, type.Scale),
                    _ => DivI64(a, b, type.Scale),
                };
            case TypeKind.Int128:
                return op switch
                {
                    ArithOp.Add => AddI128(a, b, type.Scale, false),
                    ArithOp.Sub => AddI128(a, b, type.Scale, true),
                    ArithOp.Mul => MulI128(a, b, type.Scale),
                    _ => DivI128(a, b, type.Scale),
                };
            case TypeKind.Double:
            {
                var d1 = Conv.ToDouble(Num(a));
                var d2 = Conv.ToDouble(Num(b));
                double r;
                switch (op)
                {
                    case ArithOp.Add: r = d1 + d2; break;
                    case ArithOp.Sub: r = d1 - d2; break;
                    case ArithOp.Mul: r = d1 * d2; break;
                    default:
                        if (d2 == 0) throw Err.FloatDivideByZero();
                        r = d1 / d2;
                        break;
                }
                if (double.IsInfinity(r)) throw Err.FloatOverflow();
                return Value.Dbl(r);
            }
        }
        return op switch
        {
            ArithOp.Add => Add(a, b),
            ArithOp.Sub => Sub(a, b),
            ArithOp.Mul => Mul(a, b),
            _ => Div(a, b),
        };
    }

    /// <summary>MOV_get_int64: the operand as a long at the given scale.</summary>
    static long I64(in Value v, int scale) => v.Tag == VTag.Int && v.Scale == scale ? v.L : Conv.CastExact(Num(v), scale, TypeKind.BigInt).L;

    static Value AddI64(in Value a, in Value b, int scale, bool sub)
    {
        var i1 = I64(a, scale);
        var i2 = I64(b, scale);
        var r = sub ? i1 - i2 : i1 + i2;
        var overflow = sub ? ((i1 ^ i2) & (i1 ^ r)) < 0 : ((i1 ^ r) & (i2 ^ r)) < 0;
        if (overflow) throw Err.IntegerOverflow();
        return Value.Int(r, scale);
    }

    static Value MulI64(in Value a, in Value b, int scale)
    {
        // each factor at its own scale; the result scale is their sum
        var x = Num(a);
        var y = Num(b);
        var s2 = y.Tag == VTag.Int ? y.Scale : scale - (x.Tag == VTag.Int ? x.Scale : 0);
        var i1 = I64(x, scale - s2);
        var i2 = I64(y, s2);
        var u1 = i1 >= 0 ? (ulong)i1 : (ulong)-(Int128)i1;
        var u2 = i2 >= 0 ? (ulong)i2 : (ulong)-(Int128)i2;
        var limit = (i1 ^ i2) >= 0 ? (ulong)long.MaxValue : (ulong)long.MaxValue + 1;
        if (u1 != 0 && limit / u1 < u2) throw Err.IntegerOverflow();
        return Value.Int(i1 * i2, scale);
    }

    static Value DivI64(in Value a, in Value b, int scale)
    {
        var x = Num(a);
        var y = Num(b);
        var s2 = y.Tag == VTag.Int ? y.Scale : 0;
        var i2 = I64(y, s2);
        if (i2 == 0) throw Err.DivideByZero();
        var i1 = I64(x, scale - s2);
        // scale the dividend by 10^(2*s2) as far as it fits, then take zeros off the divisor
        var addl = 2 * s2;
        if (i1 >= 0)
            while (addl > 0 && i1 <= MaxInt64Limit) { i1 *= 10; addl--; }
        else
            while (addl > 0 && i1 >= MinInt64Limit) { i1 *= 10; addl--; }
        while (addl > 0 && i2 % 10 == 0) { i2 /= 10; addl--; }
        if (i1 == long.MinValue && i2 == -1) throw Err.IntegerOverflow();
        var q = i1 / i2;
        if (q >= 0)
            while (addl > 0 && q <= MaxInt64Limit) { q *= 10; addl--; }
        else
            while (addl > 0 && q >= MinInt64Limit) { q *= 10; addl--; }
        if (addl > 0) throw Err.NumericOverflow();
        return Value.Int(q, scale);
    }

    /// <summary>MOV_get_int128: the operand as an Int128 at the given scale.</summary>
    static Int128 I128(in Value v, int scale)
    {
        var x = Num(v);
        if (x.Tag is VTag.Int or VTag.Int128) return Cvt.Int128SetScale(x.AsInt128, x.Scale, scale);
        return Conv.Cast(x, new SqlType(TypeKind.Int128, scale: scale)).AsInt128;
    }

    static Value AddI128(in Value a, in Value b, int scale, bool sub)
    {
        var d1 = I128(a, scale);
        var d2 = I128(b, scale);
        Int128 r;
        try { r = sub ? checked(d1 - d2) : checked(d1 + d2); }
        catch (OverflowException) { throw Err.IntegerOverflow128(); }
        return Value.Wide(r, scale);
    }

    static Value MulI128(in Value a, in Value b, int scale)
    {
        var x = Num(a);
        var y = Num(b);
        var s2 = y.Tag is VTag.Int or VTag.Int128 ? y.Scale : 0;
        var d1 = I128(x, scale - s2);
        var d2 = I128(y, s2);
        Int128 r;
        try { r = checked(d1 * d2); }
        catch (OverflowException) { throw Err.IntegerOverflow128(); }
        return Value.Wide(r, scale);
    }

    static Value DivI128(in Value a, in Value b, int scale)
    {
        var x = Num(a);
        var y = Num(b);
        var s2 = y.Tag is VTag.Int or VTag.Int128 ? y.Scale : 0;
        var op2 = I128(y, s2);
        var op1 = I128(x, scale - s2);
        var dividend = op1;
        // Int128::div(op2, -2 * s2)
        if (op1 == Int128.MinValue && op2 == -1) throw Err.IntegerOverflow();
        if (op2 == 0) throw Err.DivideByZero();
        var maxBy10 = Int128.MaxValue / 10;
        var minBy10 = Int128.MinValue / 10;
        var need = 2 * s2;
        var sign1 = op1.CompareTo(Int128.Zero);
        while (need > 0 && (sign1 >= 0 ? op1 <= maxBy10 : op1 >= minBy10))
        {
            op1 *= 10;
            need--;
        }
        // (Firebird looks at the dividend's last digit here, not the divisor's)
        while (need > 0)
        {
            if (dividend % 10 != 0) break;
            op2 /= 10;
            need--;
        }
        op1 /= op2;
        // the digits that could not be added before dividing are added now (overflow: numeric out of range)
        return Value.Wide(Cvt.Int128SetScale(op1, 0, need), scale);
    }

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
            case VTag.Date when b.Tag == VTag.Time: return Value.Timestamp(a.L * Value.TicksPerDay + b.L);
            case VTag.Time when b.Tag == VTag.Date: return Value.Timestamp(b.L * Value.TicksPerDay + a.L);
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
        if (v.Tag is not (VTag.Int or VTag.Double)) throw Cvt.ConversionError(v);
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
            if (x.L == long.MinValue) throw Err.IntegerOverflow();
            return Value.Int(-x.L, x.Scale);
        }
        if (x.Tag == VTag.Int128)
        {
            if (x.AsInt128 == Int128.MinValue) throw Err.IntegerOverflow128();
            return Value.Wide(-x.AsInt128, x.Scale);
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
                throw Err.DateTimeOp();
            case VTag.Timestamp when b.Tag == VTag.Timestamp:
                return TimestampDiff(a.L, b.L);
            case VTag.Timestamp when b.Tag == VTag.Date:
                throw Err.DateTimeOp();
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
