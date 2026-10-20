using System.Runtime.CompilerServices;

namespace Fakebird.Types;

public enum VTag : byte { Null, Bool, Int, Double, String, Bytes, Date, Time, Timestamp, Int128 }

/// <summary>
/// Runtime SQL value. Unboxed for numbers/dates: exact numerics are a scaled long (L, Scale), doubles are
/// stored as raw bits in L, DATE is the Firebird day number, TIME is 1/10000 seconds since midnight and
/// TIMESTAMP is day*TicksPerDay+time. Strings are .NET strings, binary data is byte[].
/// </summary>
public readonly struct Value
{
    public const long TicksPerDay = 864_000_000L; // 1/10000 s
    public const long TicksPerSecond = 10_000L;

    public readonly object? O;
    public readonly long L;
    public readonly VTag Tag;
    public readonly byte Scale;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    Value(VTag tag, long l, object? o, byte scale)
    {
        Tag = tag;
        L = l;
        O = o;
        Scale = scale;
    }

    public static readonly Value Null = default;
    public static readonly Value True = new(VTag.Bool, 1, null, 0);
    public static readonly Value False = new(VTag.Bool, 0, null, 0);

    public bool IsNull
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Tag == VTag.Null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Value Int(long v, int scale = 0) => new(VTag.Int, v, null, (byte)scale);
    public static Value Bool(bool b) => b ? True : False;

    /// <summary>
    /// A value of INT128 type: the low 64 bits in L and, only when it does not fit in a long, the high 64 bits
    /// boxed in O. The tag remembers the type so conversions report INT128's errors.
    /// </summary>
    public static Value Wide(Int128 v, int scale = 0)
    {
        var low = (long)(ulong)(UInt128)v;
        return v >= long.MinValue && v <= long.MaxValue
            ? new Value(VTag.Int128, low, null, (byte)scale)
            : new Value(VTag.Int128, low, (long)(v >> 64), (byte)scale);
    }

    /// <summary>The exact value of an Int or Int128 value.</summary>
    public Int128 AsInt128 => Tag == VTag.Int128 && O is long hi ? (Int128)(((UInt128)(ulong)hi << 64) | (ulong)L) : L;

    /// <summary>An Int128 value that fits in a long (L holds it).</summary>
    public bool FitsLong => O == null;
    public static Value Dbl(double d) => new(VTag.Double, BitConverter.DoubleToInt64Bits(d), null, 0);
    /// <summary>A FLOAT value (single precision, carried as a double; Scale 1 marks it for text conversion).</summary>
    public static Value Flt(float f) => new(VTag.Double, BitConverter.DoubleToInt64Bits(f), null, 1);
    public bool IsFloat => Tag == VTag.Double && Scale == 1;
    public static Value Str(string s) => new(VTag.String, 0, s, 0);
    public static Value Bytes(byte[] b) => new(VTag.Bytes, 0, b, 0);
    public static Value Date(long day) => new(VTag.Date, day, null, 0);
    public static Value Time(long ticks) => new(VTag.Time, ticks, null, 0);
    public static Value Timestamp(long ticks) => new(VTag.Timestamp, ticks, null, 0);

    public bool AsBool => L != 0;
    public double AsDouble => BitConverter.Int64BitsToDouble(L);
    public string AsString => (string)O!;
    public byte[] AsBytes => (byte[])O!;

    /// <summary>Numeric value as double regardless of representation.</summary>
    public double ToDouble() => Tag switch
    {
        VTag.Double => AsDouble,
        VTag.Int => Scale == 0 ? L : L / Pow10Double[Scale],
        _ => Conv.ToDouble(this),
    };

    public static readonly long[] Pow10 =
    [
        1L, 10L, 100L, 1000L, 10000L, 100000L, 1000000L, 10000000L, 100000000L, 1000000000L,
        10000000000L, 100000000000L, 1000000000000L, 10000000000000L, 100000000000000L,
        1000000000000000L, 10000000000000000L, 100000000000000000L, 1000000000000000000L,
    ];

    public static readonly double[] Pow10Double =
    [
        1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11, 1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18,
    ];

    public override string ToString() => IsNull ? "NULL" : Conv.ToStr(this);

    /// <summary>Converts to a CLR object (used by in-process APIs and tests).</summary>
    public object? ToClr() => Tag switch
    {
        VTag.Null => null,
        VTag.Bool => AsBool,
        VTag.Int => Scale == 0 ? L : (object)Conv.ToDecimal(this),
        VTag.Int128 => Scale == 0 && FitsLong ? L : Scale == 0 ? AsInt128 : (object)Conv.ToDecimal(this),
        VTag.Double => AsDouble,
        VTag.String => AsString,
        VTag.Bytes => AsBytes,
        VTag.Date => Conv.DateToDateTime(L),
        VTag.Time => TimeSpan.FromTicks(L * 1000),
        VTag.Timestamp => Conv.TimestampToDateTime(L),
        _ => null,
    };
}
