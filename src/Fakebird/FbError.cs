using System.Text;

namespace Fakebird;

/// <summary>One element of a Firebird status vector.</summary>
public readonly record struct StatusArg(int Type, int Number, string? Text);

/// <summary>
/// An error raised by the engine. Carries a Firebird-compatible status vector so the wire layer can
/// send it to the client and the client raises a faithful FbException (ErrorCode, SQLSTATE, message).
/// </summary>
public sealed class FbError : Exception
{
    public IReadOnlyList<StatusArg> Status { get; }
    public string SqlState { get; }
    public int GdsCode => Status.Count > 0 ? Status[0].Number : 0;
    /// <summary>Name of the user exception (CREATE EXCEPTION) when raised by PSQL.</summary>
    public string? UserExceptionName { get; set; }

    public FbError(IReadOnlyList<StatusArg> status, string sqlState)
        : base(Render(status))
    {
        Status = status;
        SqlState = sqlState;
    }

    public bool HasCode(int code)
    {
        foreach (var a in Status)
            if (a.Type == Wire.Isc.arg_gds && a.Number == code)
                return true;
        return false;
    }

    static readonly Dictionary<int, string> Messages = new()
    {
        [335544321] = "arithmetic exception, numeric overflow, or string truncation",
        [335544334] = "conversion error from string \"{0}\"",
        [335544336] = "deadlock",
        [335544347] = "validation error for column {0}, value \"{1}\"",
        [335544351] = "unsuccessful metadata update",
        [335544382] = "{0}",
        [335544436] = "SQL error code = {0}",
        [335544451] = "update conflicts with concurrent update",
        [335544466] = "violation of FOREIGN KEY constraint \"{0}\" on table \"{1}\"",
        [335544517] = "exception {0}",
        [335544558] = "Operation violates CHECK constraint {0} on view or table {1}",
        [335544569] = "Dynamic SQL Error",
        [335544573] = "Data type unknown",
        [335544578] = "Column unknown",
        [335544580] = "Table unknown",
        [335544634] = "Token unknown - line {0}, column {1}",
        [335544652] = "multiple rows in singleton select",
        [335544665] = "violation of PRIMARY or UNIQUE KEY constraint \"{0}\" on table \"{1}\"",
        [335544778] = "Integer divide by zero.  The code attempted to divide an integer value by an integer divisor of zero.",
        [335544779] = "Integer overflow.  The result of an integer operation caused the most significant bit of the result to carry.",
        [335544838] = "Foreign key reference target does not exist",
        [335544839] = "Foreign key references are present for the record",
        [335544843] = "Context variable '{0}' is not found in namespace '{1}'",
        [335544878] = "concurrent transaction number is {0}",
        [335544914] = "string right truncation",
        [335544916] = "numeric value is out of range",
        [335545033] = "expected length {0}, actual {1}",
        [335545072] = "Problematic key value is {0}",
    };

    static string Render(IReadOnlyList<StatusArg> status)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < status.Count; i++)
        {
            var a = status[i];
            if (a.Type != Wire.Isc.arg_gds) continue;
            var args = new List<object>();
            for (var j = i + 1; j < status.Count && status[j].Type != Wire.Isc.arg_gds; j++)
            {
                if (status[j].Type == Wire.Isc.arg_string) args.Add(status[j].Text!);
                else if (status[j].Type == Wire.Isc.arg_number) args.Add(status[j].Number);
            }
            var text = Messages.TryGetValue(a.Number, out var fmt) ? SafeFormat(fmt, args) : $"error {a.Number}";
            if (sb.Length > 0) sb.Append(Environment.NewLine);
            sb.Append(text);
        }
        return sb.ToString();
    }

    static string SafeFormat(string fmt, List<object> args)
    {
        try { return string.Format(fmt, args.ToArray()); }
        catch (FormatException) { return fmt; }
    }
}

/// <summary>Builder of Firebird status vectors.</summary>
internal sealed class StatusBuilder
{
    readonly List<StatusArg> _args = new();
    public StatusBuilder Gds(int code) { _args.Add(new(Wire.Isc.arg_gds, code, null)); return this; }
    public StatusBuilder Str(string s) { _args.Add(new(Wire.Isc.arg_string, 0, s)); return this; }
    public StatusBuilder Num(int n) { _args.Add(new(Wire.Isc.arg_number, n, null)); return this; }
    public FbError Build(string sqlState) => new(_args, sqlState);
}

/// <summary>Factory for the errors the engine raises.</summary>
internal static class Err
{
    public const int isc_arith_except = 335544321;
    public const int isc_convert_error = 335544334;
    public const int isc_deadlock = 335544336;
    public const int isc_not_valid = 335544347;
    public const int isc_no_meta_update = 335544351;
    public const int isc_random = 335544382;
    public const int isc_sqlerr = 335544436;
    public const int isc_update_conflict = 335544451;
    public const int isc_foreign_key = 335544466;
    public const int isc_except = 335544517;
    public const int isc_check_constraint = 335544558;
    public const int isc_dsql_error = 335544569;
    public const int isc_dsql_datatype_err = 335544573;
    public const int isc_dsql_field_err = 335544578;
    public const int isc_dsql_relation_err = 335544580;
    public const int isc_dsql_token_unk_err = 335544634;
    public const int isc_sing_select_err = 335544652;
    public const int isc_unique_key_violation = 335544665;
    public const int isc_divide_by_zero = 335544778;
    public const int isc_integer_overflow = 335544779;
    public const int isc_fk_target_missing = 335544838;
    public const int isc_fk_references_present = 335544839;
    public const int isc_ctx_var_not_found = 335544843;
    public const int isc_concurrent_transaction = 335544878;
    public const int isc_string_truncation = 335544914;
    public const int isc_numeric_out_of_range = 335544916;
    public const int isc_trunc_limits = 335545033;
    public const int isc_idx_key_value = 335545072;

