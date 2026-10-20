using Fakebird.Engine;
using Fakebird.Types;

namespace Fakebird.Exec;

public enum Flow { Normal, Leave, Continue, Exit }

public abstract class PStmt
{
    public abstract Flow Exec(Ctx c);
}

/// <summary>A PSQL routine invocation as seen by error stack traces ("At procedure 'P' line: 3, col: 5").</summary>
public sealed class PsqlFrame(string context, PsqlFrame? caller)
{
    public readonly string Context = context;
    public readonly PsqlFrame? Caller = caller;
    /// <summary>Position of the last statement started in this frame (0 when none has a position).</summary>
    public int Line, Col;

    /// <summary>Appends Firebird's isc_stack_trace (innermost frame first) to an error leaving the frame, once.</summary>
    public FbError Trace(FbError e)
    {
        if (e.TraceDone) return e;
        var sb = new System.Text.StringBuilder();
        for (var f = this; f != null; f = f.Caller)
        {
            if (f.Context == "At block" && f.Line == 0) continue;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(f.Context);
            if (f.Line > 0) sb.Append(" line: ").Append(f.Line).Append(", col: ").Append(f.Col);
        }
        var status = new List<StatusArg>(e.Status);
        // a trace started by a system trigger (At trigger 'CHECK_1') goes on with the PSQL frames
        var k = status.FindIndex(a => a.Type == Wire.Isc.arg_gds && a.Number == Err.isc_stack_trace);
        if (sb.Length > 0 && k >= 0 && k + 1 < status.Count)
            status[k + 1] = status[k + 1] with { Text = status[k + 1].Text + "\n" + sb };
        else if (sb.Length > 0)
        {
            status.Add(new StatusArg(Wire.Isc.arg_gds, Err.isc_stack_trace, null));
            status.Add(new StatusArg(Wire.Isc.arg_string, 0, sb.ToString()));
        }
        return new FbError(status, e.SqlState) { UserExceptionName = e.UserExceptionName, TraceDone = true, UndoMark = e.UndoMark };
    }
}

/// <summary>Records the statement's source position in the frame before running it.</summary>
public sealed class LocatedP(PStmt inner, int line, int col) : PStmt
{
    public readonly PStmt Inner = inner;

    public override Flow Exec(Ctx c)
    {
        if (c.Frame is { } f)
        {
            f.Line = line;
            f.Col = col;
        }
        return Inner.Exec(c);
    }
}

/// <summary>Assignment target: a column of a stream (PSQL variable, NEW.field, output parameter).</summary>
public readonly record struct Target(int Stream, int Index, SqlType Type, bool NotNull, string Name)
{
    public void Set(Ctx c, in Value v)
    {
        var x = Conv.Cast(v, Type);
        if (NotNull && x.IsNull) throw Err.Random("variable " + Name + " cannot be NULL");
        c.Rows[Stream][Index] = x;
    }
}

public sealed class CompiledRoutine
{
    public string Name = "";
    public int Streams;
    public int VarStream;
    public int VarCount;
    public int[] Inputs = [];
    public int[] Outputs = [];
    public SqlType[] VarTypes = [];
    public bool[] VarNotNull = [];
    public PStmt Body = null!;
    public int NewStream = -1;
    public int OldStream = -1;
    public int NewWidth;
    public int CursorCount;
    public SqlType? Returns;
    public bool Selectable;
    /// <summary>Stack trace context: "At procedure 'P'", "At trigger 'T'", "At function 'F'", "At sub procedure 'S'"...</summary>
    public string Context = "At block";

    public Ctx NewCtx(Ctx caller)
    {
        var c = caller.Child(Streams);
        c.Frame = new PsqlFrame(Context, caller.Frame);
        c.Rows[VarStream] = new Value[VarCount];
        if (CursorCount > 0) c.Cursors = new object?[CursorCount];
        return c;
    }
}

public sealed class BlockP : PStmt
{
    public (Target Target, XNode? Init)[] Decls = [];
    public PStmt[] Body = [];
    public (Func<FbError, bool> Match, PStmt Handler)[] Handlers = [];

    public override Flow Exec(Ctx c)
    {
        foreach (var (t, init) in Decls)
        {
            c.Cache = null;
            t.Set(c, init == null ? Value.Null : init.Eval(c));
        }
        var mark = c.Tx.Mark;
        try
        {
            return RunBody(c);
        }
        catch (FbError e)
        {
            foreach (var (match, handler) in Handlers)
            {
                if (!match(e)) continue;
                // Firebird undoes the savepoints above the block's own: the failed statement or nested block,
                // while the work of the statements that completed before it stays
                if (e.UndoMark is { } undo) c.Tx.RollbackTo(undo);
                var prev = c.CurrentError;
                c.CurrentError = e;
                try
                {
                    c.Cache = null;
                    return handler.Exec(c);
                }
                finally
                {
                    c.CurrentError = prev;
                }
            }
            e.UndoMark = mark; // unhandled here: the whole block is undone by the handler that catches it
            throw;
        }
    }

