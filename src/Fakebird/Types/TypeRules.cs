using Fakebird.Exec;

namespace Fakebird.Types;

/// <summary>
/// Result descriptors of expressions, ported from Firebird 5 (dsql/ExprNodes.cpp ArithmeticNode::makeDialect3,
/// jrd/DataTypeUtil.cpp makeFromList / makeConcatenate). A descriptor is a type plus nullability; a NULL literal
/// has <see cref="TypeKind.Null"/> and an untyped parameter is passed as null (unknown).
/// </summary>
public static class TypeRules
{
    /// <summary>Firebird's dtype numbering, used where it compares dtypes with MAX().</summary>
    public static int Dtype(SqlType t) => t.Kind switch
    {
        TypeKind.Char => 1,
        TypeKind.VarChar => 3,
        TypeKind.SmallInt => 8,
        TypeKind.Integer => 9,
        TypeKind.Float => 11,
        TypeKind.Double => 12,
        TypeKind.Date => 14,
        TypeKind.Time => 15,
        TypeKind.Timestamp => 16,
        TypeKind.Blob => 17,
        TypeKind.BigInt => 19,
        TypeKind.Boolean => 21,
        TypeKind.Int128 => 24,
        _ => 0,
    };


    /// <summary>Exact numeric descriptor (subtype 0 none, 1 NUMERIC, 2 DECIMAL).</summary>
    public static SqlType Exact(TypeKind kind, int scale, int subType)
    {
        if (scale == 0 && subType == 0) return kind == TypeKind.Int128 ? SqlType.Int128 : SqlType.Exact(kind, 0);
        var prec = kind switch { TypeKind.SmallInt => 4, TypeKind.Integer => 9, TypeKind.BigInt => 18, _ => 38 };
        return new SqlType(kind, scale: scale, precision: prec, subType: subType);
    }

    static int NumSub(SqlType t) => t.IsExact ? t.SubType : 0;

    /// <summary>Length of a value converted to text (DSC_string_length, in characters).</summary>
    public static int StringLength(SqlType t) => t.Kind switch
    {
        TypeKind.Char or TypeKind.VarChar => t.Length,
        TypeKind.SmallInt => 6 + (t.Scale > 0 ? 1 : 0),
        TypeKind.Integer => 11 + (t.Scale > 0 ? 1 : 0),
        TypeKind.BigInt => 20 + (t.Scale > 0 ? 1 : 0),
        TypeKind.Int128 => 47 + (t.Scale > 0 ? 1 : 0),
        TypeKind.Float => 15,
        TypeKind.Double => 24,
        TypeKind.Date => 10,
        TypeKind.Time => 13,
        TypeKind.Timestamp => 25,
        TypeKind.Blob => 9,
        TypeKind.Boolean => 5,
        _ => 0,
    };

    static FbError DsqlError(params int[] codes)
    {
        var b = new StatusBuilder().Gds(Err.isc_dsql_error);
        foreach (var c in codes) b.Gds(c);
        return b.Build();
    }

