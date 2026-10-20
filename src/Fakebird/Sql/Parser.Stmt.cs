namespace Fakebird.Sql;

public sealed partial class Parser
{
    // ======================================================================== statements

    public Stmt Statement()
    {
        var t = Cur;
        if (t.Kind != Tok.Ident) throw Unexpected();
        switch (t.Text)
        {
            case "SELECT":
            case "WITH":
                return SelectStatement();
            case "INSERT": return Insert();
            case "UPDATE":
                if (IsAt(1, "OR") && IsAt(2, "INSERT")) return UpdateOrInsert();
                return Update();
            case "DELETE": return Delete();
            case "MERGE": return Merge();
            case "EXECUTE":
                if (IsAt(1, "PROCEDURE")) return ExecProcedure();
                if (IsAt(1, "BLOCK")) return ExecBlock();
                if (IsAt(1, "STATEMENT")) return ExecStatement();
                throw Unexpected();
            case "CREATE": return Create();
            case "ALTER": return Alter();
            case "RECREATE":
                _i++;
                return CreateObject(CreateMode.Recreate);
            case "DROP": return Drop();
            case "SET": return SetStatement();
            case "COMMIT":
            {
                _i++;
                Accept("WORK");
                var retain = Accept("RETAIN");
                if (retain) Accept("SNAPSHOT");
                return new CommitStmt { Retain = retain, Pos = t.Pos };
            }
            case "ROLLBACK":
            {
                _i++;
                Accept("WORK");
                var s = new RollbackStmt { Pos = t.Pos };
                if (Accept("RETAIN")) s.Retain = true;
                else if (Accept("TO"))
                {
                    Accept("SAVEPOINT");
                    s.ToSavepoint = Ident();
                }
                return s;
            }
            case "SAVEPOINT":
                _i++;
                return new SavepointStmt { Name = Ident(), Pos = t.Pos };
            case "RELEASE":
            {
                _i++;
                Expect("SAVEPOINT");
                var s = new ReleaseSavepointStmt { Name = Ident(), Pos = t.Pos };
                s.Only = Accept("ONLY");
                return s;
            }
            case "GRANT":
            case "REVOKE":
            case "COMMENT":
                return SkipToEnd(t.Text);
            case "DECLARE":
                if (IsAt(1, "EXTERNAL") || IsAt(1, "FILTER")) return SkipToEnd("DECLARE EXTERNAL");
                throw Unexpected();
        }
        throw Unexpected();
    }

    /// <summary>Consumes the rest of a statement that is accepted and ignored.</summary>
    Stmt SkipToEnd(string what)
    {
        var pos = Cur.Pos;
        while (Cur.Kind != Tok.Eof && !IsOp(";")) _i++;
        return new NoOpStmt { What = what, Pos = pos };
    }

    SelectStmt SelectStatement()
    {
        var pos = Cur.Pos;
        var q = SelectQueryExpr();
        var s = new SelectStmt { Query = q, Pos = pos };
        if (Accept("INTO")) s.Into = TargetList();
        else if (q.Body is QuerySpec qs && qs.Into != null) s.Into = qs.Into;
        return s;
    }

    ReturningClause? Returning()
    {
        if (!Accept("RETURNING")) return null;
        var r = new ReturningClause();
        do
        {
            var item = new SelectItem();
            if (AcceptOp("*")) item.Expr = new StarExpr(null);
            else
            {
                item.Expr = Expression();
                item.Alias = OptAlias();
            }
            r.Items.Add(item);
        } while (AcceptOp(","));
        if (Accept("INTO")) r.Into = TargetList();
        return r;
    }

    InsertStmt Insert()
    {
        var s = new InsertStmt { Pos = Advance().Pos };
        Expect("INTO");
        s.Table = Ident();
        if (IsAliasStart && !Is("DEFAULT") && !Is("VALUES") && !Is("SELECT") && !Is("WITH")) s.Alias = Ident();
        if (IsOp("(") && !(IsAt(1, "SELECT") || IsAt(1, "WITH"))) s.Columns = ParenColumnList(s.ColumnPos);
        if (Accept("OVERRIDING"))
        {
            s.Overriding = AnyIdent();
            Expect("VALUE");
        }
        if (Accept("DEFAULT"))
        {
            Expect("VALUES");
            s.DefaultValues = true;
        }
        else if (Accept("VALUES"))
        {
            ExpectOp("(");
            s.Values = new List<Expr> { ValueOrDefault() };
            while (AcceptOp(",")) s.Values.Add(ValueOrDefault());
            ExpectOp(")");
        }
        else s.Query = SelectQueryExpr();
        s.Returning = Returning();
        return s;
    }

    /// <summary>DEFAULT keyword inside VALUES is represented as KeywordArg("DEFAULT").</summary>
    Expr ValueOrDefault()
    {
        if (Is("DEFAULT")) return new KeywordArg(Advance().Text);
        return Expression();
    }

    List<Assignment> SetList()
    {
        var l = new List<Assignment>();
        do
        {
            var pos = Cur.Pos;
            var name = AnyIdent();
            ColumnRefExpr target;
            if (AcceptOp(".")) target = At(new ColumnRefExpr(name, AnyIdent()), pos);
            else target = At(new ColumnRefExpr(null, name), pos);
            ExpectOp("=");
            l.Add(new Assignment { Target = target, Value = ValueOrDefault() });
        } while (AcceptOp(","));
        return l;
    }

