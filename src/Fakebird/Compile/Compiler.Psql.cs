using Fakebird.Engine;
using Fakebird.Exec;
using Fakebird.Sql;
using Fakebird.Types;

namespace Fakebird.Compile;

public sealed partial class Compiler
{
    // ======================================================================== entry points

    /// <summary>Compiles one DSQL statement. Uses the database plan cache.</summary>
    public static Prepared Prepare(Database db, string sql)
    {
        if (db.PlanCache.TryGetValue(sql, out var cached) && ((Prepared)cached).Version == db.SchemaVersion)
            return (Prepared)cached;
        var stmt = Parser.ParseStatement(sql, out var pc);
        var comp = new Compiler(db, pc);
        var p = comp.CompileTop(stmt);
        // parameters whose type stayed unknown fail when the statement is described, after everything else
        if (comp.UnknownParam) throw Err.DataTypeUnknown();
        p.ParamTypes = comp.FinalParamTypes();
        p.StreamCount = comp.StreamCount;
        p.Version = db.SchemaVersion;
        // DDL changes the schema when executed; never cache it
        if (p.StmtType != Wire.Isc.stmt_ddl && db.PlanCache.Count < 10_000) db.PlanCache[sql] = p;
        return p;
    }

    Prepared CompileTop(Stmt stmt)
    {
        switch (stmt)
        {
            case SelectStmt s:
            {
                if (s.Into != null) throw Err.Dsql(-104, "INTO is allowed only in PSQL");
                _openAtExecute = true;
                var q = CompileSelect(s.Query, null);
                q.AtExecute = true;
                if (q is LimitQuery { Input: SpecQuery inner }) inner.AtExecute = true;
                return new SelectPrepared
                {
                    Query = q,
                    Outputs = q.Columns,
                    StmtType = s.Query.ForUpdate || s.Query.WithLock ? Wire.Isc.stmt_select_for_upd : Wire.Isc.stmt_select,
                };
            }
            case InsertStmt i: return CheckNoInto(i.Returning, CompileInsert(i, null));
            case UpdateStmt u: return CheckNoInto(u.Returning, CompileUpdate(u, null));
            case DeleteStmt d: return CheckNoInto(d.Returning, CompileDelete(d, null));
            case UpdateOrInsertStmt ui: return CheckNoInto(ui.Returning, CompileUpdateOrInsert(ui, null));
            case MergeStmt m: return CheckNoInto(m.Returning, CompileMerge(m, null));
            case ExecProcStmt ep:
                if (ep.ReturningValues != null) throw Err.Dsql(-104, "RETURNING_VALUES is allowed only in PSQL");
                return CompileExecProc(ep, null);
            case ExecBlockStmt eb: return CompileExecBlock(eb);
            case CommitStmt cm:
                return new DelegatePrepared(c =>
                {
                    c.Tx.Commit(cm.Retain);
                    return ExecResult.Empty;
                }) { StmtType = Wire.Isc.stmt_commit };
            case RollbackStmt rb:
                return new DelegatePrepared(c =>
                {
                    if (rb.ToSavepoint != null) c.Tx.RollbackToSavepoint(rb.ToSavepoint);
                    else c.Tx.Rollback(rb.Retain);
                    return ExecResult.Empty;
                }) { StmtType = rb.ToSavepoint != null ? Wire.Isc.stmt_savepoint : Wire.Isc.stmt_rollback };
            case SavepointStmt sp:
                return new DelegatePrepared(c =>
                {
                    c.Tx.Savepoint(sp.Name);
                    return ExecResult.Empty;
                }) { StmtType = Wire.Isc.stmt_savepoint };
            case ReleaseSavepointStmt rs:
                return new DelegatePrepared(c =>
                {
                    c.Tx.ReleaseSavepoint(rs.Name, rs.Only);
                    return ExecResult.Empty;
                }) { StmtType = Wire.Isc.stmt_savepoint };
            case SetTransactionStmt:
                throw Err.Unsupported("SET TRANSACTION as a statement (use the client transaction API)");
            case SetGeneratorStmt sg:
                return new DelegatePrepared(c =>
                {
                    Ddl.SetGenerator(c.Db, sg);
                    return ExecResult.Empty;
                }) { StmtType = Wire.Isc.stmt_set_generator };
            case SetTimeZoneStmt tz:
                return new DelegatePrepared(c =>
                {
                    c.Att.SessionTimeZone = tz.Zone == null ? c.Att.OriginalTimeZone : TimeZones.Parse(tz.Zone);
                    TimeZones.Session = c.Att.SessionTimeZone;
                    return ExecResult.Empty;
                }) { StmtType = Wire.Isc.stmt_ddl };
            case NoOpStmt:
            case SetSqlDialectStmt:
                return new DelegatePrepared(c => ExecResult.Empty) { StmtType = Wire.Isc.stmt_ddl };
            default:
                return new DelegatePrepared(c =>
                {
                    Ddl.Execute(c.Db, stmt, c);
                    return ExecResult.Empty;
                }) { StmtType = Wire.Isc.stmt_ddl };
        }
    }

