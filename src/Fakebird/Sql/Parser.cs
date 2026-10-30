using System.Collections.Frozen;
using System.Globalization;
using Fakebird.Types;

namespace Fakebird.Sql;

/// <summary>
/// Recursive descent parser for Firebird 3 SQL (DSQL + PSQL). Fails on the first error with a
/// Firebird-style "Token unknown" error.
/// </summary>
public sealed partial class Parser
{
    readonly string _sql;
    readonly List<Token> _t;
    int _i;
    int _paramCount;

    public int ParamCount => _paramCount;

    public Parser(string sql)
    {
        _sql = sql;
        _t = Lexer.Tokenize(sql);
    }

    /// <summary>Parses a single statement (a trailing ';' is tolerated).</summary>
    public static Stmt ParseStatement(string sql, out int paramCount)
    {
        var p = new Parser(sql);
        var s = p.Statement();
        while (p.AcceptOp(";")) { }
        if (p.Cur.Kind != Tok.Eof) throw p.Unexpected();
        paramCount = p._paramCount;
        return s;
    }

    // ------------------------------------------------------------------ stored sources (backups)

    /// <summary>A column/domain/parameter default as stored: "DEFAULT x" (or "= x" for parameters).</summary>
    internal static Expr ParseDefaultSource(string source, bool parameter)
    {
        var p = new Parser(source);
        if (!p.Accept("DEFAULT")) p.ExpectOp("=");
        return p.End(parameter ? p.Expression() : p.DefaultValue());
    }

    /// <summary>A COMPUTED BY or CHECK expression as stored, with or without the CHECK keyword.</summary>
    internal static Expr ParseExprSource(string source)
    {
        var p = new Parser(source);
        p.Accept("CHECK");
        return p.End(p.Expression());
    }

    internal static SelectQuery ParseViewSource(string source)
    {
        var p = new Parser(source);
        return p.End(p.SelectQueryExpr());
    }

    /// <summary>A routine or trigger body as stored (from AS on for triggers), padded so its positions are
    /// those of the CREATE statement it came from.</summary>
    internal static BlockStmt ParseBodySource(string source, int line, int col)
    {
        var p = new Parser(new string('\n', Math.Max(line - 1, 0)) + new string(' ', Math.Max(col - 1, 0)) + source);
        p.Accept("AS");
        return p.End(p.PsqlBody());
    }

    T End<T>(T node)
    {
        while (AcceptOp(";")) { }
        if (Cur.Kind != Tok.Eof) throw Unexpected();
        return node;
    }

    // ------------------------------------------------------------------ token helpers

    Token Cur => _t[_i];