    UpdateStmt Update()
    {
        var s = new UpdateStmt { Pos = Advance().Pos };
        s.Table = Ident();
        if (!Is("SET")) s.Alias = OptAlias();
        Expect("SET");
        s.Set = SetList();
        if (Accept("WHERE"))
        {
            if (Accept("CURRENT"))
            {
                Expect("OF");
                s.CurrentOf = Ident();
            }
            else s.Where = Expression();
        }
        if (Accept("PLAN")) SkipPlan();
        if (Accept("ORDER"))
        {
            Expect("BY");
            s.OrderBy = OrderList();
        }
        if (Accept("ROWS"))
        {
            s.Rows = Additive();
            if (Accept("TO")) s.RowsTo = Additive();
        }
        s.Returning = Returning();
        return s;
    }

    DeleteStmt Delete()
    {
        var s = new DeleteStmt { Pos = Advance().Pos };
        Expect("FROM");
        s.Table = Ident();
        s.Alias = OptAlias();
        if (Accept("WHERE"))
        {
            if (Accept("CURRENT"))
            {
                Expect("OF");
                s.CurrentOf = Ident();
            }
            else s.Where = Expression();
        }
        if (Accept("PLAN")) SkipPlan();
        if (Accept("ORDER"))
        {
            Expect("BY");
            s.OrderBy = OrderList();
        }
        if (Accept("ROWS"))
        {
            s.Rows = Additive();
            if (Accept("TO")) s.RowsTo = Additive();
        }
        s.Returning = Returning();
        return s;
    }

    UpdateOrInsertStmt UpdateOrInsert()
    {
        var s = new UpdateOrInsertStmt { Pos = Advance().Pos };
        Expect("OR");
        Expect("INSERT");
        Expect("INTO");
        s.Table = Ident();
        if (IsOp("(")) s.Columns = ParenColumnList(s.ColumnPos);
        Expect("VALUES");
        ExpectOp("(");
        s.Values.Add(ValueOrDefault());
        while (AcceptOp(",")) s.Values.Add(ValueOrDefault());
        ExpectOp(")");
        if (Accept("MATCHING")) s.Matching = ParenColumnList(s.MatchingPos);
        s.Returning = Returning();
        return s;
    }

    MergeStmt Merge()
    {
        var s = new MergeStmt { Pos = Advance().Pos };
        Expect("INTO");
        s.Table = Ident();
        if (!Is("USING")) s.Alias = OptAlias();
        Expect("USING");
        s.Source = TablePrimary();
        Expect("ON");
        s.On = Expression();
        while (Accept("WHEN"))
        {
            var w = new MergeWhen();
            if (Accept("NOT"))
            {
                Expect("MATCHED");
                if (Accept("BY")) Expect("TARGET");
                w.Matched = false;
            }
            else
            {
                Expect("MATCHED");
                w.Matched = true;
            }
            if (Accept("AND")) w.Condition = Expression();
            Expect("THEN");
            if (w.Matched)
            {
                if (Accept("DELETE")) w.Delete = true;
                else
                {
                    Expect("UPDATE");
                    Expect("SET");
                    w.Set = SetList();
                }
            }
            else
            {
                Expect("INSERT");
                if (IsOp("(")) w.InsertColumns = ParenColumnList();
                Expect("VALUES");
                ExpectOp("(");
                w.InsertValues = new List<Expr> { ValueOrDefault() };
                while (AcceptOp(",")) w.InsertValues.Add(ValueOrDefault());
                ExpectOp(")");
            }
            s.Whens.Add(w);
        }
        if (s.Whens.Count == 0) throw Unexpected();
        s.Returning = Returning();
        return s;
    }

    ExecProcStmt ExecProcedure()
    {
        var s = new ExecProcStmt { Pos = Advance().Pos };
        Expect("PROCEDURE");
        s.Name = Ident();
        var paren = AcceptOp("(");
        if (!(paren && IsOp(")")) && !IsOp(";") && Cur.Kind != Tok.Eof && !Is("RETURNING_VALUES"))
        {
            s.Args.Add(Expression());
            while (AcceptOp(",")) s.Args.Add(Expression());
        }
        if (paren) ExpectOp(")");
        if (Accept("RETURNING_VALUES"))
        {
            var paren2 = AcceptOp("(");
            s.ReturningValues = TargetList();
            if (paren2) ExpectOp(")");
        }
        return s;
    }

    ExecBlockStmt ExecBlock()
    {
        var s = new ExecBlockStmt { Pos = Advance().Pos };
        Expect("BLOCK");
        if (AcceptOp("("))
        {
            do
            {
                var p = new ParamDecl { Name = Ident(), Type = DataType(allowDomain: true) };
                if (Accept("NOT")) { Expect("NULL"); p.NotNull = true; }
                CollateOpt(p.Type);
                ExpectOp("=");
                p.Value = Expression();
                s.Inputs.Add(p);
            } while (AcceptOp(","));
            ExpectOp(")");
        }
        if (Accept("RETURNS")) s.Outputs = ParamList(allowDefault: false);
        Expect("AS");
        s.Body = PsqlBody();
        return s;
    }

    void CollateOpt(TypeSpec t)
    {
        if (Accept("COLLATE")) t.Collate = AnyIdent();
    }

    List<ParamDecl> ParamList(bool allowDefault)
    {
        var l = new List<ParamDecl>();
        ExpectOp("(");
        if (AcceptOp(")")) return l;
        do
        {
            var p = new ParamDecl { Name = Ident(), Type = DataType(allowDomain: true) };
            if (Accept("NOT")) { Expect("NULL"); p.NotNull = true; }
            CollateOpt(p.Type);
            if (allowDefault && (Accept("DEFAULT") || AcceptOp("="))) p.Default = Expression();
            l.Add(p);
        } while (AcceptOp(","));
        ExpectOp(")");
        return l;
    }