    static Prepared CheckNoInto(ReturningClause? r, Prepared p)
    {
        // outside PSQL the INTO targets are not variables: Firebird reports the first as an unknown column
        if (r?.Into is [var first, ..])
            throw Err.ColumnUnknown(first is VariableExpr v ? v.Name : first.ToString()!, first.Line, first.Col);
        return p;
    }

    // ======================================================================== routines

    void BeginRoutine(bool isTrigger = false)
    {
        Routine = new RoutineScope { IsTrigger = isTrigger };
        Routine.VarStream = NewStream(0);
        RootScope = new Scope(null);
    }

    CompiledRoutine FinishRoutine(string name, PStmt body)
    {
        var r = Routine!;
        SetWidth(r.VarStream, r.Vars.Count);
        return new CompiledRoutine
        {
            Name = name,
            Streams = StreamCount,
            VarStream = r.VarStream,
            VarCount = r.Vars.Count,
            VarTypes = r.Vars.Select(v => v.Type).ToArray(),
            VarNotNull = r.Vars.Select(v => v.NotNull).ToArray(),
            Body = body,
            CursorCount = r.CursorCount,
            Selectable = r.HasSuspend,
        };
    }

    public static CompiledRoutine CompileProcedure(Database db, Procedure p)
    {
        var comp = ForStored(db);
        comp.BeginRoutine();
        var ins = p.Inputs.Select(i => comp.Routine!.Declare(i.Name, i.Type, i.NotNull)).ToArray();
        var outs = p.Outputs.Select(o => comp.Routine!.Declare(o.Name, o.Type, o.NotNull)).ToArray();
        comp.Routine!.OutputVars = outs;
        var body = comp.CompileRoutineBody(p.Body);
        var cr = comp.FinishRoutine(p.Name, body);
        cr.Context = $"At procedure '{p.Name}'";
        cr.Inputs = ins;
        cr.Outputs = outs;
        return cr;
    }

    public static CompiledRoutine CompileFunction(Database db, Function f)
    {
        var comp = ForStored(db);
        comp.BeginRoutine();
        comp.Routine!.IsFunction = true;
        comp.Routine.FunctionReturns = f.Returns;
        var ins = f.Inputs.Select(i => comp.Routine!.Declare(i.Name, i.Type, i.NotNull)).ToArray();
        var body = comp.CompileRoutineBody(f.Body);
        var cr = comp.FinishRoutine(f.Name, body);
        cr.Context = $"At function '{f.Name}'";
        cr.Inputs = ins;
        cr.Returns = f.Returns;
        return cr;
    }