    public static FbError Random(string message, string sqlState = "HY000")
        => new StatusBuilder().Gds(isc_random).Str(message).Build(sqlState);

    /// <summary>Feature recognized but not implemented by the fake: fail loudly.</summary>
    public static FbError Unsupported(string what)
        => Random("Fakebird: unsupported feature: " + what, "0A000");

    public static FbError Syntax(int line, int col, string token)
        => new StatusBuilder().Gds(isc_dsql_error).Gds(isc_sqlerr).Num(-104)
            .Gds(isc_dsql_token_unk_err).Num(line).Num(col).Gds(isc_random).Str(token).Build("42000");

    public static FbError SyntaxMsg(string message)
        => new StatusBuilder().Gds(isc_dsql_error).Gds(isc_sqlerr).Num(-104).Gds(isc_random).Str(message).Build("42000");

    public static FbError TableUnknown(string name)
        => new StatusBuilder().Gds(isc_dsql_error).Gds(isc_sqlerr).Num(-204)
            .Gds(isc_dsql_relation_err).Gds(isc_random).Str(name).Build("42S02");

    public static FbError ColumnUnknown(string name)
        => new StatusBuilder().Gds(isc_dsql_error).Gds(isc_sqlerr).Num(-206)
            .Gds(isc_dsql_field_err).Gds(isc_random).Str(name).Build("42S22");

    public static FbError ObjectUnknown(string kind, string name)
        => new StatusBuilder().Gds(isc_dsql_error).Gds(isc_sqlerr).Num(-204)
            .Gds(isc_random).Str($"{kind} {name} unknown").Build("42000");

    public static FbError Dsql(int sqlCode, string message, string sqlState = "42000")
        => new StatusBuilder().Gds(isc_dsql_error).Gds(isc_sqlerr).Num(sqlCode).Gds(isc_random).Str(message).Build(sqlState);

    public static FbError MetaUpdate(string message)
        => new StatusBuilder().Gds(isc_no_meta_update).Gds(isc_random).Str(message).Build("42000");

    public static FbError DataTypeUnknown()
        => new StatusBuilder().Gds(isc_dsql_error).Gds(isc_sqlerr).Num(-804).Gds(isc_dsql_datatype_err).Build("42000");

    public static FbError NotNull(string column)
        => new StatusBuilder().Gds(isc_not_valid).Str(column).Str("*** null ***").Build("23000");

    public static FbError NotValidDomain(string column, Types.Value value)
        => new StatusBuilder().Gds(isc_not_valid).Str(column).Str(Types.Conv.ToStr(value)).Build("23000");

    public static FbError Check(string constraint, string table)
        => new StatusBuilder().Gds(isc_check_constraint).Str(constraint).Str(table).Build("23000");

    public static FbError Unique(string constraint, string table, string key)
        => new StatusBuilder().Gds(isc_unique_key_violation).Str(constraint).Str(table)
            .Gds(isc_idx_key_value).Str(key).Build("23000");

    public static FbError ForeignKeyMissing(string constraint, string table, string key)
        => new StatusBuilder().Gds(isc_foreign_key).Str(constraint).Str(table)
            .Gds(isc_fk_target_missing).Gds(isc_idx_key_value).Str(key).Build("23000");

    public static FbError ForeignKeyPresent(string constraint, string table, string key)
        => new StatusBuilder().Gds(isc_foreign_key).Str(constraint).Str(table)
            .Gds(isc_fk_references_present).Gds(isc_idx_key_value).Str(key).Build("23000");

    public static FbError StringTruncation(int expected, int actual)
        => new StatusBuilder().Gds(isc_arith_except).Gds(isc_string_truncation)
            .Gds(isc_trunc_limits).Num(expected).Num(actual).Build("22001");

    public static FbError NumericOverflow()
        => new StatusBuilder().Gds(isc_arith_except).Gds(isc_numeric_out_of_range).Build("22003");

    public static FbError DivideByZero()
        => new StatusBuilder().Gds(isc_arith_except).Gds(isc_divide_by_zero).Build("22012");

    public static FbError Conversion(string value)
        => new StatusBuilder().Gds(isc_convert_error).Str(value).Build("22018");

    public static FbError MultipleRows()
        => new StatusBuilder().Gds(isc_sing_select_err).Build("21000");

    public static FbError UpdateConflict(long otherTx)
        => new StatusBuilder().Gds(isc_deadlock).Gds(isc_update_conflict)
            .Gds(isc_concurrent_transaction).Num((int)otherTx).Build("40001");

    public static FbError UserException(string name, int number, string message)
    {
        var b = new StatusBuilder().Gds(isc_except).Num(number).Gds(isc_random).Str(name);
        if (message.Length > 0) b.Gds(isc_random).Str(message);
        return b.Build("HY000");
    }

    public static FbError ContextVarNotFound(string name, string ns)
        => new StatusBuilder().Gds(isc_ctx_var_not_found).Str(name).Str(ns).Build("HY000");
}