    /// <summary>Sets a node's source position from the token that starts at <paramref name="pos"/>.</summary>
    T At<T>(T node, int pos) where T : class
    {
        var lo = 0;
        var hi = _t.Count - 1;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (_t[mid].Pos < pos) lo = mid + 1; else hi = mid;
        }
        var tok = _t[lo];
        switch (node)
        {
            case Expr e: (e.Pos, e.Line, e.Col) = (pos, tok.Line, tok.Col); break;
            case TableRef r: (r.Pos, r.Line, r.Col) = (pos, tok.Line, tok.Col); break;
        }
        return node;
    }
    Token PeekTok(int n = 1) => _t[Math.Min(_i + n, _t.Count - 1)];
    Token Advance() => _t[_i++];

    bool Is(string kw) => Cur.Kind == Tok.Ident && Cur.Text == kw;
    bool IsAt(int n, string kw) { var t = PeekTok(n); return t.Kind == Tok.Ident && t.Text == kw; }
    bool IsOp(string op) => Cur.Kind == Tok.Op && Cur.Text == op;

    bool Accept(string kw)
    {
        if (!Is(kw)) return false;
        _i++;
        return true;
    }

    bool Accept(string kw1, string kw2)
    {
        if (!Is(kw1) || !IsAt(1, kw2)) return false;
        _i += 2;
        return true;
    }

    void Expect(string kw)
    {
        if (!Accept(kw)) throw Unexpected();
    }

    bool AcceptOp(string op)
    {
        if (!IsOp(op)) return false;
        _i++;
        return true;
    }

    void ExpectOp(string op)
    {
        if (!AcceptOp(op)) throw Unexpected();
    }

    /// <summary>A data type the parser rejects: [dsql_error, sqlerr -842, code, ..., At line L, column C].</summary>
    static FbError TypeSpecError(Token at, int code, params int[] args)
    {
        var b = new StatusBuilder().Gds(Err.isc_dsql_error).Gds(Err.isc_sqlerr).Num(-842).Gds(code);
        foreach (var a in args) b.Num(a);
        return b.LineCol(at.Line, at.Col).Build();
    }

    FbError Unexpected()
    {
        var t = Cur;
        if (t.Kind == Tok.Eof)
            return new StatusBuilder().Gds(Err.isc_dsql_error).Gds(Err.isc_sqlerr).Num(-104)
                .Gds(FbMessages.command_end_err2).Num(t.Line).Num(Math.Max(1, t.Col - 1)).Build();
        // the token as written (keywords and identifiers are upper-cased by the lexer)
        var text = t.Kind == Tok.Ident && t.Pos + t.Text.Length <= _sql.Length && _sql[t.Pos] != '"'
            && string.Equals(_sql.Substring(t.Pos, t.Text.Length), t.Text, StringComparison.OrdinalIgnoreCase)
            ? _sql.Substring(t.Pos, t.Text.Length) : t.Text;
        return Err.Syntax(t.Line, t.Col, t.Kind == Tok.String ? "'" + text + "'" : text);
    }

    /// <summary>Identifier (unquoted non-reserved word or quoted).</summary>
    string Ident()
    {
        var t = Cur;
        if (t.Kind == Tok.QuotedIdent || (t.Kind == Tok.Ident && !Reserved.Contains(t.Text)))
        {
            _i++;
            return t.Text;
        }
        throw Unexpected();
    }

    /// <summary>Identifier allowing reserved words (used after '.' and in a few unambiguous places).</summary>
    string AnyIdent()
    {
        var t = Cur;
        if (t.Kind is Tok.QuotedIdent or Tok.Ident)
        {
            _i++;
            return t.Text;
        }
        throw Unexpected();
    }


    bool IsIdentStart => Cur.Kind == Tok.QuotedIdent || (Cur.Kind == Tok.Ident && !Reserved.Contains(Cur.Text));

    bool IsAliasStart => Cur.Kind == Tok.QuotedIdent || (Cur.Kind == Tok.Ident && !Reserved.Contains(Cur.Text) && !AliasStop.Contains(Cur.Text));

    string? OptAlias()
    {
        if (Accept("AS")) return Ident();
        if (IsAliasStart) return Ident();
        return null;
    }

    List<string> IdentList()
    {
        var l = new List<string> { Ident() };
        while (AcceptOp(",")) l.Add(Ident());
        return l;
    }

    /// <summary>A column list of INSERT/UPDATE OR INSERT/MERGE: columns may be qualified (t.col).</summary>
    List<string> ParenColumnList(List<(int Line, int Col)>? positions = null)
    {
        ExpectOp("(");
        var l = new List<string>();
        do
        {
            positions?.Add((Cur.Line, Cur.Col));
            var name = Ident();
            while (AcceptOp(".")) name = Ident();
            l.Add(name);
        } while (AcceptOp(","));
        ExpectOp(")");
        return l;
    }

    List<string> ParenIdentList()
    {
        ExpectOp("(");
        var l = IdentList();
        ExpectOp(")");
        return l;
    }

    string SourceFrom(int startTokenIndex) => SourceRange(startTokenIndex, _i);

    string SourceRange(int fromTok, int toTokExclusive)
    {
        var start = _t[fromTok].Pos;
        var endTok = _t[Math.Max(toTokExclusive - 1, fromTok)];
        var end = endTok.Kind == Tok.Eof ? _sql.Length : endTok.Pos + TokenLength(endTok);
        return _sql[start..Math.Min(end, _sql.Length)];
    }

    int TokenLength(Token t)
    {
        // find the end of the token in the raw text: next token start, trimmed
        var idx = _t.IndexOf(t);
        if (idx + 1 < _t.Count)
        {
            var next = _t[idx + 1].Pos;
            var s = _sql.AsSpan(t.Pos, Math.Max(0, next - t.Pos)).TrimEnd();
            return s.Length;
        }
        return _sql.Length - t.Pos;
    }

    long IntLiteral()
    {
        var neg = AcceptOp("-");
        if (!neg) AcceptOp("+");
        if (Cur.Kind != Tok.Integer) throw Unexpected();
        var v = long.Parse(Advance().Text, CultureInfo.InvariantCulture);
        return neg ? -v : v;
    }

    string StringLiteral()
    {
        if (Cur.Kind != Tok.String) throw Unexpected();
        return Advance().Text;
    }

    // ------------------------------------------------------------------ reserved words

    /// <summary>Firebird 5's reserved words (src/common/ParserTokens.h, the tokens that are not non-reserved).</summary>
    static readonly FrozenSet<string> Reserved = new[]
    {
        "ADD", "ADMIN", "ALL", "ALTER", "AND", "ANY", "AS", "AT", "AVG", "BEGIN", "BETWEEN", "BIGINT", "BINARY",
        "BIT_LENGTH", "BLOB", "BOOLEAN", "BOTH", "BY", "CASE", "CAST", "CHAR", "CHAR_LENGTH", "CHARACTER",
        "CHARACTER_LENGTH", "CHECK", "CLOSE", "COLLATE", "COLUMN", "COMMENT", "COMMIT", "CONNECT", "CONSTRAINT",
        "CORR", "COUNT", "COVAR_POP", "COVAR_SAMP", "CREATE", "CROSS", "CURRENT", "CURRENT_CONNECTION",
        "CURRENT_DATE", "CURRENT_ROLE", "CURRENT_TIME", "CURRENT_TIMESTAMP", "CURRENT_TRANSACTION", "CURRENT_USER",
        "CURSOR", "DATE", "DAY", "DEC", "DECFLOAT", "DECIMAL", "DECLARE", "DEFAULT", "DELETE", "DELETING",
        "DETERMINISTIC", "DISCONNECT", "DISTINCT", "DOUBLE", "DROP", "ELSE", "END", "ESCAPE", "EXECUTE", "EXISTS",
        "EXTERNAL", "EXTRACT", "FALSE", "FETCH", "FILTER", "FLOAT", "FOR", "FOREIGN", "FROM", "FULL", "FUNCTION",
        "GDSCODE", "GLOBAL", "GRANT", "GROUP", "HAVING", "HOUR", "IN", "INDEX", "INNER", "INSENSITIVE", "INSERT",
        "INSERTING", "INT", "INT128", "INTEGER", "INTO", "IS", "JOIN", "LEADING", "LEFT", "LIKE", "LATERAL", "LOCAL",
        "LOCALTIME", "LOCALTIMESTAMP", "LONG", "LOWER", "MAX", "MERGE", "MIN", "MINUTE", "MONTH", "NATIONAL",
        "NATURAL", "NCHAR", "NO", "NOT", "NULL", "NUMERIC", "OCTET_LENGTH", "OF", "OFFSET", "ON", "ONLY", "OPEN",
        "OR", "ORDER", "OUTER", "OVER", "PARAMETER", "PLAN", "POSITION", "POST_EVENT", "PRECISION", "PRIMARY",
        "PROCEDURE", "PUBLICATION", "RDB$DB_KEY", "RDB$ERROR", "RDB$GET_CONTEXT", "RDB$GET_TRANSACTION_CN",
        "RDB$RECORD_VERSION", "RDB$RESET_CONTEXT", "RDB$ROLE_IN_USE", "RDB$SET_CONTEXT", "RDB$SYSTEM_PRIVILEGE",
        "REAL", "RECORD_VERSION", "RECREATE", "RECURSIVE", "REFERENCES", "REGR_AVGX", "REGR_AVGY", "REGR_COUNT",
        "REGR_INTERCEPT", "REGR_R2", "REGR_SLOPE", "REGR_SXX", "REGR_SXY", "REGR_SYY", "RELEASE", "RESETTING",
        "RETURN", "RETURNING_VALUES", "RETURNS", "REVOKE", "RIGHT", "ROLLBACK", "ROW", "ROW_COUNT", "ROWS",
        "SAVEPOINT", "SCHEMA", "SCROLL", "SECOND", "SELECT", "SENSITIVE", "SET", "SIMILAR", "SMALLINT", "SOME",
        "SQLCODE", "SQLSTATE", "START", "STDDEV_POP", "STDDEV_SAMP", "SUM", "TABLE", "THEN", "TIME", "TIMESTAMP",
        "TIMEZONE_HOUR", "TIMEZONE_MINUTE", "TO", "TRAILING", "TRIGGER", "TRIM", "TRUE", "UNBOUNDED", "UNION",
        "UNIQUE", "UNKNOWN", "UPDATE", "UPDATING", "UPPER", "USER", "USING", "VALUE", "VALUES", "VAR_POP",
        "VAR_SAMP", "VARBINARY", "VARCHAR", "VARIABLE", "VARYING", "VIEW", "WHEN", "WHERE", "WHILE", "WINDOW",
        "WITH", "WITHOUT", "YEAR",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Non-reserved words that cannot start an implicit alias in this parser.</summary>
    static readonly FrozenSet<string> AliasStop = new[]
    {
        "RETURNING", "MATCHING", "SKIP", "WINDOW", "LOCK", "DO", "INNER", "LEFT", "RIGHT", "FULL", "CROSS",
        "NATURAL", "JOIN", "UNION", "ORDER", "GROUP", "WHERE", "HAVING", "PLAN", "ROWS", "OFFSET", "FETCH",
    }.ToFrozenSet(StringComparer.Ordinal);

    // ======================================================================== expressions

    public Expr Expression() => OrExpr();

    Expr OrExpr()
    {
        var l = AndExpr();
        while (Is("OR"))
        {
            var pos = Advance().Pos;
            l = new BinaryExpr(BinaryOp.Or, l, AndExpr()) { Pos = pos };
        }
        return l;
    }

    Expr AndExpr()
    {
        var l = NotExpr();
        while (Is("AND"))
        {
            var pos = Advance().Pos;
            l = new BinaryExpr(BinaryOp.And, l, NotExpr()) { Pos = pos };
        }
        return l;
    }

    Expr NotExpr()
    {
        if (Is("NOT"))
        {
            var pos = Advance().Pos;
            return new UnaryExpr(UnaryOp.Not, NotExpr()) { Pos = pos };
        }
        return Predicate();
    }

    static BinaryOp? CompOp(Token t) => t.Kind != Tok.Op ? null : t.Text switch
    {
        "=" => BinaryOp.Eq,
        "<>" => BinaryOp.Ne,
        "<" => BinaryOp.Lt,
        "<=" => BinaryOp.Le,
        ">" => BinaryOp.Gt,
        ">=" => BinaryOp.Ge,
        _ => null,
    };

    Expr Predicate()
    {
        var left = Additive();
        while (true)
        {
            var pos = Cur.Pos;
            var op = CompOp(Cur);
            if (op != null)
            {
                _i++;
                if (Is("ANY") || Is("SOME") || Is("ALL"))
                {
                    var q = Advance().Text == "ALL" ? Quantifier.All : Quantifier.Any;
                    ExpectOp("(");
                    var query = SelectQueryExpr();
                    ExpectOp(")");
                    left = new QuantifiedExpr(op.Value, left, q, query) { Pos = pos };
                }
                else
                    left = new BinaryExpr(op.Value, left, Additive()) { Pos = pos };
                continue;
            }
            if (Is("IS"))
            {
                _i++;
                var not = Accept("NOT");
                if (Accept("NULL")) left = new IsNullExpr(left, not) { Pos = pos };
                else if (Accept("DISTINCT"))
                {
                    Expect("FROM");
                    left = new IsDistinctExpr(left, Additive(), not) { Pos = pos };
                }
                else if (Accept("TRUE")) left = new IsBoolExpr(left, true, not) { Pos = pos };
                else if (Accept("FALSE")) left = new IsBoolExpr(left, false, not) { Pos = pos };
                else if (Accept("UNKNOWN")) left = new IsBoolExpr(left, null, not) { Pos = pos };
                else throw Unexpected();
                continue;
            }
            var negated = false;
            if (Is("NOT") && (IsAt(1, "BETWEEN") || IsAt(1, "LIKE") || IsAt(1, "IN") || IsAt(1, "STARTING") || IsAt(1, "STARTS")
                              || IsAt(1, "CONTAINING") || IsAt(1, "SIMILAR")))
            {
                _i++;
                negated = true;
            }
            if (Accept("BETWEEN"))
            {
                var lo = Additive();
                Expect("AND");
                var hi = Additive();
                left = new BetweenExpr(left, lo, hi, negated) { Pos = pos };
            }
            else if (Accept("LIKE"))
            {
                var pat = Additive();
                Expr? esc = Accept("ESCAPE") ? Additive() : null;
                left = new LikeExpr(LikeKind.Like, left, pat, esc, negated) { Pos = pos };
            }
            else if (Accept("STARTING") || Accept("STARTS")) // STARTS [WITH]: undocumented synonym accepted by Firebird 3
            {
                Accept("WITH");
                left = new LikeExpr(LikeKind.StartingWith, left, Additive(), null, negated) { Pos = pos };
            }
            else if (Accept("CONTAINING"))
                left = new LikeExpr(LikeKind.Containing, left, Additive(), null, negated) { Pos = pos };
            else if (Accept("SIMILAR"))
            {
                Expect("TO");
                var pat = Additive();
                Expr? esc = Accept("ESCAPE") ? Additive() : null;
                left = new LikeExpr(LikeKind.SimilarTo, left, pat, esc, negated) { Pos = pos };
            }
            else if (Accept("IN"))
            {
                ExpectOp("(");
                if (Is("SELECT") || Is("WITH"))
                {
                    var q = SelectQueryExpr();
                    ExpectOp(")");
                    left = new InQueryExpr(left, q, negated) { Pos = pos };
                }
                else
                {
                    var list = new List<Expr> { Expression() };
                    while (AcceptOp(",")) list.Add(Expression());
                    ExpectOp(")");
                    left = new InListExpr(left, list, negated) { Pos = pos };
                }
            }
            else if (negated) throw Unexpected();
            else return left;
        }
    }

    Expr Additive()
    {
        var l = Multiplicative();
        while (true)
        {
            var pos = Cur.Pos;
            if (AcceptOp("+")) l = new BinaryExpr(BinaryOp.Add, l, Multiplicative()) { Pos = pos };
            else if (AcceptOp("-")) l = new BinaryExpr(BinaryOp.Sub, l, Multiplicative()) { Pos = pos };
            else return l;
        }
    }

    Expr Multiplicative()
    {
        var l = Concat();
        while (true)
        {
            var pos = Cur.Pos;
            if (AcceptOp("*")) l = new BinaryExpr(BinaryOp.Mul, l, Concat()) { Pos = pos };
            else if (AcceptOp("/")) l = new BinaryExpr(BinaryOp.Div, l, Concat()) { Pos = pos };
            else return l;
        }
    }

    Expr Concat()
    {
        var l = Unary();
        while (IsOp("||"))
        {
            var pos = Advance().Pos;
            l = new BinaryExpr(BinaryOp.Concat, l, Unary()) { Pos = pos };
        }
        return l;
    }

    Expr Unary()
    {
        var pos = Cur.Pos;
        if (AcceptOp("-"))
        {
            // fold negative numeric literals so -9223372036854775808 works
            if (Cur.Kind is Tok.Integer or Tok.Decimal or Tok.Float)
            {
                var lit = NumberLiteral(negate: true);
                if (lit is LiteralExpr le) le.Negated = true;
                return Postfix(lit);
            }
            return new UnaryExpr(UnaryOp.Neg, Unary()) { Pos = pos };
        }
        if (AcceptOp("+")) return Unary(); // parse.y: '+' value is the value itself
        return Postfix(Primary());
    }

    Expr Postfix(Expr e)
    {
        while (true)
        {
            if (Is("COLLATE"))
            {
                var pos = Advance().Pos;
                e = new CollateExpr(e, AnyIdent()) { Pos = pos };
            }
            else if (Is("AT") && (IsAt(1, "LOCAL") || IsAt(1, "TIME") && IsAt(2, "ZONE")))
            {
                // AT binds tighter than COLLATE, || and the arithmetic operators
                var pos = Advance().Pos;
                if (Accept("LOCAL")) e = new AtExpr(e, null) { Pos = pos };
                else
                {
                    _i += 2;
                    e = new AtExpr(e, Primary()) { Pos = pos };
                }
            }
            else return e;
        }
    }

    /// <summary>Exact literals beyond BIGINT are INT128 (NUM128/LIMIT64); larger ones would be DECFLOAT.</summary>
    static Expr WideLiteral(string digits, int scale, int pos)
    {
        if (Int128.TryParse(digits, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var w))
            return new LiteralExpr(Value.Wide(w, scale), TypeRules.Exact(TypeKind.Int128, scale, 0)) { Pos = pos };
        throw Err.Unsupported("DECFLOAT literal " + digits);
    }

    /// <summary>
    /// A string literal is CHAR of its length in the connection charset (UTF8). An introducer relabels the literal's
    /// bytes: _OCTETS gives binary, a single-byte charset reads each UTF-8 byte as one character.
    /// </summary>
    static Expr StringLiteralExpr(string text, string? introducer, int pos)
    {
        var cs = introducer == null ? Charset.Utf8 : Compile.Compiler.TryCharsetOf(introducer) ?? Charset.None;
        switch (cs)
        {
            case Charset.Octets:
            {
                var b = Conv.Utf8.GetBytes(text);
                return new LiteralExpr(Value.Bytes(b), new SqlType(TypeKind.Char, b.Length, charset: Charset.Octets)) { Pos = pos };
            }
            case Charset.Utf8 or Charset.UnicodeFss:
                return new LiteralExpr(Value.Str(text), SqlType.Char(Conv.CharLength(text), cs)) { Pos = pos };
            default:
            {
                var bytes = Conv.Utf8.GetBytes(text);
                var relabeled = Charsets.IsNarrow(cs) ? Charsets.Decode(bytes, cs) : System.Text.Encoding.Latin1.GetString(bytes);
                return new LiteralExpr(Value.Str(relabeled), SqlType.Char(relabeled.Length, cs)) { Pos = pos };
            }
        }
    }

    Expr NumberLiteral(bool negate)
    {
        var t = Advance();
        var text = negate ? "-" + t.Text : t.Text;
        switch (t.Kind)
        {
            case Tok.Integer:
            {
                if (long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var l))
                {
                    var type = l is >= int.MinValue and <= int.MaxValue ? SqlType.Integer : SqlType.BigInt;
                    return new LiteralExpr(Value.Int(l), type) { Pos = t.Pos };
                }
                return WideLiteral(text, 0, t.Pos);
            }
            case Tok.Decimal:
            {
                var dot = text.IndexOf('.');
                var scale = dot < 0 ? 0 : text.Length - dot - 1;
                var digits = dot < 0 ? text : text.Remove(dot, 1);
                if (long.TryParse(digits, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var m))
                    // Firebird makes every literal with a decimal point a scaled BIGINT (SCALEDINT)
                    return new LiteralExpr(Value.Int(m, scale), SqlType.ExactExpr(TypeKind.BigInt, scale)) { Pos = t.Pos };
                return WideLiteral(digits, scale, t.Pos);
            }
            default:
                // the engine converts approximate literals from their text like any string (CVT_get_double)
                return new LiteralExpr(Value.Dbl(Cvt.StringToDouble(text)), SqlType.Double) { Pos = t.Pos };
        }
    }

    static readonly FrozenSet<string> ContextVars = new[]
    {
        "CURRENT_DATE", "CURRENT_TIME", "CURRENT_TIMESTAMP", "LOCALTIME", "LOCALTIMESTAMP", "CURRENT_USER",
        "CURRENT_ROLE", "CURRENT_CONNECTION", "CURRENT_TRANSACTION", "USER", "ROW_COUNT", "SQLCODE", "GDSCODE",
        "SQLSTATE", "INSERTING", "UPDATING", "DELETING",
    }.ToFrozenSet(StringComparer.Ordinal);

    Expr Primary()
    {
        var t = Cur;
        var pos = t.Pos;
        switch (t.Kind)
        {
            case Tok.Integer:
            case Tok.Decimal:
            case Tok.Float:
                return NumberLiteral(negate: false);
            case Tok.String:
            {
                _i++;
                var text = t.Text;
                // adjacent string literals are one literal ('a' 'b' is 'ab')
                while (Cur.Kind == Tok.String && Cur.Introducer == null) text += Advance().Text;
                return StringLiteralExpr(text, t.Introducer, pos);
            }
            case Tok.HexString:
            {
                // x'..' needs an even number of hex digits, else the string is an unknown token
                if (t.Text.Length % 2 == 1 || !t.Text.All(Uri.IsHexDigit)) throw Err.Syntax(t.Line, t.Col + 1, "'" + t.Text + "'");
                _i++;
                var bytes = Convert.FromHexString(t.Text);
                return new LiteralExpr(Value.Bytes(bytes), new SqlType(TypeKind.Char, bytes.Length, charset: Charset.Octets)) { Pos = pos };
            }
            case Tok.Param:
                _i++;
                return new ParamExpr(_paramCount++) { Pos = pos };
            case Tok.Colon:
            {
                _i++;
                var name = AnyIdent();
                if (AcceptOp("."))
                {
                    // :NEW.FIELD
                    var field = AnyIdent();
                    return At(new ColumnRefExpr(name, field), pos);
                }
                return new VariableExpr(name) { Pos = pos };
            }
            case Tok.Op when t.Text == "(":
            {
                _i++;
                if (Is("SELECT") || Is("WITH"))
                {
                    var q = SelectQueryExpr();
                    ExpectOp(")");
                    return new SubqueryExpr(q) { Pos = pos };
                }
                var e = Expression();
                ExpectOp(")");
                return e;
            }
            case Tok.QuotedIdent:
                return IdentifierExpr();
            case Tok.Ident:
                break;
            default:
                throw Unexpected();
        }

        // keywords
        switch (t.Text)
        {
            case "NULL": _i++; return new LiteralExpr(Value.Null, SqlType.Null) { Pos = pos };
            case "TRUE": _i++; return new LiteralExpr(Value.True, SqlType.Boolean) { Pos = pos };
            case "FALSE": _i++; return new LiteralExpr(Value.False, SqlType.Boolean) { Pos = pos };
            case "UNKNOWN": _i++; return new LiteralExpr(Value.Null, SqlType.Boolean) { Pos = pos };
            case "DATE" when PeekTok().Kind == Tok.String:
                _i++;
                // literals do not accept NOW/TODAY/TOMORROW/YESTERDAY (allow_special = false)
                return new LiteralExpr(Value.Date(Cvt.StringToDateTime(StringLiteral(), Cvt.Expect.Date, allowSpecial: false)), SqlType.Date) { Pos = pos };
            // a TIME or TIMESTAMP literal that names a time zone is WITH TIME ZONE (MAKE_constant)
            case "TIME" when PeekTok().Kind == Tok.String:
            {
                _i++;
                var str = StringLiteral();
                var utc = Cvt.StringToDateTime(str, Cvt.Expect.TimeTz, out var zone, out var hasZone, allowSpecial: false);
                return hasZone
                    ? new LiteralExpr(Value.TimeTz(utc, zone), SqlType.TimeTz) { Pos = pos }
                    : new LiteralExpr(Value.Time(Cvt.StringToDateTime(str, Cvt.Expect.Time, allowSpecial: false)), SqlType.Time) { Pos = pos };
            }
            case "TIMESTAMP" when PeekTok().Kind == Tok.String:
            {
                _i++;
                var str = StringLiteral();
                var utc = Cvt.StringToDateTime(str, Cvt.Expect.TimestampTz, out var zone, out var hasZone, allowSpecial: false);
                return hasZone
                    ? new LiteralExpr(Value.TimestampTz(utc, zone), SqlType.TimestampTz) { Pos = pos }
                    : new LiteralExpr(Value.Timestamp(Cvt.StringToDateTime(str, Cvt.Expect.Timestamp, allowSpecial: false)), SqlType.Timestamp) { Pos = pos };
            }
            case "CASE": return CaseExpression();
            case "CAST":
            {
                _i++;
                ExpectOp("(");
                var e = Expression();
                Expect("AS");
                var type = DataType(allowDomain: true);
                ExpectOp(")");
                return new CastExpr(e, type) { Pos = pos };
            }
            case "EXISTS":
            case "SINGULAR":
            {
                _i++;
                ExpectOp("(");
                var q = SelectQueryExpr();
                ExpectOp(")");
                return new ExistsExpr(q, t.Text == "SINGULAR") { Pos = pos };
            }
            case "NEXT" when IsAt(1, "VALUE"):
            {
                _i += 2;
                Expect("FOR");
                return new NextValueExpr(Ident()) { Pos = pos };
            }
            case "EXTRACT":
            {
                _i++;
                ExpectOp("(");
                var part = new KeywordArg(AnyIdent()) { Pos = pos };
                Expect("FROM");
                var e = Expression();
                ExpectOp(")");
                return new FuncExpr("EXTRACT", [part, e]) { Pos = pos };
            }
            case "FIRST_DAY" or "LAST_DAY" when PeekTok().IsOp("("):
            {
                var fn = Advance().Text;
                ExpectOp("(");
                Expect("OF");
                var part = new KeywordArg(AnyIdent()) { Pos = pos };
                if (part.Keyword is not ("YEAR" or "QUARTER" or "MONTH" or "WEEK")) throw Unexpected();
                Expect("FROM");
                var e = Expression();
                ExpectOp(")");
                return new FuncExpr(fn, [part, e]) { Pos = pos };
            }
            case "SUBSTRING":
            {
                _i++;
                ExpectOp("(");
                var save = _i;
                var e = Additive();
                if (Accept("SIMILAR"))
                {
                    // SUBSTRING(value SIMILAR pattern ESCAPE char)
                    var pattern = Additive();
                    Expect("ESCAPE");
                    var escape = Additive();
                    ExpectOp(")");
                    return new FuncExpr("SUBSTRING_SIMILAR", [e, pattern, escape]) { Pos = pos };
                }
                _i = save;
                e = Expression();
                List<Expr> args = [e];
                if (Accept("FROM"))
                {
                    args.Add(Expression());
                    if (Accept("FOR")) args.Add(Expression());
                }
                else
                {
                    ExpectOp(",");
                    args.Add(Expression());
                    if (AcceptOp(",")) args.Add(Expression());
                }
                ExpectOp(")");
                return new FuncExpr("SUBSTRING", args) { Pos = pos };
            }
            case "TRIM":
            {
                _i++;
                ExpectOp("(");
                var spec = "BOTH";
                Expr? chars = null;
                Expr source;
                if (Is("LEADING") || Is("TRAILING") || Is("BOTH"))
                {
                    spec = Advance().Text;
                    if (!Accept("FROM"))
                    {
                        chars = Expression();
                        Expect("FROM");
                    }
                    source = Expression();
                }
                else
                {
                    var first = Expression();
                    if (Accept("FROM"))
                    {
                        chars = first;
                        source = Expression();
                    }
                    else source = first;
                }
                ExpectOp(")");
                // without trim characters TRIM removes spaces; an explicit NULL makes the result NULL
                return new FuncExpr("TRIM", [new KeywordArg(spec), chars ?? new KeywordArg(" "), source]) { Pos = pos };
            }
            case "POSITION":
            {
                _i++;
                ExpectOp("(");
                var a = Additive();
                List<Expr> args = [a];
                if (Accept("IN")) args.Add(Expression());
                else
                {
                    ExpectOp(",");
                    args.Add(Expression());
                    if (AcceptOp(",")) args.Add(Expression());
                }
                ExpectOp(")");
                return new FuncExpr("POSITION", args) { Pos = pos };
            }
            case "OVERLAY":
            {
                _i++;
                ExpectOp("(");
                var a = Expression();
                Expect("PLACING");
                var b = Expression();
                Expect("FROM");
                var c = Expression();
                List<Expr> args = [a, b, c];
                if (Accept("FOR")) args.Add(Expression());
                ExpectOp(")");
                return new FuncExpr("OVERLAY", args) { Pos = pos };
            }
            case "DATEADD":
            {
                _i++;
                ExpectOp("(");
                Expr n;
                KeywordArg part;
                Expr d;
                if (IsDatePart(Cur) && PeekTok().IsOp(","))
                {
                    part = new KeywordArg(Advance().Text);
                    ExpectOp(",");
                    n = Expression();
                    ExpectOp(",");
                    d = Expression();
                }
                else
                {
                    n = Additive();
                    part = new KeywordArg(AnyIdent());
                    Expect("TO");
                    d = Expression();
                }
                ExpectOp(")");
                return new FuncExpr("DATEADD", [part, n, d]) { Pos = pos };
            }
            case "DATEDIFF":
            {
                _i++;
                ExpectOp("(");
                var part = new KeywordArg(AnyIdent());
                Expr a, b;
                if (Accept("FROM"))
                {
                    a = Expression();
                    Expect("TO");
                    b = Expression();
                }
                else
                {
                    ExpectOp(",");
                    a = Expression();
                    ExpectOp(",");
                    b = Expression();
                }
                ExpectOp(")");
                return new FuncExpr("DATEDIFF", [part, a, b]) { Pos = pos };
            }
        }
        if (ContextVars.Contains(t.Text))
        {
            _i++;
            var prec = -1;
            if ((t.Text is "CURRENT_TIME" or "CURRENT_TIMESTAMP" or "LOCALTIME" or "LOCALTIMESTAMP") && IsOp("("))
            {
                _i++;
                prec = (int)IntLiteral();
                ExpectOp(")");
            }
            return new ContextVarExpr(t.Text, prec) { Pos = pos };
        }
        return IdentifierExpr();
    }

    static bool IsDatePart(Token t) => t.Kind == Tok.Ident && t.Text is "YEAR" or "MONTH" or "DAY" or "HOUR"
        or "MINUTE" or "SECOND" or "MILLISECOND" or "WEEK" or "WEEKDAY" or "YEARDAY";

    Expr IdentifierExpr()
    {
        var pos = Cur.Pos;
        var name = AnyIdent();
        if (IsOp("("))
            return FunctionCall(name, null, pos);
        if (AcceptOp("."))
        {
            if (AcceptOp("*")) return new StarExpr(name) { Pos = pos };
            var second = AnyIdent();
            if (IsOp("("))
                return FunctionCall(second, name, pos); // package.function
            return At(new ColumnRefExpr(name, second), pos);
        }
        if (Reserved.Contains(name) && _t[_i - 1].Kind == Tok.Ident && name is not ("VALUE" or "RDB$DB_KEY"))
        {
            _i--;
            throw Unexpected();
        }
        return At(new ColumnRefExpr(null, name), pos);
    }

    Expr FunctionCall(string name, string? package, int pos)
    {
        ExpectOp("(");
        var f = new FuncExpr(name, new List<Expr>()) { Pos = pos, Package = package };
        if (package == null && RankingFunctions.Contains(name) && !IsOp(")")) throw Unexpected();
        // NTILE takes a number, a parameter or a variable
        if (package == null && name == "NTILE" && Cur.Kind is not (Tok.Integer or Tok.Param or Tok.Colon or Tok.Ident or Tok.QuotedIdent)) throw Unexpected();
        if (package == null && name == "NTILE" && Is("NULL")) throw Unexpected();
        // aggregates have their own grammar rules: one argument (LIST: an optional delimiter), * only in COUNT
        var aggregate = package == null && name is "COUNT" or "SUM" or "AVG" or "MIN" or "MAX" or "LIST";
        if (aggregate && (IsOp(")") || IsOp("*") && name != "COUNT")) throw Unexpected();
        if (AcceptOp(")")) return Over(f);
        if (AcceptOp("*"))
        {
            f.Star = true;
            ExpectOp(")");
            return Over(f);
        }
        if (Accept("DISTINCT")) f.Distinct = true;
        else Accept("ALL");
        if (name == "GEN_ID")
        {
            f.Args.Add(At(new ColumnRefExpr(null, AnyIdent()), pos));
            ExpectOp(",");
            f.Args.Add(Expression());
            ExpectOp(")");
            return f;
        }
        f.Args.Add(Expression());
        // HASH(x USING alg), CRYPT_HASH(x USING alg): the algorithm name becomes a string argument
        if (package == null && name is "HASH" or "CRYPT_HASH" && Accept("USING"))
        {
            var alg = AnyIdent();
            f.Args.Add(new LiteralExpr(Types.Value.Str(alg), Types.SqlType.VarChar(alg.Length, Types.Charset.Ascii)));
        }
        while (IsOp(","))
        {
            if (aggregate && (name != "LIST" || f.Args.Count == 2)) throw Unexpected();
            _i++;
            f.Args.Add(Expression());
        }
        ExpectOp(")");
        return Over(f);
    }

    static readonly HashSet<string> RankingFunctions = ["ROW_NUMBER", "RANK", "DENSE_RANK", "PERCENT_RANK", "CUME_DIST"];

    Expr Over(FuncExpr f)
    {
        if (f.Name == "NTH_VALUE" && f.Package == null && Accept("FROM"))
        {
            if (Accept("LAST")) f.FromLast = true;
            else Expect("FIRST");
        }
        // ranking functions only exist as window functions
        if (f.Package == null && (RankingFunctions.Contains(f.Name) || f.Name == "NTILE") && !Is("OVER")) throw Unexpected();
        if (!Accept("OVER")) return f;
        if (IsIdentStart && !IsOp("("))
            return new WindowExpr(f, new WindowSpec { BaseName = Ident() }) { Pos = f.Pos };
        ExpectOp("(");
        var w = WindowBody();
        ExpectOp(")");
        return new WindowExpr(f, w) { Pos = f.Pos };
    }

    /// <summary>[base window] [PARTITION BY ...] [ORDER BY ...] [ROWS | RANGE frame]</summary>
    internal WindowSpec WindowBody()
    {
        var w = new WindowSpec();
        if (IsIdentStart && !Is("PARTITION") && !Is("ORDER") && !Is("ROWS") && !Is("RANGE")) w.BaseName = Ident();
        if (Accept("PARTITION"))
        {
            Expect("BY");
            w.PartitionBy.Add(Expression());
            while (AcceptOp(",")) w.PartitionBy.Add(Expression());
        }
        if (Accept("ORDER"))
        {
            Expect("BY");
            w.OrderBy = OrderList();
        }
        if (Is("ROWS") || Is("RANGE"))
        {
            var unit = Advance().Text == "ROWS" ? FrameUnit.Rows : FrameUnit.Range;
            FrameBound start, end;
            if (Accept("BETWEEN"))
            {
                start = FrameBoundSpec();
                Expect("AND");
                end = FrameBoundSpec();
            }
            else
            {
                start = FrameBoundSpec();
                end = new FrameBound(FrameBoundKind.CurrentRow, null);
            }
            w.Frame = new FrameSpec(unit, start, end);
        }
        return w;
    }

    FrameBound FrameBoundSpec()
    {
        if (Accept("UNBOUNDED"))
        {
            if (Accept("PRECEDING")) return new FrameBound(FrameBoundKind.UnboundedPreceding, null);
            Expect("FOLLOWING");
            return new FrameBound(FrameBoundKind.UnboundedFollowing, null);
        }
        if (Accept("CURRENT"))
        {
            Expect("ROW");
            return new FrameBound(FrameBoundKind.CurrentRow, null);
        }
        var e = Additive();
        if (Accept("PRECEDING")) return new FrameBound(FrameBoundKind.Preceding, e);
        Expect("FOLLOWING");
        return new FrameBound(FrameBoundKind.Following, e);
    }

    Expr CaseExpression()
    {
        var pos = Advance().Pos;
        Expr? operand = null;
        if (!Is("WHEN")) operand = Expression();
        var whens = new List<(Expr, Expr)>();
        while (Accept("WHEN"))
        {
            var w = Expression();
            Expect("THEN");
            whens.Add((w, Expression()));
        }
        if (whens.Count == 0) throw Unexpected();
        Expr? els = Accept("ELSE") ? Expression() : null;
        Expect("END");
        return new CaseExpr(operand, whens, els) { Pos = pos };
    }

    // ======================================================================== data types

    /// <summary>Parses a data type; with allowDomain also domain names and TYPE OF.</summary>
    TypeSpec DataType(bool allowDomain)
    {
        var ts = new TypeSpec();
        if (allowDomain && Accept("TYPE"))
        {
            Expect("OF");
            if (Accept("COLUMN"))
            {
                ts.TypeOfTable = Ident();
                ExpectOp(".");
                ts.TypeOfColumn = AnyIdent();
            }
            else
            {
                ts.TypeOf = true;
                ts.Domain = Ident();
            }
            return ts;
        }
        var t = Cur;
        if (t.Kind == Tok.QuotedIdent)
        {
            if (!allowDomain) throw Unexpected();
            ts.Domain = Advance().Text;
            return ts;
        }
        if (t.Kind != Tok.Ident) throw Unexpected();
        switch (t.Text)
        {
            case "SMALLINT":
            case "INTEGER":
            case "BIGINT":
            case "BOOLEAN":
            case "DATE":
            case "REAL":
                _i++;
                ts.BaseType = t.Text == "REAL" ? "FLOAT" : t.Text;
                break;
            case "INT":
                _i++;
                ts.BaseType = "INTEGER";
                break;
            case "FLOAT":
                _i++;
                ts.BaseType = "FLOAT";
                if (AcceptOp("("))
                {
                    var p = IntLiteral();
                    ExpectOp(")");
                    if (p > 24) ts.BaseType = "DOUBLE";
                }
                break;
            case "DOUBLE":
                _i++;
                Expect("PRECISION");
                ts.BaseType = "DOUBLE";
                break;
            case "TIME":
            case "TIMESTAMP":
                _i++;
                ts.BaseType = t.Text;
                if (Accept("WITHOUT"))
                {
                    Expect("TIME");
                    Expect("ZONE");
                }
                else if (Is("WITH") && IsAt(1, "TIME") && IsAt(2, "ZONE"))
                {
                    _i += 3;
                    ts.BaseType += " WITH TIME ZONE";
                }
                break;
            case "NUMERIC":
            case "DECIMAL":
            case "DEC":
                _i++;
                ts.BaseType = t.Text == "NUMERIC" ? "NUMERIC" : "DECIMAL";
                if (AcceptOp("("))
                {
                    var pt = Cur;
                    ts.Precision = (int)IntLiteral();
                    if (ts.Precision is < 1 or > 38) throw TypeSpecError(pt, FbMessages.precision_err2, 1, 38);
                    if (AcceptOp(","))
                    {
                        var st = Cur;
                        ts.Scale = (int)IntLiteral();
                        if (ts.Scale > ts.Precision) throw TypeSpecError(st, FbMessages.scale_nogt);
                    }
                    ExpectOp(")");
                }
                break;
            case "CHAR":
            case "CHARACTER":
            case "VARCHAR":
            case "NCHAR":
            case "NATIONAL":
            {
                _i++;
                var varying = t.Text == "VARCHAR";
                if (t.Text == "NATIONAL")
                {
                    if (!Accept("CHARACTER")) Expect("CHAR");
                }
                if (Accept("VARYING")) varying = true;
                ts.BaseType = varying ? "VARCHAR" : "CHAR";
                if (t.Text is "NCHAR" or "NATIONAL") ts.Charset = "ISO8859_1";
                if (AcceptOp("("))
                {
                    var lt = Cur;
                    var len = IntLiteral();
                    // the grammar wants a positive short integer
                    if (len > short.MaxValue) throw TypeSpecError(lt, FbMessages.expec_short);
                    if (len < 1) throw TypeSpecError(lt, FbMessages.expec_positive);
                    ts.Length = (int)len;
                    ExpectOp(")");
                }
                else if (varying) throw Unexpected();
                CharsetClause(ts);
                break;
            }
            case "BLOB":
            {
                _i++;
                ts.BaseType = "BLOB";
                ts.SubType = 0;
                if (AcceptOp("("))
                {
                    if (!IsOp(",")) IntLiteral(); // segment size
                    if (AcceptOp(",")) ts.SubType = (int)IntLiteral();
                    ExpectOp(")");
                }
                while (true)
                {
                    if (Accept("SUB_TYPE"))
                    {
                        if (Cur.Kind == Tok.Integer || IsOp("-")) ts.SubType = (int)IntLiteral();
                        else
                        {
                            var st = AnyIdent();
                            ts.SubType = st switch
                            {
                                "TEXT" => 1,
                                "BINARY" => 0,
                                _ => throw Err.Unsupported("BLOB SUB_TYPE " + st),
                            };
                        }
                    }
                    else if (Accept("SEGMENT"))
                    {
                        Expect("SIZE");
                        IntLiteral();
                    }
                    else if (CharsetClause(ts)) { }
                    else break;
                }
                break;
            }
            default:
                if (!allowDomain) throw Unexpected();
                ts.Domain = Ident();
                break;
        }
        if (ts.BaseType is "CHAR" or "VARCHAR" or "BLOB") { }
        return ts;
    }

    bool CharsetClause(TypeSpec ts)
    {
        if (Is("CHARACTER") && IsAt(1, "SET"))
        {
            _i += 2;
            ts.Charset = AnyIdent();
            return true;
        }
        if (Accept("CHARSET"))
        {
            ts.Charset = AnyIdent();
            return true;
        }
        return false;
    }
}