    public static CompiledRoutine CompileTrigger(Database db, Trigger t, Table table)
    {
        var comp = ForStored(db);
        comp.BeginRoutine(isTrigger: true);
        var newStream = comp.NewStream(table.Columns.Count);
        var oldStream = comp.NewStream(table.Columns.Count);
        // an INSERT trigger has no OLD record and a DELETE trigger no NEW one: their columns are unknown
        if ((t.Events & (TriggerEvent.Insert | TriggerEvent.Update)) != 0) comp.RootScope!.Add(TableItem(table, "NEW", newStream, true, "NEW"));
        if ((t.Events & (TriggerEvent.Update | TriggerEvent.Delete)) != 0) comp.RootScope!.Add(TableItem(table, "OLD", oldStream, true, "OLD"));
        comp._triggerNewStream = newStream;
        comp._triggerTable = table;
        comp._triggerBefore = t.Timing == TriggerTiming.Before;
        comp._triggerEvents = t.Events;
        var body = comp.CompileRoutineBody(t.Body);
        var cr = comp.FinishRoutine(t.Name, body);
        cr.Context = $"At trigger '{t.Name}'";
        cr.NewStream = newStream;
        cr.OldStream = oldStream;
        cr.NewWidth = table.Columns.Count;
        return cr;
    }

    int _triggerNewStream = -1;
    Table? _triggerTable;
    bool _triggerBefore;
    TriggerEvent _triggerEvents;

    /// <summary>EXECUTE BLOCK: a routine compiled into the statement's own stream space.</summary>
    Prepared CompileExecBlock(ExecBlockStmt eb)
    {
        BeginRoutine();
        var r = Routine!;
        var inputs = new List<(int Var, XNode Value)>();
        foreach (var p in eb.Inputs)
        {
            var type = ResolveType(p.Type);
            var vi = r.Declare(p.Name, type, p.NotNull);
            inputs.Add((vi, Bind(p.Value!, new Scope(null), type)));
        }
        var outs = eb.Outputs.Select(o => r.Declare(o.Name, ResolveType(o.Type), o.NotNull)).ToArray();
        r.OutputVars = outs;
        var body = CompileRoutineBody(eb.Body);
        SetWidth(r.VarStream, r.Vars.Count);
        var varStream = r.VarStream;
        var varCount = r.Vars.Count;
        var cursorCount = r.CursorCount;
        var selectable = r.HasSuspend;
        var outCols = eb.Outputs.Select((o, i) => new OutCol { Name = o.Name, Field = o.Name, Type = r.Vars[outs[i]].Type }).ToArray();
        return new DelegatePrepared(c =>
        {
            var vars = new Value[varCount];
            c.Rows[varStream] = vars;
            if (cursorCount > 0) c.Cursors = new object?[cursorCount];
            foreach (var (vi, val) in inputs) vars[vi] = Conv.Cast(val.Eval(c), r.Vars[vi].Type);
            c.Frame = new PsqlFrame("At block", null);
            try
            {
                if (selectable) c.Output = new List<Value[]>();
                body.Exec(c);
            }
            catch (FbError e) when (!e.TraceDone)
            {
                throw c.Frame.Trace(e);
            }
            if (selectable) return new ExecResult { Rows = c.Output!, Selected = c.Output!.Count };
            if (outs.Length == 0) return new ExecResult { Rows = [[]] };
            return new ExecResult { Rows = [outs.Select(o => vars[o]).ToArray()] };
        })
        {
            StmtType = selectable ? Wire.Isc.stmt_select : Wire.Isc.stmt_exec_procedure,
            Outputs = outCols,
        };
    }

    PStmt CompileRoutineBody(BlockStmt body)
    {
        var r = Routine!;
        // declarations of the top-level block
        var decls = new List<(Target, XNode?)>();
        foreach (var d in body.Declarations)
        {
            switch (d)
            {
                case VarDecl v:
                {
                    var type = ResolveType(v.Type);
                    var vi = r.Declare(v.Name, type, v.NotNull);
                    var init = v.Default == null ? null : Bind(v.Default, RootScope!, type);
                    decls.Add((new Target(r.VarStream, vi, type, v.NotNull, v.Name), init));
                    break;
                }
                case CursorDecl cd:
                {
                    var q = CompileSelect(cd.Query, RootScope);
                    var cols = q.Columns.Select(col => new ColInfo { Name = col.Name, Type = col.Type, Field = col.Field, Relation = col.Relation }).ToList();
                    var st = NewStream(cols.Count);
                    var slot = r.CursorCount++;
                    r.Cursors[cd.Name] = (slot, st, cols, q);
                    RootScope!.Add(new SourceItem { Alias = cd.Name, Name = cd.Name, Stream = st, Cols = cols, QualifiedOnly = true });
                    break;
                }
                case SubRoutineDecl sub:
                    (r.SubRoutines ??= new(StringComparer.Ordinal))[sub.Name] = sub;
                    break;
            }
        }
        var block = (BlockP)CompileBlock(body);
        block.Decls = decls.ToArray();
        return block;
    }