    Flow RunBody(Ctx c)
    {
        foreach (var s in Body)
        {
            c.Cache = null;
            var f = s.Exec(c);
            if (f != Flow.Normal) return f;
        }
        return Flow.Normal;
    }
}

public sealed class AssignP(Target target, XNode value) : PStmt
{
    public override Flow Exec(Ctx c)
    {
        target.Set(c, value.Eval(c));
        return Flow.Normal;
    }
}

public sealed class IfP(XNode cond, PStmt then, PStmt? els) : PStmt
{
    public override Flow Exec(Ctx c)
    {
        if (cond.IsTrue(c)) return then.Exec(c);
        return els?.Exec(c) ?? Flow.Normal;
    }
}

public abstract class LoopP : PStmt
{
    public string? Label;

    /// <summary>Returns true when the loop must stop and sets the flow to propagate.</summary>
    protected bool Handle(Ctx c, Flow f, out Flow result)
    {
        result = Flow.Normal;
        switch (f)
        {
            case Flow.Normal: return false;
            case Flow.Exit: result = Flow.Exit; return true;
            case Flow.Leave:
                if (c.FlowLabel == null || c.FlowLabel == Label)
                {
                    c.FlowLabel = null;
                    return true;
                }
                result = Flow.Leave;
                return true;
            case Flow.Continue:
                if (c.FlowLabel == null || c.FlowLabel == Label)
                {
                    c.FlowLabel = null;
                    return false;
                }
                result = Flow.Continue;
                return true;
        }
        return false;
    }
}

public sealed class WhileP(XNode cond, PStmt body) : LoopP
{
    public override Flow Exec(Ctx c)
    {
        while (true)
        {
            c.Cache = null;
            if (!cond.IsTrue(c)) return Flow.Normal;
            if (Handle(c, body.Exec(c), out var r)) return r;
        }
    }
}

public sealed class ForSelectP(QueryNode query, Target[] into, int cursorStream, int cursorSlot, PStmt body) : LoopP
{
    public override Flow Exec(Ctx c)
    {
        var rows = query.ToList(c);
        var cursor = cursorSlot >= 0 ? new CursorState { Rows = rows } : null;
        if (cursor != null) c.Cursors![cursorSlot] = cursor;
        try
        {
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (cursor != null) cursor.Pos = i;
                for (var k = 0; k < into.Length; k++) into[k].Set(c, row[k]);
                if (cursorStream >= 0) c.Rows[cursorStream] = row;
                c.RowCount = 1;
                c.Cache = null;
                if (Handle(c, body.Exec(c), out var r)) return r;
            }
            return Flow.Normal;
        }
        finally
        {
            if (cursor != null) c.Cursors![cursorSlot] = null;
        }
    }
}

public sealed class CursorState
{
    public List<Value[]> Rows = null!;
    public int Pos = -1;
}

public sealed class OpenCursorP(QueryNode query, int slot) : PStmt
{
    public override Flow Exec(Ctx c)
    {
        // opening an open cursor restarts it, like Firebird's Cursor::open
        c.Cursors![slot] = new CursorState { Rows = query.ToList(c) };
        return Flow.Normal;
    }
}

public sealed class FetchCursorP(int slot, int stream, Target[] into) : PStmt
{
    public override Flow Exec(Ctx c)
    {
        var cs = c.Cursors![slot] as CursorState ?? throw new StatusBuilder().Gds(FbMessages.cursor_not_open).Build();
        cs.Pos++;
        if (cs.Pos >= cs.Rows.Count)
        {
            c.RowCount = 0;
            return Flow.Normal;
        }
        var row = cs.Rows[cs.Pos];
        for (var k = 0; k < into.Length; k++) into[k].Set(c, row[k]);
        if (stream >= 0) c.Rows[stream] = row;
        c.RowCount = 1;
        return Flow.Normal;
    }
}

public sealed class CloseCursorP(int slot) : PStmt
{
    public override Flow Exec(Ctx c)
    {
        c.Cursors![slot] = null; // closing a closed cursor does nothing
        return Flow.Normal;
    }
}