    // ------------------------------------------------------------------ SET ...

    Stmt SetStatement()
    {
        var pos = Advance().Pos;
        if (Accept("GENERATOR"))
        {
            var name = Ident();
            Expect("TO");
            return new SetGeneratorStmt { Name = name, Value = IntLiteral(), Pos = pos };
        }
        if (Accept("TRANSACTION"))
        {
            SkipToEnd("SET TRANSACTION");
            return new SetTransactionStmt { Pos = pos };
        }
        if (Accept("STATISTICS")) return SkipToEnd("SET STATISTICS");
        if (Is("SQL") && IsAt(1, "DIALECT"))
        {
            _i += 2;
            return new SetSqlDialectStmt { Dialect = (int)IntLiteral(), Pos = pos };
        }
        if (Accept("NAMES")) return SkipToEnd("SET NAMES");
        if (Accept("BIND") || Accept("DECFLOAT") || Accept("TIME") || Accept("ROLE")) return SkipToEnd("SET");
        throw Unexpected();
    }

    // ------------------------------------------------------------------ CREATE / ALTER / DROP

    Stmt Create()
    {
        Advance();
        if (Accept("OR"))
        {
            Expect("ALTER");
            return CreateObject(CreateMode.CreateOrAlter);
        }
        return CreateObject(CreateMode.Create);
    }

    Stmt CreateObject(CreateMode mode)
    {
        var pos = Cur.Pos;
        if (Accept("TABLE")) return CreateTable(mode, false, pos);
        if (Is("GLOBAL"))
        {
            _i++;
            Expect("TEMPORARY");
            Expect("TABLE");
            return CreateTable(mode, true, pos);
        }
        if (Accept("VIEW")) return CreateView(mode, pos);
        if (Accept("PROCEDURE")) return CreateProcedure(mode, pos);
        if (Accept("FUNCTION")) return CreateFunction(mode, pos);
        if (Accept("TRIGGER")) return CreateTrigger(mode, pos);
        if (Accept("EXCEPTION"))
        {
            var name = Ident();
            return new CreateExceptionStmt { Mode = mode, Name = name, Message = StringLiteral(), Pos = pos };
        }
        if (Accept("SEQUENCE") || Accept("GENERATOR")) return CreateSequence(mode, pos);
        if (mode == CreateMode.Create)
        {
            if (Accept("DOMAIN")) return CreateDomain(pos);
            var unique = Accept("UNIQUE");
            var desc = false;
            if (Accept("DESC") || Accept("DESCENDING")) desc = true;
            else if (!Accept("ASC")) Accept("ASCENDING");
            if (Accept("INDEX")) return CreateIndex(unique, desc, pos);
            if (unique || desc) throw Unexpected();
            if (Accept("ROLE")) return SkipToEnd("CREATE ROLE");
            if (Accept("USER")) return SkipToEnd("CREATE USER");
            if (Accept("SHADOW")) return SkipToEnd("CREATE SHADOW");
            if (Is("COLLATION")) return SkipToEnd("CREATE COLLATION");
            if (Is("PACKAGE")) throw Err.Unsupported("PACKAGE");
            if (Is("DATABASE")) throw Err.Unsupported("CREATE DATABASE statement (use FbConnection.CreateDatabase)");
        }
        if (Is("PACKAGE")) throw Err.Unsupported("PACKAGE");
        throw Unexpected();
    }

    CreateSequenceStmt CreateSequence(CreateMode mode, int pos)
    {
        var s = new CreateSequenceStmt { Mode = mode, Name = Ident(), Pos = pos };
        while (true)
        {
            if (Accept("START"))
            {
                Expect("WITH");
                s.StartWith = IntLiteral();
            }
            else if (Accept("INCREMENT"))
            {
                Accept("BY");
                s.IncrementBy = (int)IntLiteral();
            }
            else if (Accept("RESTART"))
            {
                s.Restart = true;
                if (Accept("WITH")) s.StartWith = IntLiteral();
            }
            else break;
        }
        return s;
    }

    Stmt Alter()
    {
        var pos = Advance().Pos;
        if (Accept("TABLE")) return AlterTable(pos);
        if (Accept("SEQUENCE") || Accept("GENERATOR")) return CreateSequence(CreateMode.Alter, pos);
        if (Accept("INDEX"))
        {
            var name = Ident();
            var s = new AlterIndexStmt { Name = name, Pos = pos };
            if (Accept("ACTIVE")) s.Active = true;
            else
            {
                Expect("INACTIVE");
                s.Active = false;
            }
            return s;
        }
        if (Accept("DOMAIN")) return AlterDomain(pos);
        if (Accept("DATABASE")) return SkipToEnd("ALTER DATABASE");
        if (Accept("USER") || Accept("ROLE") || Accept("CHARACTER")) return SkipToEnd("ALTER");
        if (Is("TRIGGER") && !IsAt(3, "AS"))
        {
            // ALTER TRIGGER name ACTIVE/INACTIVE/POSITION without body handled by CreateTrigger in Alter mode
        }
        return CreateObject(CreateMode.Alter);
    }