    PStmt CompileBlock(BlockStmt b)
    {
        var body = b.Body.Select(CompilePsql).ToArray();
        var handlers = b.Handlers.Select(h => (MakeMatcher(h), CompilePsql(h.Body))).ToArray();
        return new BlockP { Body = body, Handlers = handlers };
    }

    Func<FbError, bool> MakeMatcher(WhenHandler h)
    {
        if (h.Any) return _ => true;
        var preds = h.Conditions.Select<WhenCondition, Func<FbError, bool>>(cnd => cnd.Kind switch
        {
            "EXCEPTION" => Db.Exceptions.ContainsKey(cnd.Name!)
                ? e => e.UserExceptionName == cnd.Name
                : throw Err.ObjectUnknown("Exception", cnd.Name!),
            "SQLCODE" => e => SqlCodes.SqlCodeOf(e) == cnd.Code,
            "GDSCODE" => SqlCodes.GdsCodeByName(cnd.Name!) is var code ? e => e.GdsCode == code : null!,
            "SQLSTATE" => e => e.SqlState == cnd.Name,
            _ => throw Err.Unsupported("WHEN " + cnd.Kind),
        }).ToArray();
        return e => preds.Any(p => p(e));
    }

    Target ResolveTarget(Expr e)
    {
        var r = Routine!;
        switch (e)
        {
            case VariableExpr v:
                return VarTarget(v.Name, v);
            case ColumnRefExpr { Qualifier: null } c:
                return VarTarget(c.Name, c);
            case ColumnRefExpr { Qualifier: "NEW" } c when r.IsTrigger:
            {
                if (_triggerEvents == TriggerEvent.Delete) throw Err.ColumnUnknown("NEW." + c.Name, c.Line, c.Col);
                if (!_triggerBefore) throw Err.Random("Cannot update NEW context in AFTER trigger");
                var t = _triggerTable!;
                var ci = t.FindColumn(c.Name);
                if (ci < 0) throw Err.ColumnUnknown("NEW." + c.Name, c.Line, c.Col);
                return new Target(_triggerNewStream, ci, t.Columns[ci].Type, false, "NEW." + c.Name);
            }
            case ColumnRefExpr { Qualifier: "OLD" } c when r.IsTrigger && _triggerEvents == TriggerEvent.Insert:
                throw Err.ColumnUnknown("OLD." + c.Name, c.Line, c.Col);
            case ColumnRefExpr c:
                throw Err.Random($"Cannot assign to {c}");
        }
        throw Err.Dsql(-104, "Invalid assignment target");
    }

    Target VarTarget(string name, Expr at)
    {
        var r = Routine!;
        if (!r.VarIndex.TryGetValue(name, out var vi)) throw Err.ColumnUnknown(name, at.Line, at.Col);
        var v = r.Vars[vi];
        return new Target(r.VarStream, vi, v.Type, v.NotNull, name);
    }

    Target[] Targets(List<Expr> list) => list.Select(ResolveTarget).ToArray();

    PStmt CompilePsql(Stmt st)
    {
        var p = CompilePsqlCore(st);
        return st.Line > 0 ? new LocatedP(p, st.Line, st.Col) : p;
    }