/// <summary>Runs a compiled DML/SELECT statement inside PSQL, assigning RETURNING/INTO targets.</summary>
public sealed class StatementP(Prepared stmt, Target[]? into) : PStmt
{
    public override Flow Exec(Ctx c)
    {
        var mark = c.Tx.Mark;
        ExecResult r;
        try { r = stmt.Execute(c); }
        catch (FbError e)
        {
            e.UndoMark ??= mark; // a failed statement is undone as a whole
            throw;
        }
        c.RowCount = r.Inserted + r.Updated + r.Deleted;
        if (stmt.StmtType == Wire.Isc.stmt_select)
        {
            var rows = r.Rows!;
            c.RowCount = rows.Count;
            if (into != null)
            {
                if (rows.Count > 1) throw Err.MultipleRows();
                if (rows.Count == 1)
                    for (var k = 0; k < into.Length; k++) into[k].Set(c, rows[0][k]);
            }
        }
        else if (into != null && r.Rows is { Count: > 0 })
        {
            var row = r.Rows[0];
            for (var k = 0; k < into.Length; k++) into[k].Set(c, row[k]);
        }
        return Flow.Normal;
    }
}

public sealed class SuspendP(int[] outputs, int varStream) : PStmt
{
    public override Flow Exec(Ctx c)
    {
        if (c.Output == null) return Flow.Exit; // executable procedure: SUSPEND acts like EXIT
        var vars = c.Rows[varStream];
        var row = new Value[outputs.Length];
        for (var i = 0; i < row.Length; i++) row[i] = vars[outputs[i]];
        c.Output.Add(row);
        return Flow.Normal;
    }
}

public sealed class ExitP : PStmt
{
    public override Flow Exec(Ctx c) => Flow.Exit;
}

public sealed class LeaveP(string? label, bool isContinue) : PStmt
{
    public override Flow Exec(Ctx c)
    {
        c.FlowLabel = label;
        return isContinue ? Flow.Continue : Flow.Leave;
    }
}

public sealed class ReturnP(XNode value, SqlType type) : PStmt
{
    public override Flow Exec(Ctx c)
    {
        c.ReturnValue = Conv.Cast(value.Eval(c), type);
        return Flow.Exit;
    }
}

public sealed class RaiseP(ExceptionDef? def, XNode? message, XNode[]? args) : PStmt
{
    public override Flow Exec(Ctx c)
    {
        if (def == null)
        {
            // outside a handler there is nothing to re-raise: Firebird does nothing
            if (c.CurrentError == null) return Flow.Normal;
            // a re-raised error gets a second stack trace from the re-raising frame, like Firebird
            throw new FbError(c.CurrentError.Status, c.CurrentError.SqlState) { UserExceptionName = c.CurrentError.UserExceptionName };
        }
        var msg = def.Message;
        if (message != null)
        {
            var v = message.Eval(c);
            msg = v.IsNull ? "" : Conv.ToStr(v);
        }
        else if (args != null)
        {
            // EXCEPTION ... USING: isc_formatted_exception with the message and every argument
            var vals = args.Select(a => a.Eval(c)).Select(v => v.IsNull ? "<null>" : Conv.ToStr(v)).ToArray();
            for (var i = 0; i < vals.Length; i++) msg = msg.Replace("@" + (i + 1), vals[i]);
            var f = Err.UserException(def.Name, def.Number, "", msg, vals);
            f.UserExceptionName = def.Name;
            throw f;
        }
        var e = Err.UserException(def.Name, def.Number, msg);
        e.UserExceptionName = def.Name;
        throw e;
    }
}

public sealed class ExprP(XNode node) : PStmt
{
    public override Flow Exec(Ctx c)
    {
        node.Eval(c);
        return Flow.Normal;
    }
}

public sealed class SeqP(PStmt[] body) : PStmt
{
    public override Flow Exec(Ctx c)
    {
        foreach (var s in body)
        {
            var f = s.Exec(c);
            if (f != Flow.Normal) return f;
        }
        return Flow.Normal;
    }
}

public sealed class NoopP : PStmt
{
    public override Flow Exec(Ctx c) => Flow.Normal;
}

/// <summary>IN AUTONOMOUS TRANSACTION DO ...</summary>
public sealed class AutonomousP(PStmt body) : PStmt
{
    public override Flow Exec(Ctx c)
    {
        var outer = c.Tx;
        var tx = outer.Att.StartTransaction(outer.Snapshot, outer.ReadOnly, outer.Wait);
        c.Tx = tx;
        try
        {
            var f = body.Exec(c);
            tx.Commit();
            return f;
        }
        catch
        {
            if (tx.Active) tx.Rollback();
            throw;
        }
        finally
        {
            c.Tx = outer;
        }
    }
}