    Stmt Drop()
    {
        var pos = Advance().Pos;
        var kindTok = AnyIdent();
        ObjectKind kind = kindTok switch
        {
            "TABLE" => ObjectKind.Table,
            "VIEW" => ObjectKind.View,
            "PROCEDURE" => ObjectKind.Procedure,
            "FUNCTION" => ObjectKind.Function,
            "TRIGGER" => ObjectKind.Trigger,
            "EXCEPTION" => ObjectKind.Exception,
            "SEQUENCE" or "GENERATOR" => ObjectKind.Sequence,
            "INDEX" => ObjectKind.Index,
            "DOMAIN" => ObjectKind.Domain,
            "ROLE" => ObjectKind.Role,
            "USER" => ObjectKind.User,
            "PACKAGE" => ObjectKind.Package,
            "EXTERNAL" => ObjectKind.ExternalFunction,
            "FILTER" => ObjectKind.Filter,
            _ => throw Unexpected(),
        };
        if (kind == ObjectKind.ExternalFunction) Expect("FUNCTION");
        if (kind is ObjectKind.Role or ObjectKind.User or ObjectKind.Filter or ObjectKind.ExternalFunction)
            return SkipToEnd("DROP " + kindTok);
        return new DropStmt { Kind = kind, Name = Ident(), Pos = pos };
    }

    // ------------------------------------------------------------------ tables

    CreateTableStmt CreateTable(CreateMode mode, bool gtt, int pos)
    {
        var s = new CreateTableStmt { Mode = mode, Name = Ident(), GlobalTemporary = gtt, Pos = pos };
        if (Accept("EXTERNAL")) throw Err.Unsupported("external tables");
        ExpectOp("(");
        do
        {
            if (Is("CONSTRAINT") || Is("PRIMARY") || Is("UNIQUE") || Is("FOREIGN") || Is("CHECK"))
                s.Constraints.Add(TableConstraint());
            else
                s.Columns.Add(ColumnDefinition());
        } while (AcceptOp(","));
        ExpectOp(")");
        if (gtt && Accept("ON"))
        {
            Expect("COMMIT");
            if (Accept("PRESERVE")) s.OnCommitPreserve = true;
            else Expect("DELETE");
            Expect("ROWS");
        }
        return s;
    }

    ColumnDef ColumnDefinition()
    {
        var c = new ColumnDef { Name = Ident() };
        if (Accept("COMPUTED") || Is("GENERATED") && IsAt(1, "ALWAYS"))
        {
            if (Is("GENERATED")) { _i += 2; Expect("AS"); }
            else Accept("BY");
            ExpectOp("(");
            var start = _i;
            c.ComputedBy = Expression();
            c.ComputedBySource = SourceFrom(start);
            ExpectOp(")");
            return c;
        }
        if (Is("GENERATED") && (IsAt(1, "BY") || IsAt(1, "ALWAYS")))
        {
            // no data type: GENERATED ... AS IDENTITY implies BIGINT? (FB3 requires a type before)
        }
        else c.Type = DataType(allowDomain: true);
        if (Accept("COMPUTED"))
        {
            Accept("BY");
            ExpectOp("(");
            var start = _i;
            c.ComputedBy = Expression();
            c.ComputedBySource = SourceFrom(start);
            ExpectOp(")");
            return c;
        }
        if (Is("GENERATED") && (IsAt(1, "BY") || IsAt(1, "ALWAYS")))
        {
            _i++;
            if (Accept("ALWAYS")) c.IdentityAlways = true;
            else
            {
                Expect("BY");
                Expect("DEFAULT");
            }
            Expect("AS");
            Expect("IDENTITY");
            c.Identity = true;
            if (AcceptOp("("))
            {
                while (!IsOp(")"))
                {
                    if (Accept("START")) { Expect("WITH"); c.IdentityStart = IntLiteral(); }
                    else if (Accept("INCREMENT")) { Accept("BY"); c.IdentityIncrement = (int)IntLiteral(); }
                    else throw Unexpected();
                }
                ExpectOp(")");
            }
        }
        if (Accept("DEFAULT"))
        {
            var start = _i;
            c.Default = DefaultValue();
            c.DefaultSource = SourceFrom(start);
        }
        while (true)
        {
            if (Is("NOT") && IsAt(1, "NULL"))
            {
                _i += 2;
                c.NotNull = true;
                continue;
            }
            if (Accept("COLLATE"))
            {
                c.Collate = AnyIdent();
                continue;
            }
            if (Is("CONSTRAINT") || Is("PRIMARY") || Is("UNIQUE") || Is("REFERENCES") || Is("CHECK"))
            {
                string? name = null;
                if (Accept("CONSTRAINT")) name = Ident();
                if (Is("NOT") && IsAt(1, "NULL"))
                {
                    _i += 2;
                    c.NotNull = true;
                    c.NotNullName = name;
                    continue;
                }
                var con = new ConstraintDef { Name = name };
                con.Columns.Add(c.Name);
                if (Accept("PRIMARY"))
                {
                    Expect("KEY");
                    con.Kind = ConstraintKind.PrimaryKey;
                    UsingIndex(con);
                }
                else if (Accept("UNIQUE"))
                {
                    con.Kind = ConstraintKind.Unique;
                    UsingIndex(con);
                }
                else if (Accept("REFERENCES"))
                {
                    con.Kind = ConstraintKind.ForeignKey;
                    References(con);
                }
                else if (Accept("CHECK"))
                {
                    con.Kind = ConstraintKind.Check;
                    ExpectOp("(");
                    var start = _i;
                    con.Check = Expression();
                    con.CheckSource = SourceFrom(start);
                    ExpectOp(")");
                }
                else throw Unexpected();
                c.Constraints.Add(con);
                continue;
            }
            break;
        }
        return c;
    }

    Expr DefaultValue()
    {
        if (Is("NULL")) { _i++; return new LiteralExpr(Types.Value.Null, Types.SqlType.Null); }
        return Unary();
    }