    PStmt CompilePsqlCore(Stmt st)
    {
        var r = Routine!;
        var root = RootScope!;
        switch (st)
        {
            case BlockStmt b:
                return CompileBlock(b);
            case AssignStmt a:
            {
                var t = ResolveTarget(a.Target);
                return new AssignP(t, Bind(a.Value, root, t.Type));
            }
            case IfStmt i:
                return new IfP(BindBool(i.Condition, root), CompilePsql(i.Then), i.Else == null ? null : CompilePsql(i.Else));
            case WhileStmt w:
            {
                var cond = BindBool(w.Condition, root);
                r.Labels.Add(w.Label ?? "");
                try { return new WhileP(cond, CompilePsql(w.Body)) { Label = w.Label }; }
                finally { r.Labels.RemoveAt(r.Labels.Count - 1); }
            }
            case ForSelectStmt f:
            {
                var q = CompileSelect(f.Query, root);
                var into = Targets(f.Into);
                if (into.Length > q.Columns.Length) throw Err.Dsql(-313, "Count of column list and variable list do not match");
                var cursorStream = -1;
                var cursorSlot = -1;
                SourceItem? item = null;
                if (f.CursorName != null)
                {
                    var cols = q.Columns.Select(col => new ColInfo { Name = col.Name, Type = col.Type, Field = col.Field, Relation = col.Relation }).ToList();
                    cursorStream = NewStream(cols.Count);
                    cursorSlot = r.CursorCount++;
                    item = new SourceItem { Alias = f.CursorName, Name = f.CursorName, Stream = cursorStream, Cols = cols, QualifiedOnly = true };
                    root.Add(item);
                }
                r.Labels.Add(f.Label ?? "");
                try
                {
                    var body = CompilePsql(f.Body);
                    return new ForSelectP(q, into, cursorStream, cursorSlot, body) { Label = f.Label };
                }
                finally
                {
                    r.Labels.RemoveAt(r.Labels.Count - 1);
                    if (item != null) root.Items.Remove(item);
                }
            }
            case SelectStmt s:
            {
                var q = CompileSelect(s.Query, root);
                var into = s.Into == null ? null : Targets(s.Into);
                if (into != null && into.Length != q.Columns.Length) throw Err.Dsql(-313, "Count of column list and variable list do not match");
                return new StatementP(new SelectPrepared { Query = q, StmtType = Wire.Isc.stmt_select, Outputs = q.Columns }, into);
            }
            case InsertStmt ins: return new StatementP(CompileInsert(ins, root), IntoOf(ins.Returning));
            case UpdateStmt up: return new StatementP(CompileUpdate(up, root), IntoOf(up.Returning));
            case DeleteStmt del: return new StatementP(CompileDelete(del, root), IntoOf(del.Returning));
            case UpdateOrInsertStmt uoi: return new StatementP(CompileUpdateOrInsert(uoi, root), IntoOf(uoi.Returning));
            case MergeStmt m: return new StatementP(CompileMerge(m, root), IntoOf(m.Returning));
            case ExecProcStmt ep:
                return new StatementP(CompileExecProc(ep, root), ep.ReturningValues == null ? null : Targets(ep.ReturningValues));
            case ExecStatementStmt es:
                return CompileExecStatement(es, null);
            case ForExecStmt fe:
            {
                r.Labels.Add(fe.Label ?? "");
                try
                {
                    var body = CompilePsql(fe.Body);
                    return CompileExecStatement(fe.Exec, body, fe.Label);
                }
                finally { r.Labels.RemoveAt(r.Labels.Count - 1); }
            }
            case SuspendStmt:
                if (r.IsTrigger || r.IsFunction) throw Err.Dsql(-104, "SUSPEND is not allowed in triggers or functions");
                if (r.OutputVars.Length == 0) throw Err.Dsql104(FbMessages.suspend_without_returns);
                r.HasSuspend = true;
                return new SuspendP(r.OutputVars, r.VarStream);
            case ExitStmt:
                return new ExitP();
            case LeaveStmt lv:
                if (lv.Label != null && !r.Labels.Contains(lv.Label)) throw Err.Dsql(-104, "Label " + lv.Label + " is not found");
                if (r.Labels.Count == 0) throw Err.Dsql(-104, "LEAVE/BREAK outside of a loop");
                return new LeaveP(lv.Label, false);
            case ContinueStmt ct:
                if (ct.Label != null && !r.Labels.Contains(ct.Label)) throw Err.Dsql(-104, "Label " + ct.Label + " is not found");
                if (r.Labels.Count == 0) throw Err.Dsql(-104, "CONTINUE outside of a loop");
                return new LeaveP(ct.Label, true);
            case ReturnStmt ret:
                if (!r.IsFunction) throw Err.Dsql(-104, "RETURN is allowed only in functions");
                return new ReturnP(Bind(ret.Value, root, r.FunctionReturns), r.FunctionReturns!);
            case RaiseStmt rs:
            {
                if (rs.Name == null) return new RaiseP(null, null, null);
                var def = Db.Exceptions.TryGetValue(rs.Name, out var x) ? x : throw Err.ObjectUnknown("Exception", rs.Name);
                return new RaiseP(def,
                    rs.Message == null ? null : Bind(rs.Message, root, SqlType.VarChar(1021)),
                    rs.Using?.Select(u => Bind(u, root, SqlType.VarChar(1021))).ToArray());
            }
            case OpenCursorStmt oc:
            {
                var cur = r.Cursors.TryGetValue(oc.Name, out var cc) ? cc : throw Err.Dsql(-504, "Cursor " + oc.Name + " is not found");
                return new OpenCursorP(cur.Query!, cur.Index);
            }
            case FetchCursorStmt fc:
            {
                var cur = r.Cursors.TryGetValue(fc.Name, out var cc) ? cc : throw Err.Dsql(-504, "Cursor " + fc.Name + " is not found");
                return new FetchCursorP(cur.Index, cur.Stream, Targets(fc.Into));
            }
            case CloseCursorStmt cl:
            {
                var cur = r.Cursors.TryGetValue(cl.Name, out var cc) ? cc : throw Err.Dsql(-504, "Cursor " + cl.Name + " is not found");
                return new CloseCursorP(cur.Index);
            }
            case AutonomousStmt au:
                return new AutonomousP(CompilePsql(au.Body));
            case PostEventStmt pe:
                return new ExprP(Bind(pe.Name, root, SqlType.VarChar(127)));
            case NoOpStmt:
                return new NoopP();
            case CallStmt cs:
                return new ExprP(Bind(cs.Call, root, SqlType.VarChar(8191)));
        }
        throw Err.Unsupported("PSQL statement " + st.GetType().Name);
    }

