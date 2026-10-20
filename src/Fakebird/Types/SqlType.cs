using Fakebird.Wire;

namespace Fakebird.Types;

public enum TypeKind : byte
{
    Null,
    SmallInt,
    Integer,
    BigInt,
    Float,
    Double,
    Char,
    VarChar,
    Blob,
    Date,
    Time,
    Timestamp,
    Boolean,
    /// <summary>Firebird 4+ INT128 (also the internal type of exact multiplication/division with a BIGINT, and of SUM(BIGINT)).</summary>
    Int128,
    /// <summary>Firebird 4+ TIMESTAMP WITH TIME ZONE and TIME WITH TIME ZONE.</summary>
    TimestampTz,
    TimeTz,
}

public enum Charset : byte { None, Octets, Utf8, Ascii, Win1252, Iso8859_1, UnicodeFss, Win1250, Win1251 }

/// <summary>Binary (code point order), and the ICU collations UNICODE, UNICODE_CI and UNICODE_CI_AI.</summary>
public enum Collation : byte { Binary, CaseInsensitive, CaseAccentInsensitive, Unicode }

/// <summary>
/// A static SQL type (column, domain, expression, parameter). Exact numerics are represented as
/// SmallInt/Integer/BigInt with Scale &gt; 0 and SubType 1 (NUMERIC) or 2 (DECIMAL), as Firebird does.
/// </summary>
public sealed class SqlType : IEquatable<SqlType>
{
    public TypeKind Kind { get; }
    /// <summary>Character length for CHAR/VARCHAR.</summary>
    public int Length { get; }
    /// <summary>Number of decimal digits (positive) for exact numerics.</summary>
    public int Scale { get; }
    public int Precision { get; }
    /// <summary>Blob subtype (0 binary, 1 text) or numeric subtype (1 NUMERIC, 2 DECIMAL).</summary>
    public int SubType { get; }
    public Charset Charset { get; }
    public Collation Collation { get; }

    public SqlType(TypeKind kind, int length = 0, int scale = 0, int precision = 0, int subType = 0,
        Charset charset = Charset.Utf8, Collation collation = Collation.Binary)
    {
        Kind = kind;
        Length = length;
        Scale = scale;
        Precision = precision;
        SubType = subType;
        Charset = kind is TypeKind.Char or TypeKind.VarChar || (kind == TypeKind.Blob && subType == 1) ? charset : Charset.None;
        if (kind == TypeKind.Blob && subType != 1) Charset = Charset.Octets;
        Collation = collation;
    }

    public static readonly SqlType Null = new(TypeKind.Null);
    public static readonly SqlType SmallInt = new(TypeKind.SmallInt);
    public static readonly SqlType Integer = new(TypeKind.Integer);
    public static readonly SqlType BigInt = new(TypeKind.BigInt);
    public static readonly SqlType Int128 = new(TypeKind.Int128);
    public static readonly SqlType Float = new(TypeKind.Float);
    public static readonly SqlType Double = new(TypeKind.Double);
    public static readonly SqlType Date = new(TypeKind.Date);
    public static readonly SqlType Time = new(TypeKind.Time);
    public static readonly SqlType Timestamp = new(TypeKind.Timestamp);
    public static readonly SqlType TimestampTz = new(TypeKind.TimestampTz);
    public static readonly SqlType TimeTz = new(TypeKind.TimeTz);
    public static readonly SqlType Boolean = new(TypeKind.Boolean);
    public static readonly SqlType BlobText = new(TypeKind.Blob, subType: 1);
    public static readonly SqlType BlobBinary = new(TypeKind.Blob, subType: 0);

    public static SqlType VarChar(int length, Charset cs = Charset.Utf8, Collation col = Collation.Binary)
        => new(TypeKind.VarChar, length: length, charset: cs, collation: col);

    public static SqlType Char(int length, Charset cs = Charset.Utf8, Collation col = Collation.Binary)
        => new(TypeKind.Char, length: length, charset: cs, collation: col);