    void UsingIndex(ConstraintDef c)
    {
        if (!Accept("USING")) return;
        if (Accept("DESC") || Accept("DESCENDING")) c.IndexDescending = true;
        else if (!Accept("ASC")) Accept("ASCENDING");
        Expect("INDEX");
        c.IndexName = Ident();
    }

    void References(ConstraintDef c)
    {
        c.RefTable = Ident();
        if (IsOp("(")) c.RefColumns = ParenIdentList();
        while (Accept("ON"))
        {
            var isDelete = Accept("DELETE");
            if (!isDelete) Expect("UPDATE");
            RefAction a;
            if (Accept("CASCADE")) a = RefAction.Cascade;
            else if (Accept("SET"))
            {
                if (Accept("NULL")) a = RefAction.SetNull;
                else
                {
                    Expect("DEFAULT");
                    a = RefAction.SetDefault;
                }
            }
            else
            {
                Expect("NO");
                Expect("ACTION");
                a = RefAction.NoAction;
            }
            if (isDelete) c.OnDelete = a;
            else c.OnUpdate = a;
        }
        UsingIndex(c);
    }

    ConstraintDef TableConstraint()
    {
        var c = new ConstraintDef();
        if (Accept("CONSTRAINT")) c.Name = Ident();
        if (Accept("PRIMARY"))
        {
            Expect("KEY");
            c.Kind = ConstraintKind.PrimaryKey;
            c.Columns = ParenIdentList();
            UsingIndex(c);
        }
        else if (Accept("UNIQUE"))
        {
            c.Kind = ConstraintKind.Unique;
            c.Columns = ParenIdentList();
            UsingIndex(c);
        }
        else if (Accept("FOREIGN"))
        {
            Expect("KEY");
            c.Kind = ConstraintKind.ForeignKey;
            c.Columns = ParenIdentList();
            Expect("REFERENCES");
            References(c);
        }
        else if (Accept("CHECK"))
        {
            c.Kind = ConstraintKind.Check;
            ExpectOp("(");
            var start = _i;
            c.Check = Expression();
            c.CheckSource = SourceFrom(start);
            ExpectOp(")");
        }
        else throw Unexpected();
        return c;
    }

    AlterTableStmt AlterTable(int pos)
    {
        var s = new AlterTableStmt { Name = Ident(), Pos = pos };
        do
        {
            if (Accept("ADD"))
            {
                if (Is("CONSTRAINT") || Is("PRIMARY") || Is("UNIQUE") || Is("FOREIGN") || Is("CHECK"))
                    s.Actions.Add(new AddConstraintAction { Constraint = TableConstraint() });
                else
                {
                    Accept("COLUMN");
                    s.Actions.Add(new AddColumnAction { Column = ColumnDefinition() });
                }
            }
            else if (Accept("DROP"))
            {
                if (Accept("CONSTRAINT")) s.Actions.Add(new DropConstraintAction { Name = Ident() });
                else
                {
                    Accept("COLUMN");
                    s.Actions.Add(new DropColumnAction { Name = Ident() });
                }
            }
            else if (Accept("ALTER"))
            {
                Accept("COLUMN");
                var a = new AlterColumnAction { Name = Ident() };
                if (Accept("TO")) a.NewName = Ident();
                else if (Accept("POSITION")) a.Position = (int)IntLiteral();
                else if (Accept("TYPE")) a.NewType = DataType(allowDomain: true);
                else if (Accept("SET"))
                {
                    if (Accept("DEFAULT"))
                    {
                        a.SetDefault = true;
                        var start = _i;
                        a.Default = DefaultValue();
                        a.DefaultSource = SourceFrom(start);
                    }
                    else if (Accept("NOT"))
                    {
                        Expect("NULL");
                        a.NotNull = true;
                    }
                    else throw Unexpected();
                }
                else if (Accept("DROP"))
                {
                    if (Accept("DEFAULT")) a.DropDefault = true;
                    else if (Accept("NOT"))
                    {
                        Expect("NULL");
                        a.NotNull = false;
                    }
                    else throw Unexpected();
                }
                else if (Accept("RESTART"))
                {
                    a.RestartIdentity = true;
                    if (Accept("WITH")) a.RestartWith = IntLiteral();
                }
                else if (Accept("COMPUTED"))
                {
                    Accept("BY");
                    ExpectOp("(");
                    var start = _i;
                    a.ComputedBy = Expression();
                    a.ComputedBySource = SourceFrom(start);
                    ExpectOp(")");
                }
                else
                {
                    // ALTER col datatype (FB2 syntax)
                    a.NewType = DataType(allowDomain: true);
                }
                s.Actions.Add(a);
            }
            else throw Unexpected();
        } while (AcceptOp(","));
        return s;
    }

    CreateIndexStmt CreateIndex(bool unique, bool desc, int pos)
    {
        var s = new CreateIndexStmt { Name = Ident(), Unique = unique, Descending = desc, Pos = pos };
        Expect("ON");
        s.Table = Ident();
        if (Accept("COMPUTED"))
        {
            Accept("BY");
            ExpectOp("(");
            s.ComputedBy = Expression();
            ExpectOp(")");
        }
        else s.Columns = ParenIdentList();
        return s;
    }

    CreateViewStmt CreateView(CreateMode mode, int pos)
    {
        var s = new CreateViewStmt { Mode = mode, Name = Ident(), Pos = pos };
        if (IsOp("(")) s.Columns = ParenIdentList();
        Expect("AS");
        var start = _i;
        s.Query = SelectQueryExpr();
        s.Source = SourceFrom(start);
        if (Accept("WITH"))
        {
            Expect("CHECK");
            Expect("OPTION");
            s.WithCheckOption = true;
        }
        return s;
    }