    Target[]? IntoOf(ReturningClause? r) => r?.Into == null ? null : Targets(r.Into);

    // ------------------------------------------------------------------ EXECUTE STATEMENT

    PStmt CompileExecStatement(ExecStatementStmt es, PStmt? loopBody, string? label = null)
    {
        var root = RootScope!;
        var sql = Bind(es.Sql, root, SqlType.VarChar(8191));
        string[]? names = null;
        if (es.ArgNames.Any(n => n != null))
        {
            if (es.ArgNames.Any(n => n == null)) throw Err.Dsql(-104, "Cannot mix named and positional parameters in EXECUTE STATEMENT");
            names = es.ArgNames.Select(n => n!).ToArray();
        }
        var args = es.Args.Select(a => Bind(a, root, SqlType.VarChar(8191))).ToArray();
        var into = es.Into == null ? null : Targets(es.Into);
        return new ExecStatementP(sql, args, names, into, loopBody, es.Autonomous) { Label = label };
    }

    // ------------------------------------------------------------------ sub-routines (FB3 DECLARE PROCEDURE/FUNCTION)

    CompiledRoutine GetSubRoutine(SubRoutineDecl sub)
    {
        var r = Routine!;
        r.CompiledSubRoutines ??= new(StringComparer.Ordinal);
        if (r.CompiledSubRoutines.TryGetValue(sub.Name, out var existing)) return (CompiledRoutine)existing;
        if (sub.Body == null) throw Err.Dsql(-104, "Sub-routine " + sub.Name + " has no body");
        var cr = new CompiledRoutine { Name = sub.Name, Context = $"At sub {(sub.IsFunction ? "function" : "procedure")} '{sub.Name}'" };
        r.CompiledSubRoutines[sub.Name] = cr;
        var comp = new Compiler(Db, 0) { DefaultCharset = DefaultCharset };
        comp.BeginRoutine();
        comp.Routine!.SubRoutines = r.SubRoutines;
        comp.Routine.CompiledSubRoutines = r.CompiledSubRoutines;
        comp.Routine.IsFunction = sub.IsFunction;
        if (sub.IsFunction) comp.Routine.FunctionReturns = comp.ResolveType(sub.Returns!);
        var ins = sub.Inputs.Select(i => comp.Routine.Declare(i.Name, comp.ResolveType(i.Type), i.NotNull)).ToArray();
        var outs = sub.Outputs.Select(o => comp.Routine.Declare(o.Name, comp.ResolveType(o.Type), o.NotNull)).ToArray();
        comp.Routine.OutputVars = outs;
        var body = comp.CompileRoutineBody(sub.Body);
        var done = comp.FinishRoutine(sub.Name, body);
        cr.Streams = done.Streams;
        cr.VarStream = done.VarStream;
        cr.VarCount = done.VarCount;
        cr.VarTypes = done.VarTypes;
        cr.VarNotNull = done.VarNotNull;
        cr.Body = done.Body;
        cr.CursorCount = done.CursorCount;
        cr.Selectable = done.Selectable;
        cr.Inputs = ins;
        cr.Outputs = outs;
        cr.Returns = comp.Routine.FunctionReturns;
        return cr;
    }