/// <summary>Invocation of stored routines and triggers.</summary>
public static class Routines
{
    public static Func<Database, Procedure, CompiledRoutine> CompileProcedure = null!;
    public static Func<Database, Function, CompiledRoutine> CompileFunction = null!;
    public static Func<Database, Trigger, Table, CompiledRoutine> CompileTrigger = null!;

    public static CompiledRoutine Get(Database db, Procedure p)
    {
        if (p.Compiled is CompiledRoutine cr && p.CompiledVersion == db.SchemaVersion) return cr;
        cr = CompileProcedure(db, p);
        p.Compiled = cr;
        p.CompiledVersion = db.SchemaVersion;
        return cr;
    }

    static Value[] EvalArgs(Ctx c, XNode[] args)
    {
        var vals = new Value[args.Length];
        for (var i = 0; i < args.Length; i++) vals[i] = args[i].Eval(c);
        return vals;
    }

    static Ctx Enter(Ctx caller, CompiledRoutine cr, Value[] args)
    {
        var c = cr.NewCtx(caller);
        var vars = c.Rows[cr.VarStream];
        for (var i = 0; i < cr.Inputs.Length; i++)
        {
            var vi = cr.Inputs[i];
            var v = Conv.Cast(args[i], cr.VarTypes[vi]);
            if (cr.VarNotNull[vi] && v.IsNull) throw Err.Random($"parameter {i + 1} of {cr.Name} cannot be NULL");
            vars[vi] = v;
        }
        return c;
    }

    public static List<Value[]> CallSelectable(Ctx caller, Procedure p, XNode[] args)
        => RunSelectable(caller, Get(caller.Db, p), EvalArgs(caller, args));

    public static List<Value[]> RunSelectable(Ctx caller, CompiledRoutine cr, Value[] args)
    {
        var c = Enter(caller, cr, args);
        c.Output = new List<Value[]>();
        Run(c, cr);
        return c.Output;
    }

    /// <summary>EXECUTE PROCEDURE: returns the output parameter values.</summary>
    public static Value[] CallExecutable(Ctx caller, Procedure p, Value[] args) => RunExecutable(caller, Get(caller.Db, p), args);

    public static Value[] RunExecutable(Ctx caller, CompiledRoutine cr, Value[] args)
    {
        var c = Enter(caller, cr, args);
        Run(c, cr);
        var vars = c.Rows[cr.VarStream];
        var outRow = new Value[cr.Outputs.Length];
        for (var i = 0; i < outRow.Length; i++) outRow[i] = vars[cr.Outputs[i]];
        return outRow;
    }

    public static Value CallFunction(Ctx caller, Function f, Value[] args)
    {
        CompiledRoutine cr;
        if (f.Compiled is CompiledRoutine x && f.CompiledVersion == caller.Db.SchemaVersion) cr = x;
        else
        {
            cr = CompileFunction(caller.Db, f);
            f.Compiled = cr;
            f.CompiledVersion = caller.Db.SchemaVersion;
        }
        return RunFunction(caller, cr, args);
    }

    public static Value RunFunction(Ctx caller, CompiledRoutine cr, Value[] args)
    {
        var c = Enter(caller, cr, args);
        c.ReturnValue = Value.Null;
        Run(c, cr);
        return c.ReturnValue;
    }

    public static void Fire(Ctx caller, Trigger t, Table table, Sql.TriggerEvent ev, Value[]? oldRow, Value[]? newRow)
    {
        CompiledRoutine cr;
        if (t.Compiled is CompiledRoutine x && t.CompiledVersion == caller.Db.SchemaVersion) cr = x;
        else
        {
            cr = CompileTrigger(caller.Db, t, table);
            t.Compiled = cr;
            t.CompiledVersion = caller.Db.SchemaVersion;
        }
        var c = cr.NewCtx(caller);
        c.TriggerAction = ev;
        c.Rows[cr.NewStream] = newRow ?? new Value[cr.NewWidth];
        c.Rows[cr.OldStream] = oldRow ?? new Value[cr.NewWidth];
        Run(c, cr);
    }

    static void Run(Ctx c, CompiledRoutine cr)
    {
        try
        {
            cr.Body.Exec(c);
        }
        catch (FbError e) when (!e.TraceDone)
        {
            throw c.Frame!.Trace(e);
        }
    }
}

/// <summary>Call of a stored PSQL function from an expression.</summary>
public sealed class CallFunctionNode : XNode
{
    public Function? Function;
    public CompiledRoutine? Local;
    public XNode[] Args = [];

    public override Value Eval(Ctx c)
    {
        var vals = new Value[Args.Length];
        for (var i = 0; i < vals.Length; i++) vals[i] = Args[i].Eval(c);
        return Function != null ? Routines.CallFunction(c, Function, vals) : Routines.RunFunction(c, Local!, vals);
    }
}