    /// <summary>
    /// Result of a dialect 3 arithmetic operator. A NULL operand takes the other operand's type (NULL + NULL is
    /// INTEGER); the result is nullable when an operand is.
    /// </summary>
    public static (SqlType Type, bool Nullable) Arith(ArithOp op, SqlType a, bool aNullable, SqlType b, bool bNullable)
    {
        if (a.Kind == TypeKind.Null && b.Kind == TypeKind.Null) return (SqlType.Integer, true);
        if (a.Kind == TypeKind.Null) { a = b; aNullable = true; }
        if (b.Kind == TypeKind.Null) { b = a; bNullable = true; }
        var nullable = aNullable || bNullable;

        switch (op)
        {
            case ArithOp.Add:
            case ArithOp.Sub:
            {
                if (a.Kind == TypeKind.Blob || b.Kind == TypeKind.Blob)
                    throw new StatusBuilder().Gds(Err.isc_sqlerr).Num(-607).Gds(FbMessages.dsql_no_blob_array).Build();
                if (a.IsString || b.IsString)
                    throw DsqlError(Err.isc_expression_eval_err, FbMessages.dsql_nostring_addsub_dial3);
                if (a.IsExact && b.IsExact)
                    return (Exact(a.Kind == TypeKind.Int128 || b.Kind == TypeKind.Int128 ? TypeKind.Int128 : TypeKind.BigInt,
                        Math.Max(a.Scale, b.Scale), Math.Max(NumSub(a), NumSub(b))), nullable);
                if (a.IsNumeric && b.IsNumeric) return (SqlType.Double, nullable);
                // mixed numeric / non-numeric: the date/time operand decides
                if (a.IsDateTime && b.IsDateTime)
                {
                    if (op == ArithOp.Sub)
                    {
                        if (a.Kind == b.Kind)
                            return (a.Kind switch
                            {
                                TypeKind.Date => SqlType.Integer,
                                TypeKind.Time => new SqlType(TypeKind.Integer, scale: 4, precision: 9, subType: 1),
                                _ => new SqlType(TypeKind.BigInt, scale: 9, precision: 18, subType: 1),
                            }, nullable);
                        // DSQL accepts TIMESTAMP - DATE (the engine rejects it when evaluating)
                        if (a.Kind == TypeKind.Timestamp && b.Kind == TypeKind.Date || a.Kind == TypeKind.Date && b.Kind == TypeKind.Timestamp)
                            return (new SqlType(TypeKind.BigInt, scale: 9, precision: 18, subType: 1), nullable);
                        throw DsqlError(Err.isc_expression_eval_err, FbMessages.dsql_invalid_datetime_subtract);
                    }
                    if (a.Kind == TypeKind.Date && b.Kind == TypeKind.Time || a.Kind == TypeKind.Time && b.Kind == TypeKind.Date)
                        return (SqlType.Timestamp, nullable);
                    throw DsqlError(Err.isc_expression_eval_err, FbMessages.dsql_invalid_dateortime_add);
                }
                if (a.IsDateTime && (b.IsNumeric || b.Kind == TypeKind.Boolean)) return (a, nullable);
                if (b.IsDateTime && (a.IsNumeric || a.Kind == TypeKind.Boolean))
                {
                    if (op == ArithOp.Add) return (b, nullable);
                    throw DsqlError(Err.isc_expression_eval_err, FbMessages.dsql_invalid_type_minus_date);
                }
                throw DsqlError(Err.isc_expression_eval_err, FbMessages.dsql_invalid_type_addsub_dial3);
            }
            case ArithOp.Mul:
            case ArithOp.Div:
            {
                var mul = op == ArithOp.Mul;
                if (a.IsString || b.IsString)
                    throw DsqlError(Err.isc_expression_eval_err, mul ? FbMessages.dsql_nostring_multip_dial3 : FbMessages.dsql_nostring_div_dial3);
                if (a.Kind == TypeKind.Blob || b.Kind == TypeKind.Blob)
                    throw new StatusBuilder().Gds(Err.isc_sqlerr).Num(-607).Gds(FbMessages.dsql_no_blob_array).Build();
                if (a.IsExact && b.IsExact)
                {
                    var scale = a.Scale + b.Scale;
                    // DSC_multiply_result: SMALLINT/INTEGER pairs give BIGINT, anything with a BIGINT gives INT128.
                    // BIGINT division drops NUMERIC/DECIMAL, multiplication and INT128 keep it.
                    var wide = a.Kind is TypeKind.BigInt or TypeKind.Int128 || b.Kind is TypeKind.BigInt or TypeKind.Int128;
                    var sub = mul || wide ? Math.Max(NumSub(a), NumSub(b)) : 0;
                    return (Exact(wide ? TypeKind.Int128 : TypeKind.BigInt, scale, sub), nullable);
                }
                if (a.IsNumeric && b.IsNumeric) return (SqlType.Double, nullable);
                throw DsqlError(Err.isc_expression_eval_err, mul ? FbMessages.dsql_invalid_type_multip_dial3 : FbMessages.dsql_invalid_type_div_dial3);
            }
        }
        throw new InvalidOperationException(op.ToString());
    }