    /// <summary>NUMERIC/DECIMAL(p, s) mapped to its storage type like Firebird dialect 3.</summary>
    public static SqlType Numeric(int precision, int scale, bool isDecimal = false)
    {
        if (precision < 1 || precision > 38) throw Err.Dsql(-842, $"Precision must be from 1 to 38");
        if (scale < 0 || scale > precision) throw Err.Dsql(-842, "Scale must be between zero and precision");
        // Firebird 4+: precisions above 18 are stored as INT128
        TypeKind kind = precision <= 4 && !isDecimal ? TypeKind.SmallInt
            : precision <= 9 ? TypeKind.Integer
            : precision <= 18 ? TypeKind.BigInt
            : TypeKind.Int128;
        return new SqlType(kind, scale: scale, precision: precision, subType: isDecimal ? 2 : 1);
    }

    /// <summary>Exact numeric with storage kind and scale (used for expression result types).</summary>
    public static SqlType Exact(TypeKind kind, int scale)
    {
        if (scale == 0)
            return kind switch { TypeKind.SmallInt => SmallInt, TypeKind.Integer => Integer, _ => BigInt };
        var prec = kind switch { TypeKind.SmallInt => 4, TypeKind.Integer => 9, _ => 18 };
        return new SqlType(kind, scale: scale, precision: prec, subType: 1);
    }

    /// <summary>Exact numeric produced by an expression: Firebird leaves the subtype 0 (clients show DECIMAL).</summary>
    public static SqlType ExactExpr(TypeKind kind, int scale)
        => scale == 0 ? Exact(kind, 0) : new SqlType(kind, scale: scale, precision: kind == TypeKind.Integer ? 9 : 18);

    public bool IsExact => Kind is TypeKind.SmallInt or TypeKind.Integer or TypeKind.BigInt or TypeKind.Int128;
    public bool IsApprox => Kind is TypeKind.Float or TypeKind.Double;
    public bool IsNumeric => IsExact || IsApprox;
    public bool IsString => Kind is TypeKind.Char or TypeKind.VarChar;
    public bool IsTextBlob => Kind == TypeKind.Blob && SubType == 1;
    public bool IsTextual => IsString || IsTextBlob;
    public bool IsDateTime => Kind is TypeKind.Date or TypeKind.Time or TypeKind.Timestamp or TypeKind.TimestampTz or TypeKind.TimeTz;
    public bool IsTz => Kind is TypeKind.TimestampTz or TypeKind.TimeTz;
    public bool IsOctets => Charset == Charset.Octets && IsString;
    /// <summary>Values of this type are carried as byte[] (OCTETS strings and binary blobs).</summary>
    public bool IsBinary => (IsString && Charset == Charset.Octets) || (Kind == TypeKind.Blob && SubType != 1);

    public int BytesPerChar => Charset switch
    {
        Charset.Utf8 => 4,
        Charset.UnicodeFss => 3,
        _ => 1,
    };

    /// <summary>Byte length as described on the wire.</summary>
    public int ByteLength => Kind switch
    {
        TypeKind.Char or TypeKind.VarChar => Length * BytesPerChar,
        TypeKind.SmallInt => 2,
        TypeKind.Integer => 4,
        TypeKind.BigInt => 8,
        TypeKind.Int128 => 16,
        TypeKind.Float => 4,
        TypeKind.Double => 8,
        TypeKind.Date => 4,
        TypeKind.Time => 4,
        TypeKind.Timestamp => 8,
        TypeKind.TimestampTz => 12,
        TypeKind.TimeTz => 8,
        TypeKind.Boolean => 1,
        TypeKind.Blob => 8,
        _ => 0,
    };

    public int WireCharsetId => Charset switch
    {
        Charset.None => Isc.CS_NONE,
        Charset.Octets => Isc.CS_OCTETS,
        Charset.Ascii => 2,
        Charset.UnicodeFss => 3,
        Charset.Utf8 => Isc.CS_UTF8,
        Charset.Iso8859_1 => 21,
        Charset.Win1250 => 51,
        Charset.Win1251 => 52,
        Charset.Win1252 => 53,
        _ => Isc.CS_UTF8,
    };