    XNode BindSubFunctionCall(SubRoutineDecl sub, FuncExpr f, Scope s)
    {
        var cr = GetSubRoutine(sub);
        var parms = sub.Inputs.Select(i => new RoutineParam { Name = i.Name, Type = ResolveType(i.Type), Default = i.Default }).ToList();
        var args = BindProcArgs(parms, f.Args, s, sub.Name, function: true);
        return new CallFunctionNode { Local = cr, Args = args, Type = cr.Returns ?? ResolveType(sub.Returns!), Name = sub.Name };
    }

    XNode BindFunctionCall(Function fn, FuncExpr f, Scope s)
    {
        var args = BindProcArgs(fn.Inputs, f.Args, s, fn.Name, function: true);
        return new CallFunctionNode { Function = fn, Args = args, Type = fn.Returns, Name = fn.Name };
    }

    JT BuildSubProcRef(SubRoutineDecl sub, NamedTableRef n, Scope s)
    {
        var cr = GetSubRoutine(sub);
        var parms = sub.Inputs.Select(i => new RoutineParam { Name = i.Name, Type = ResolveType(i.Type), Default = i.Default }).ToList();
        var args = BindProcArgs(parms, n.Args ?? new List<Expr>(), s, sub.Name);
        var stream = NewStream(sub.Outputs.Count);
        var rel = new FuncRel(c => Routines.RunSelectable(c, cr, EvalAll(c, args)), stream);
        var item = new SourceItem
        {
            Alias = n.Alias, Name = n.Name, Stream = stream,
            Cols = sub.Outputs.Select(o => new ColInfo { Name = o.Name, Type = ResolveType(o.Type), Relation = sub.Name, Field = o.Name }).ToList(),
        };
        s.Add(item);
        return new JLeaf { Item = item, Fixed = rel };
    }
}