    CreateDomainStmt CreateDomain(int pos)
    {
        var s = new CreateDomainStmt { Name = Ident(), Pos = pos };
        Accept("AS");
        s.Type = DataType(allowDomain: false);
        while (true)
        {
            if (Accept("DEFAULT"))
            {
                var start = _i;
                s.Default = DefaultValue();
                s.DefaultSource = SourceFrom(start);
            }
            else if (Is("NOT") && IsAt(1, "NULL")) { _i += 2; s.NotNull = true; }
            else if (Accept("CHECK"))
            {
                ExpectOp("(");
                var start = _i;
                s.Check = Expression();
                s.CheckSource = SourceFrom(start);
                ExpectOp(")");
            }
            else if (Accept("COLLATE")) s.Collate = AnyIdent();
            else break;
        }
        return s;
    }

    AlterDomainStmt AlterDomain(int pos)
    {
        var s = new AlterDomainStmt { Name = Ident(), Pos = pos };
        while (true)
        {
            if (Accept("TO")) s.NewName = Ident();
            else if (Accept("SET"))
            {
                if (Accept("DEFAULT")) { s.SetDefault = true; s.Default = DefaultValue(); }
                else { Expect("NOT"); Expect("NULL"); s.NotNull = true; }
            }
            else if (Accept("DROP"))
            {
                if (Accept("DEFAULT")) s.DropDefault = true;
                else if (Accept("CONSTRAINT")) s.DropConstraint = true;
                else { Expect("NOT"); Expect("NULL"); s.NotNull = false; }
            }
            else if (Accept("ADD"))
            {
                Accept("CONSTRAINT");
                Expect("CHECK");
                ExpectOp("(");
                s.AddCheck = Expression();
                ExpectOp(")");
            }
            else if (Accept("TYPE")) s.NewType = DataType(allowDomain: false);
            else break;
        }
        return s;
    }

    // ------------------------------------------------------------------ procedures / functions / triggers

    CreateProcedureStmt CreateProcedure(CreateMode mode, int pos)
    {
        var startTok = _i;
        var s = new CreateProcedureStmt { Mode = mode, Name = Ident(), Pos = pos };
        if (IsOp("(")) s.Inputs = ParamList(allowDefault: true);
        if (Accept("RETURNS")) s.Outputs = ParamList(allowDefault: false);
        if (Is("EXTERNAL")) throw Err.Unsupported("external procedures");
        Accept("SQL");
        Accept("SECURITY");
        Expect("AS");
        s.Body = PsqlBody();
        s.Source = SourceFrom(startTok);
        return s;
    }

    CreateFunctionStmt CreateFunction(CreateMode mode, int pos)
    {
        var startTok = _i;
        var s = new CreateFunctionStmt { Mode = mode, Name = Ident(), Pos = pos };
        if (IsOp("(")) s.Inputs = ParamList(allowDefault: true);
        Expect("RETURNS");
        s.Returns = DataType(allowDomain: true);
        CollateOpt(s.Returns);
        if (Accept("DETERMINISTIC")) s.Deterministic = true;
        if (Is("EXTERNAL")) throw Err.Unsupported("external functions");
        Expect("AS");
        s.Body = PsqlBody();
        s.Source = SourceFrom(startTok);
        return s;
    }

    CreateTriggerStmt CreateTrigger(CreateMode mode, int pos)
    {
        var startTok = _i;
        var s = new CreateTriggerStmt { Mode = mode, Name = Ident(), Pos = pos };
        // Legacy syntax: FOR table
        if (Accept("FOR")) s.Table = Ident();
        var hasEvent = false;
        while (true)
        {
            if (Accept("ACTIVE")) s.Active = true;
            else if (Accept("INACTIVE")) s.Active = false;
            else if (Accept("BEFORE") || Accept("AFTER"))
            {
                s.Timing = _t[_i - 1].Text == "BEFORE" ? TriggerTiming.Before : TriggerTiming.After;
                hasEvent = true;
                if (Is("DDL") || Is("ANY")) throw Err.Unsupported("DDL triggers");
                do
                {
                    var ev = AnyIdent();
                    s.Events |= ev switch
                    {
                        "INSERT" => TriggerEvent.Insert,
                        "UPDATE" => TriggerEvent.Update,
                        "DELETE" => TriggerEvent.Delete,
                        _ => throw Err.Unsupported("trigger event " + ev),
                    };
                } while (Accept("OR"));
            }
            else if (Accept("POSITION")) s.Position = (int)IntLiteral();
            else if (Is("ON") && s.Table == null)
            {
                _i++;
                if (Accept("CONNECT")) s.DbEvent = DbTriggerEvent.Connect;
                else if (Accept("DISCONNECT")) s.DbEvent = DbTriggerEvent.Disconnect;
                else if (Accept("TRANSACTION"))
                {
                    var ev = AnyIdent();
                    s.DbEvent = ev switch
                    {
                        "START" => DbTriggerEvent.TransactionStart,
                        "COMMIT" => DbTriggerEvent.TransactionCommit,
                        "ROLLBACK" => DbTriggerEvent.TransactionRollback,
                        _ => throw Unexpected(),
                    };
                }
                else s.Table = Ident();
                hasEvent = true;
            }
            else break;
        }
        if (Accept("SQL")) Expect("SECURITY");
        if (Accept("AS"))
        {
            s.Body = PsqlBody();
        }
        else if (mode != CreateMode.Alter) throw Unexpected();
        if (!hasEvent && mode != CreateMode.Alter) throw Unexpected();
        s.Source = SourceFrom(startTok);
        return s;
    }