    /// <summary>
    /// Common type of a list of values (CASE, COALESCE, IIF, DECODE, UNION, MINVALUE...). Unknown (null) entries are
    /// parameters still to be typed; NULL literals are skipped but make the result nullable.
    /// </summary>
    public static (SqlType Type, bool Nullable) FromList(IReadOnlyList<(SqlType? Type, bool Nullable)> args, string expressionName)
    {
        SqlType? result = null;
        var allNulls = true;
        var nullable = false;
        var anyVarying = false;
        var anyBlobOrText = false;
        var textLen = 0;
        SqlType? textType = null; // charset/collation donor

        // makeBlobOrText: the result becomes text as long as the longest value converted to text
        void MakeText(SqlType arg)
        {
            textLen = Math.Max(StringLength(arg), result == null ? 0 : result.IsString ? textLen : StringLength(result));
            textType = ResultTextType(textType, arg);
            result = SqlType.Char(textLen);
        }

        foreach (var (t, n) in args)
        {
            allNulls &= t is { Kind: TypeKind.Null };
            if (t == null || t.Kind == TypeKind.Null)
            {
                nullable = true;
                continue;
            }
            nullable |= n;
            anyVarying |= t.Kind != TypeKind.Char;

            if (t.Kind == TypeKind.Blob || result is { Kind: TypeKind.Blob })
            {
                result = BlobOf(result, t);
                anyBlobOrText = true;
            }
            else if (t.IsString || result is { IsString: true })
            {
                MakeText(t);
                anyBlobOrText = true;
            }
            else if (t.IsNumeric)
            {
                if (result == null || result.IsNumeric)
                {
                    if (!t.IsExact && result is { IsExact: true }) result = t;
                    else if (result == null) result = t;
                    else if (result.IsExact && t.IsExact)
                    {
                        var kind = Dtype(t) > Dtype(result) ? t.Kind : result.Kind;
                        result = Exact(kind, Math.Max(result.Scale, t.Scale), Math.Max(NumSub(result), NumSub(t)));
                    }
                    else if (!result.IsExact && !t.IsExact)
                        result = Dtype(t) > Dtype(result) ? t : result;
                }
                else
                    MakeText(t);
            }
            else if (t.IsDateTime || t.Kind == TypeKind.Boolean)
            {
                if (result == null) result = t;
                else if (result.Kind != t.Kind)
                    MakeText(t);
            }
            else
                throw new StatusBuilder().Gds(Err.isc_sqlerr).Num(-804).Gds(Err.isc_dsql_datatype_err).Build();
        }

        if (!anyBlobOrText && result is { IsString: true })
            throw new StatusBuilder().Gds(Err.isc_sqlerr).Num(-104).Gds(FbMessages.dsql_datatypes_not_comparable)
                .Str("").Str(expressionName).Build();

        if (allNulls) return (SqlType.Char(1, Charset.None), true);
        if (result == null) return (SqlType.Null, true);
        if (result.IsString)
        {
            var cs = textType?.IsString == true || textType?.IsTextBlob == true ? textType.Charset : Charset.Ascii;
            var col = textType?.IsString == true ? textType.Collation : Collation.Binary;
            var len = Math.Min(textLen, 32765 / (cs == Charset.Utf8 ? 4 : cs == Charset.UnicodeFss ? 3 : 1));
            result = anyVarying ? SqlType.VarChar(len, cs, col) : SqlType.Char(len, cs, col);
        }
        return (result, nullable);
    }