/// <summary>EXECUTE STATEMENT / FOR EXECUTE STATEMENT: dynamic SQL compiled through the plan cache.</summary>
public sealed class ExecStatementP(XNode sql, XNode[] args, string[]? names, Target[]? into, PStmt? body, bool autonomous) : LoopP
{
    public override Flow Exec(Ctx c)
    {
        var text = sql.Eval(c);
        // a NULL text is an empty statement (Unexpected end of command)
        var sqlText = text.IsNull ? "" : Conv.ToStr(text);
        int[]? order = null; // named parameters: the argument used by each positional marker
        if (names != null) (sqlText, order) = NamedParams.Rewrite(sqlText, names);
        var p = Compiler.Prepare(c.Db, sqlText);
        if (p.ParamTypes.Length != (order?.Length ?? args.Length)) throw new StatusBuilder().Gds(FbMessages.eds_input_prm_mismatch).Build();
        if (into != null && p.Outputs.Length != into.Length) throw new StatusBuilder().Gds(FbMessages.eds_output_prm_mismatch).Build();
        var vals = new Value[p.ParamTypes.Length];
        var argVals = new Value[args.Length];
        for (var i = 0; i < argVals.Length; i++) argVals[i] = args[i].Eval(c);
        for (var i = 0; i < vals.Length; i++) vals[i] = Conv.Cast(argVals[order?[i] ?? i], p.ParamTypes[i]);
        var outer = c.Tx;
        var tx = autonomous ? outer.Att.StartTransaction(outer.Snapshot, outer.ReadOnly, outer.Wait) : outer;
        ExecResult r;
        try
        {
            r = Engine.Executor.Run(p, tx, vals, c);
            if (autonomous) tx.Commit();
        }
        catch
        {
            if (autonomous && tx.Active) tx.Rollback();
            throw;
        }
        var rows = r.Rows ?? [];
        // Firebird leaves ROW_COUNT at 0 after a dynamic DML statement
        c.RowCount = p.StmtType == Wire.Isc.stmt_select ? rows.Count : 0;
        if (body == null)
        {
            if (into != null)
            {
                if (rows.Count > 1) throw Err.MultipleRows();
                if (rows.Count == 1)
                    for (var k = 0; k < into.Length; k++) into[k].Set(c, rows[0][k]);
            }
            return Flow.Normal;
        }
        foreach (var row in rows)
        {
            if (into != null)
                for (var k = 0; k < into.Length; k++) into[k].Set(c, row[k]);
            c.Cache = null;
            if (Handle(c, body.Exec(c), out var res)) return res;
        }
        return Flow.Normal;
    }
}

/// <summary>Rewrites the :name markers of EXECUTE STATEMENT ('...') (name := value) into positional markers.</summary>
static class NamedParams
{
    public static (string Sql, int[] Order) Rewrite(string sql, string[] names)
    {
        var sb = new System.Text.StringBuilder(sql.Length);
        var order = new List<int>();
        string? missing = null;
        var i = 0;
        while (i < sql.Length)
        {
            var ch = sql[i];
            if (ch is '\'' or '"')
            {
                var end = sql.IndexOf(ch, i + 1);
                while (end >= 0 && end + 1 < sql.Length && sql[end + 1] == ch) end = sql.IndexOf(ch, end + 2);
                end = end < 0 ? sql.Length : end + 1;
                sb.Append(sql, i, end - i);
                i = end;
            }
            else if (ch == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                var end = sql.IndexOf('\n', i);
                end = end < 0 ? sql.Length : end;
                sb.Append(sql, i, end - i);
                i = end;
            }
            else if (ch == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                var end = sql.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? sql.Length : end + 2;
                sb.Append(sql, i, end - i);
                i = end;
            }
            else if (ch == ':' && i + 1 < sql.Length && (char.IsLetter(sql[i + 1]) || sql[i + 1] == '_'))
            {
                var start = i + 1;
                var end = start;
                while (end < sql.Length && (char.IsLetterOrDigit(sql[end]) || sql[end] is '_' or '$')) end++;
                var name = sql[start..end].ToUpperInvariant();
                var idx = Array.FindIndex(names, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
                if (idx < 0) missing ??= name;
                order.Add(idx);
                sb.Append('?');
                i = end;
            }
            else sb.Append(sql[i++]);
        }
        // every named argument must be used; then every marker must have its argument
        for (var k = 0; k < names.Length; k++)
            if (!order.Contains(k)) throw new StatusBuilder().Gds(FbMessages.eds_input_prm_not_used).Str(names[k]).Build();
        if (missing != null) throw new StatusBuilder().Gds(FbMessages.eds_input_prm_mismatch).Build();
        return (sb.ToString(), order.ToArray());
    }
}