    // ======================================================================== PSQL

    /// <summary>[declarations] BEGIN ... END</summary>
    BlockStmt PsqlBody()
    {
        var decls = new List<PsqlDecl>();
        while (Is("DECLARE"))
        {
            _i++;
            if (Accept("PROCEDURE") || Accept("FUNCTION"))
            {
                var isFunc = _t[_i - 1].Text == "FUNCTION";
                var sub = new SubRoutineDecl { IsFunction = isFunc, Name = Ident() };
                if (IsOp("(")) sub.Inputs = ParamList(allowDefault: true);
                if (isFunc)
                {
                    Expect("RETURNS");
                    sub.Returns = DataType(allowDomain: true);
                }
                else if (Accept("RETURNS")) sub.Outputs = ParamList(allowDefault: false);
                if (Accept("AS")) sub.Body = PsqlBody();
                else ExpectOp(";");
                decls.Add(sub);
                continue;
            }
            Accept("VARIABLE");
            var name = Ident();
            if (Accept("SCROLL") || Accept("NO"))
            {
                if (_t[_i - 1].Text == "NO") Expect("SCROLL");
            }
            if (Accept("CURSOR"))
            {
                Expect("FOR");
                ExpectOp("(");
                var q = SelectQueryExpr();
                ExpectOp(")");
                ExpectOp(";");
                decls.Add(new CursorDecl { Name = name, Query = q });
                continue;
            }
            var v = new VarDecl { Name = name, Type = DataType(allowDomain: true) };
            if (Accept("NOT")) { Expect("NULL"); v.NotNull = true; }
            CollateOpt(v.Type);
            if (Accept("DEFAULT") || AcceptOp("=")) v.Default = Expression();
            ExpectOp(";");
            decls.Add(v);
        }
        var block = Block();
        block.Declarations = decls;
        return block;
    }

    BlockStmt Block()
    {
        var b = new BlockStmt { Pos = Cur.Pos };
        Expect("BEGIN");
        while (!Is("END") && !Is("WHEN"))
        {
            if (AcceptOp(";")) continue;
            b.Body.Add(PsqlStatement());
        }
        while (Accept("WHEN"))
        {
            var h = new WhenHandler();
            do
            {
                if (Accept("ANY")) h.Any = true;
                else if (Accept("EXCEPTION")) h.Conditions.Add(new WhenCondition("EXCEPTION", Ident(), 0));
                else if (Accept("SQLCODE")) h.Conditions.Add(new WhenCondition("SQLCODE", null, (int)IntLiteral()));
                else if (Accept("GDSCODE")) h.Conditions.Add(new WhenCondition("GDSCODE", AnyIdent(), 0));
                else if (Accept("SQLSTATE")) h.Conditions.Add(new WhenCondition("SQLSTATE", StringLiteral(), 0));
                else throw Unexpected();
            } while (AcceptOp(","));
            Expect("DO");
            h.Body = PsqlStatement();
            b.Handlers.Add(h);
        }
        Expect("END");
        return b;
    }

    void EndStatement() => ExpectOp(";");

    Stmt PsqlStatement()
    {
        var start = Cur;
        var st = PsqlStatementCore();
        if (st.Line == 0) (st.Line, st.Col) = (start.Line, start.Col);
        return st;
    }