    static SqlType BlobOf(SqlType? result, SqlType arg)
    {
        // a binary blob anywhere makes the result binary
        var binary = arg.Kind == TypeKind.Blob && arg.SubType != 1 || result is { Kind: TypeKind.Blob, SubType: not 1 };
        if (binary) return SqlType.BlobBinary;
        var text = arg.IsTextual ? arg : result is { IsTextual: true } ? result : null;
        return text != null && text.Charset != Charset.Utf8 ? new SqlType(TypeKind.Blob, subType: 1, charset: text.Charset) : SqlType.BlobText;
    }

    /// <summary>getResultTextType: OCTETS wins, NONE/ASCII give way to a real charset, else the first one.</summary>
    static SqlType? ResultTextType(SqlType? cur, SqlType arg)
    {
        if (!arg.IsTextual) return cur;
        if (cur == null) return arg;
        if (cur.Charset == Charset.None || arg.Charset == Charset.Octets) return arg;
        if (cur.Charset == Charset.Ascii && arg.Charset != Charset.None) return arg;
        return cur;
    }

    static int Bpc(Charset cs) => cs switch { Charset.Utf8 => 4, Charset.UnicodeFss => 3, _ => 1 };

    /// <summary>Character set of a descriptor: text and text blobs have one, everything else is NONE.</summary>
    static Charset CharsetOf(SqlType t) => t.IsString || t.IsTextBlob ? t.Charset : t.Kind == TypeKind.Blob ? Charset.Octets : Charset.None;

    /// <summary>
    /// ConcatenateNode::make / makeConcatenate: a NULL operand counts as empty text in the other's character set; the
    /// result charset follows getResultTextType; lengths add up in the result's bytes (raw bytes for NONE/OCTETS).
    /// </summary>
    public static (SqlType Type, bool Nullable) Concat(SqlType a, bool aNullable, SqlType b, bool bNullable)
    {
        var nullable = aNullable || bNullable;
        if (a.Kind == TypeKind.Null && b.Kind == TypeKind.Null) return (SqlType.Char(1, Charset.None), true);
        if (a.Kind == TypeKind.Null) a = SqlType.Char(0, CharsetOf(b), b.Collation);
        if (b.Kind == TypeKind.Null) b = SqlType.Char(0, CharsetOf(a), a.Collation);
        var ca = CharsetOf(a);
        var cb = CharsetOf(b);
        // getResultTextType
        var (cs, donor) = ca == Charset.None || cb == Charset.Octets ? (cb, b) : ca == Charset.Ascii && cb != Charset.None ? (cb, b) : (ca, a);
        var collation = donor.IsString ? donor.Collation : Collation.Binary;
        if (a.Kind == TypeKind.Blob || b.Kind == TypeKind.Blob)
        {
            // getResultBlobSubType: a binary second operand makes the blob binary, else the first one decides
            var sub = b.Kind == TypeKind.Blob && b.SubType != 1 || b.Kind != TypeKind.Blob && cb == Charset.Octets ? 0
                : a.Kind == TypeKind.Blob ? a.SubType : 1;
            return (sub == 1 ? new SqlType(TypeKind.Blob, subType: 1, charset: cs == Charset.None || cs == Charset.Octets ? Charset.Utf8 : cs) : SqlType.BlobBinary, nullable);
        }
        long Bytes(SqlType t, Charset c) => t.IsString ? (long)t.Length * Bpc(c) : StringLength(t);
        var raw = cs is Charset.None or Charset.Octets;
        long len = raw
            ? Bytes(a, ca) + Bytes(b, cb)
            : Bytes(a, ca) / Bpc(ca) + Bytes(b, cb) / Bpc(cb);
        var max = 32765 / Bpc(cs);
        return (SqlType.VarChar((int)Math.Min(len, max), cs, collation), nullable);
    }

    /// <summary>Convenience overload over bound nodes.</summary>
    public static (SqlType Type, bool Nullable) FromList(IEnumerable<XNode?> nodes, string expressionName)
        => FromList(nodes.Select(n => n == null ? ((SqlType?)null, true) : (n.Type, !n.NotNull)).ToList(), expressionName);
}
