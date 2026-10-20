using System.Text;

namespace Fakebird.Sql;

public enum Tok : byte
{
    Eof,
    Ident,        // unquoted identifier or keyword (upper-cased in Text)
    QuotedIdent,  // "Name" (exact case)
    String,       // 'text'
    HexString,    // x'0A0B'
    Integer,      // 123
    Decimal,      // 1.5
    Float,        // 1e5
    Param,        // ?
    Colon,        // :
    Op,           // operators / punctuation (Text holds it)
}

public readonly record struct Token(Tok Kind, string Text, int Pos, int Line, int Col)
{
    /// <summary>Character set introducer of a string literal (_WIN1252 'x'), upper case.</summary>
    public string? Introducer { get; init; }

    public bool Is(string kw) => Kind == Tok.Ident && Text == kw;
    public bool IsOp(string op) => Kind == Tok.Op && Text == op;
    public override string ToString() => Kind == Tok.Eof ? "<end>" : Text;
}

/// <summary>Hand written SQL tokenizer. Unquoted identifiers are upper-cased (dialect 3).</summary>
public sealed class Lexer
{
    readonly string _s;
    int _pos;
    int _line = 1;
    int _lineStart;

    public Lexer(string s) => _s = s;

    public static List<Token> Tokenize(string sql)
    {
        var lx = new Lexer(sql);
        var list = new List<Token>(sql.Length / 4 + 4);
        while (true)
        {
            var t = lx.Next();
            list.Add(t);
            if (t.Kind == Tok.Eof) return list;
        }
    }

    Token Make(Tok kind, string text, int start, int line, int col) => new(kind, text, start, line, col);

    void SkipTrivia()
    {
        while (_pos < _s.Length)
        {
            var c = _s[_pos];
            if (c == '\n') { _pos++; _line++; _lineStart = _pos; }
            else if (char.IsWhiteSpace(c)) _pos++;
            else if (c == '-' && _pos + 1 < _s.Length && _s[_pos + 1] == '-')
            {
                while (_pos < _s.Length && _s[_pos] != '\n') _pos++;
            }
            else if (c == '/' && _pos + 1 < _s.Length && _s[_pos + 1] == '*')
            {
                _pos += 2;
                while (_pos < _s.Length && !(_s[_pos] == '*' && _pos + 1 < _s.Length && _s[_pos + 1] == '/'))
                {
                    if (_s[_pos] == '\n') { _line++; _lineStart = _pos + 1; }
                    _pos++;
                }
                if (_pos >= _s.Length) throw Err.SyntaxMsg("Unterminated comment");
                _pos += 2;
            }
            else break;
        }
    }