    Stmt PsqlStatementCore()
    {
        var pos = Cur.Pos;
        // label:
        if (Cur.Kind is Tok.Ident or Tok.QuotedIdent && PeekTok().Kind == Tok.Colon && !Reserved.Contains(Cur.Text))
        {
            var label = Advance().Text;
            _i++;
            // a label can only name a loop
            if (!Is("WHILE") && !Is("FOR")) throw Unexpected();
            var st = PsqlStatement();
            switch (st)
            {
                case WhileStmt w: w.Label = label; break;
                case ForSelectStmt f: f.Label = label; break;
                case ForExecStmt fe: fe.Label = label; break;
                default: throw Err.SyntaxMsg("Label must precede a loop");
            }
            return st;
        }
        if (Cur.Kind == Tok.Colon)
        {
            // :var = expr (old syntax)
            var target = Target();
            ExpectOp("=");
            var value = Expression();
            EndStatement();
            return new AssignStmt { Target = target, Value = value, Pos = pos };
        }
        if (Cur.Kind == Tok.QuotedIdent)
            return AssignmentStatement(pos);
        if (Cur.Kind != Tok.Ident) throw Unexpected();
        switch (Cur.Text)
        {
            case "BEGIN": return Block();
            case "IF":
            {
                _i++;
                ExpectOp("(");
                var cond = Expression();
                ExpectOp(")");
                Expect("THEN");
                var s = new IfStmt { Condition = cond, Then = PsqlStatement(), Pos = pos };
                if (Accept("ELSE")) s.Else = PsqlStatement();
                return s;
            }
            case "WHILE":
            {
                _i++;
                ExpectOp("(");
                var cond = Expression();
                ExpectOp(")");
                Expect("DO");
                return new WhileStmt { Condition = cond, Body = PsqlStatement(), Pos = pos };
            }
            case "FOR":
            {
                _i++;
                if (Is("EXECUTE"))
                {
                    var ex = (ExecStatementStmt)ExecStatementCore();
                    Expect("DO");
                    return new ForExecStmt { Exec = ex, Body = PsqlStatement(), Pos = pos };
                }
                var q = SelectQueryExpr();
                var f = new ForSelectStmt { Query = q, Pos = pos };
                if (q.Body is QuerySpec qs && qs.Into != null) f.Into = qs.Into;
                if (Accept("INTO")) f.Into = TargetList();
                if (Accept("AS"))
                {
                    Expect("CURSOR");
                    f.CursorName = Ident();
                }
                Expect("DO");
                f.Body = PsqlStatement();
                return f;
            }
            case "SELECT":
            case "WITH":
            {
                var s = SelectStatement();
                EndStatement();
                return s;
            }
            case "INSERT":
            case "UPDATE":
            case "DELETE":
            case "MERGE":
            {
                var s = Statement();
                EndStatement();
                return s;
            }
            case "EXECUTE":
            {
                Stmt s;
                if (IsAt(1, "PROCEDURE")) s = ExecProcedure();
                else if (IsAt(1, "STATEMENT")) s = ExecStatementCore();
                else throw Unexpected();
                EndStatement();
                return s;
            }
            case "SUSPEND": _i++; EndStatement(); return new SuspendStmt { Pos = pos };
            case "EXIT": _i++; EndStatement(); return new ExitStmt { Pos = pos };
            case "BREAK": _i++; EndStatement(); return new LeaveStmt { Pos = pos };
            case "LEAVE":
            {
                _i++;
                var s = new LeaveStmt { Pos = pos };
                if (!IsOp(";")) s.Label = Ident();
                EndStatement();
                return s;
            }
            case "CONTINUE":
            {
                _i++;
                var s = new ContinueStmt { Pos = pos };
                if (!IsOp(";")) s.Label = Ident();
                EndStatement();
                return s;
            }
            case "RETURN":
            {
                _i++;
                var s = new ReturnStmt { Value = Expression(), Pos = pos };
                EndStatement();
                return s;
            }
            case "EXCEPTION":
            {
                _i++;
                var s = new RaiseStmt { Pos = pos };
                if (!IsOp(";"))
                {
                    s.Name = Ident();
                    if (Accept("USING"))
                    {
                        ExpectOp("(");
                        s.Using = new List<Expr> { Expression() };
                        while (AcceptOp(",")) s.Using.Add(Expression());
                        ExpectOp(")");
                    }
                    else if (!IsOp(";")) s.Message = Expression();
                }
                EndStatement();
                return s;
            }
            case "OPEN":
            {
                _i++;
                var s = new OpenCursorStmt { Name = Ident(), Pos = pos };
                EndStatement();
                return s;
            }
            case "CLOSE":
            {
                _i++;
                var s = new CloseCursorStmt { Name = Ident(), Pos = pos };
                EndStatement();
                return s;
            }
            case "FETCH":
            {
                _i++;
                if (Accept("NEXT") || Accept("PRIOR") || Accept("FIRST") || Accept("LAST")) Expect("FROM");
                var s = new FetchCursorStmt { Name = Ident(), Pos = pos };
                Expect("INTO");
                s.Into = TargetList();
                EndStatement();
                return s;
            }
            case "IN" when IsAt(1, "AUTONOMOUS"):
            {
                _i += 2;
                Expect("TRANSACTION");
                Expect("DO");
                return new AutonomousStmt { Body = PsqlStatement(), Pos = pos };
            }
            case "POST_EVENT":
            {
                _i++;
                var s = new PostEventStmt { Name = Expression(), Pos = pos };
                EndStatement();
                return s;
            }
        }
        return AssignmentStatement(pos);
    }

    Stmt AssignmentStatement(int pos)
    {
        if (PeekTok().IsOp("("))
        {
            var call = Expression();
            EndStatement();
            return new CallStmt { Call = call, Pos = pos };
        }
        var name = AnyIdent();
        Expr target;
        if (AcceptOp(".")) target = At(new ColumnRefExpr(name, AnyIdent()), pos);
        else target = At(new VariableExpr(name), pos);
        ExpectOp("=");
        var value = Expression();
        EndStatement();
        return new AssignStmt { Target = target, Value = value, Pos = pos };
    }

    Stmt ExecStatement()
    {
        return ExecStatementCore();
    }

    Stmt ExecStatementCore()
    {
        var pos = Advance().Pos;
        Expect("STATEMENT");
        var s = new ExecStatementStmt { Pos = pos };
        if (AcceptOp("("))
        {
            s.Sql = Expression();
            ExpectOp(")");
            if (AcceptOp("("))
            {
                do
                {
                    string? pname = null;
                    if (Cur.Kind == Tok.Ident && PeekTok().IsOp(":") == false && PeekTok().Kind == Tok.Colon && PeekTok(2).IsOp("="))
                    {
                        pname = Advance().Text;
                        _i += 2;
                    }
                    s.ArgNames.Add(pname);
                    s.Args.Add(Expression());
                } while (AcceptOp(","));
                ExpectOp(")");
            }
        }
        else s.Sql = Expression();
        while (true)
        {
            if (Accept("WITH"))
            {
                if (Accept("AUTONOMOUS")) { Expect("TRANSACTION"); s.Autonomous = true; }
                else if (Accept("COMMON")) Expect("TRANSACTION");
                else if (Accept("CALLER")) Expect("PRIVILEGES");
                else throw Unexpected();
            }
            else if (Accept("AS"))
            {
                // AS USER x PASSWORD y ROLE z
                while (Accept("USER") || Accept("PASSWORD") || Accept("ROLE")) Primary();
            }
            else if (Accept("ON"))
            {
                Expect("EXTERNAL");
                throw Err.Unsupported("EXECUTE STATEMENT ON EXTERNAL");
            }
            else break;
        }
        if (Accept("INTO")) s.Into = TargetList();
        return s;
    }
}