    public int WireSqlType => Kind switch
    {
        TypeKind.Null => Isc.SQL_NULL,
        TypeKind.SmallInt => Isc.SQL_SHORT,
        TypeKind.Integer => Isc.SQL_LONG,
        TypeKind.BigInt => Isc.SQL_INT64,
        TypeKind.Int128 => Isc.SQL_INT128,
        TypeKind.Float => Isc.SQL_FLOAT,
        TypeKind.Double => Isc.SQL_DOUBLE,
        TypeKind.Char => Isc.SQL_TEXT,
        TypeKind.VarChar => Isc.SQL_VARYING,
        TypeKind.Blob => Isc.SQL_BLOB,
        TypeKind.Date => Isc.SQL_TYPE_DATE,
        TypeKind.Time => Isc.SQL_TYPE_TIME,
        TypeKind.Timestamp => Isc.SQL_TIMESTAMP,
        TypeKind.TimestampTz => Isc.SQL_TIMESTAMP_TZ,
        TypeKind.TimeTz => Isc.SQL_TIME_TZ,
        TypeKind.Boolean => Isc.SQL_BOOLEAN,
        _ => throw new InvalidOperationException(),
    };

    public int WireSubType => Kind switch
    {
        TypeKind.Char or TypeKind.VarChar => WireCharsetId,
        TypeKind.Blob => SubType,
        TypeKind.SmallInt or TypeKind.Integer or TypeKind.BigInt or TypeKind.Int128 => SubType,
        _ => 0,
    };

    /// <summary>
    /// The type as described to clients with DataTypeCompatibility = 3.0 (Firebird's "legacy" bindings): INT128
    /// becomes BIGINT with the same scale and subtype, and the WITH TIME ZONE types become TIMESTAMP and TIME (in the
    /// session time zone).
    /// </summary>
    public SqlType Bound => Kind switch
    {
        TypeKind.Int128 => new SqlType(TypeKind.BigInt, scale: Scale, precision: 18, subType: SubType),
        TypeKind.TimestampTz => Timestamp,
        TypeKind.TimeTz => Time,
        _ => this,
    };

    public SqlType WithCollation(Collation c) => c == Collation ? this
        : new SqlType(Kind, Length, Scale, Precision, SubType, Charset, c);

    public SqlType WithLength(int len) => len == Length ? this
        : new SqlType(Kind, len, Scale, Precision, SubType, Charset, Collation);

    public SqlType WithCharset(Charset cs) => cs == Charset ? this
        : new SqlType(Kind, Length, Scale, Precision, SubType, cs, Collation);

    public bool Equals(SqlType? o) => o is not null && Kind == o.Kind && Length == o.Length && Scale == o.Scale
        && SubType == o.SubType && Charset == o.Charset && Collation == o.Collation && Precision == o.Precision;
    public override bool Equals(object? obj) => Equals(obj as SqlType);
    public override int GetHashCode() => HashCode.Combine(Kind, Length, Scale, SubType, Charset, Collation);

    public override string ToString() => Kind switch
    {
        TypeKind.SmallInt or TypeKind.Integer or TypeKind.BigInt or TypeKind.Int128 when Scale > 0 =>
            $"{(SubType == 2 ? "DECIMAL" : "NUMERIC")}({Precision}, {Scale})",
        TypeKind.SmallInt => "SMALLINT",
        TypeKind.Integer => "INTEGER",
        TypeKind.BigInt => "BIGINT",
        TypeKind.Int128 => "INT128",
        TypeKind.Float => "FLOAT",
        TypeKind.Double => "DOUBLE PRECISION",
        TypeKind.Char => $"CHAR({Length})",
        TypeKind.VarChar => $"VARCHAR({Length})",
        TypeKind.Blob => $"BLOB SUB_TYPE {SubType}",
        TypeKind.Date => "DATE",
        TypeKind.Time => "TIME",
        TypeKind.Timestamp => "TIMESTAMP",
        TypeKind.TimestampTz => "TIMESTAMP WITH TIME ZONE",
        TypeKind.TimeTz => "TIME WITH TIME ZONE",
        TypeKind.Boolean => "BOOLEAN",
        _ => "NULL",
    };
}