    public Token Next()
    {
        SkipTrivia();
        var start = _pos;
        var line = _line;
        var col = _pos - _lineStart + 1;
        if (_pos >= _s.Length) return Make(Tok.Eof, "", start, line, col);
        var c = _s[_pos];

        if (char.IsLetter(c) || c == '_' || c == '$')
        {
            // x'..' hex literal / q'..' alternate quoting
            if ((c is 'x' or 'X') && _pos + 1 < _s.Length && _s[_pos + 1] == '\'')
            {
                _pos += 2;
                var e = _s.IndexOf('\'', _pos);
                if (e < 0) throw Err.SyntaxMsg("Unterminated string");
                var hex = _s[_pos..e];
                _pos = e + 1;
                return Make(Tok.HexString, hex, start, line, col);
            }
            if ((c is 'q' or 'Q') && _pos + 2 < _s.Length && _s[_pos + 1] == '\'')
            {
                var open = _s[_pos + 2];
                var close = open switch { '(' => ')', '[' => ']', '{' => '}', '<' => '>', _ => open };
                var bodyStart = _pos + 3;
                var e = _s.IndexOf(close + "'", bodyStart, StringComparison.Ordinal);
                if (e < 0) throw Err.SyntaxMsg("Unterminated string");
                var text = _s[bodyStart..e];
                CountLines(_pos, e + 2);
                _pos = e + 2;
                return Make(Tok.String, text, start, line, col);
            }
            _pos++;
            while (_pos < _s.Length && (char.IsLetterOrDigit(_s[_pos]) || _s[_pos] == '_' || _s[_pos] == '$')) _pos++;
            // character set introducer: _WIN1252'text'
            var q = _pos;
            while (c == '_' && q < _s.Length && _s[q] is ' ' or '\t') q++;
            if (c == '_' && q < _s.Length && _s[q] == '\'')
            {
                var cs = _s[(start + 1).._pos].ToUpperInvariant();
                _pos = q;
                var lit = Next();
                return lit with { Pos = start, Line = line, Col = col, Introducer = cs };
            }
            // a name cannot start with _ or $ (_X only introduces a character set before a string)
            if (c == '$')
            {
                _pos = start + 1;
                return Make(Tok.Op, "$", start, line, col);
            }
            if (c == '_') return Make(Tok.Op, _s[start.._pos], start, line, col);
            // Firebird 4+ names hold at most 63 characters
            if (_pos - start > 63) throw NameError(FbMessages.dyn_name_longer, line, col);
            return Make(Tok.Ident, _s[start.._pos].ToUpperInvariant(), start, line, col);
        }
        if (c is >= '0' and <= '9' || (c == '.' && _pos + 1 < _s.Length && char.IsDigit(_s[_pos + 1])))
            return Number(start, line, col);
        switch (c)
        {
            case '\'':
            {
                _pos++;
                StringBuilder? sb = null;
                var segStart = _pos;
                while (true)
                {
                    if (_pos >= _s.Length) throw Err.SyntaxMsg("Unterminated string");
                    var ch = _s[_pos];
                    if (ch == '\'')
                    {
                        if (_pos + 1 < _s.Length && _s[_pos + 1] == '\'')
                        {
                            sb ??= new StringBuilder();
                            sb.Append(_s, segStart, _pos - segStart + 1);
                            _pos += 2;
                            segStart = _pos;
                            continue;
                        }
                        break;
                    }
                    if (ch == '\n') { _line++; _lineStart = _pos + 1; }
                    _pos++;
                }
                string text;
                if (sb == null) text = _s[segStart.._pos];
                else { sb.Append(_s, segStart, _pos - segStart); text = sb.ToString(); }
                _pos++;
                return Make(Tok.String, text, start, line, col);
            }
            case '"':
            {
                _pos++;
                var sb = new StringBuilder();
                while (true)
                {
                    if (_pos >= _s.Length) throw Err.SyntaxMsg("Unterminated identifier");
                    var ch = _s[_pos];
                    if (ch == '"')
                    {
                        if (_pos + 1 < _s.Length && _s[_pos + 1] == '"') { sb.Append('"'); _pos += 2; continue; }
                        break;
                    }
                    sb.Append(ch);
                    _pos++;
                }
                _pos++;
                var quoted = sb.ToString();
                if (quoted.Length == 0) throw NameError(FbMessages.dyn_zero_len_id, line, col);
                if (quoted.EnumerateRunes().Count() > 63) throw NameError(FbMessages.dyn_name_longer, line, col);
                return Make(Tok.QuotedIdent, quoted, start, line, col);
            }
            case '?':
                _pos++;
                return Make(Tok.Param, "?", start, line, col);
            case ':':
                _pos++;
                return Make(Tok.Colon, ":", start, line, col);
        }
        // operators
        string op;
        var two = _pos + 1 < _s.Length ? _s.Substring(_pos, 2) : null;
        if (two is "<>" or "!=" or "~=" or "^=" or "<=" or ">=" or "||" or "!<" or "~<" or "^<" or "!>" or "~>" or "^>")
        {
            op = two switch
            {
                "!=" or "~=" or "^=" => "<>",
                "!<" or "~<" or "^<" => ">=",
                "!>" or "~>" or "^>" => "<=",
                _ => two,
            };
            _pos += 2;
        }
        else if ("=<>+-*/(),;.[]".Contains(c))
        {
            op = c.ToString();
            _pos++;
        }
        else throw Err.Syntax(line, col, c.ToString());
        return Make(Tok.Op, op, start, line, col);
    }

    void CountLines(int from, int to)
    {
        for (var i = from; i < to; i++)
            if (_s[i] == '\n') { _line++; _lineStart = i + 1; }
    }

    Token Number(int start, int line, int col)
    {
        var kind = Tok.Integer;
        while (_pos < _s.Length && char.IsDigit(_s[_pos])) _pos++;
        if (_pos < _s.Length && _s[_pos] == '.')
        {
            kind = Tok.Decimal;
            _pos++;
            while (_pos < _s.Length && char.IsDigit(_s[_pos])) _pos++;
        }
        if (_pos < _s.Length && (_s[_pos] == 'e' || _s[_pos] == 'E'))
        {
            var save = _pos;
            _pos++;
            if (_pos < _s.Length && (_s[_pos] == '+' || _s[_pos] == '-')) _pos++;
            if (_pos < _s.Length && char.IsDigit(_s[_pos]))
            {
                kind = Tok.Float;
                while (_pos < _s.Length && char.IsDigit(_s[_pos])) _pos++;
            }
            else _pos = save;
        }
        // 0x hex integer literal
        if (kind == Tok.Integer && _pos - start == 1 && _s[start] == '0' && _pos < _s.Length && (_s[_pos] == 'x' || _s[_pos] == 'X'))
        {
            _pos++;
            var hs = _pos;
            while (_pos < _s.Length && Uri.IsHexDigit(_s[_pos])) _pos++;
            var v = Convert.ToInt64(_s[hs.._pos], 16);
            return Make(Tok.Integer, v.ToString(), start, line, col);
        }
        return Make(kind, _s[start.._pos], start, line, col);
    }

    static FbError NameError(int code, int line, int col)
        => new StatusBuilder().Gds(Err.isc_dsql_error).Gds(Err.isc_sqlerr).Num(-104).Gds(code).LineCol(line, col).Build();
}
