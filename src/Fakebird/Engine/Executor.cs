using Fakebird.Compile;
using Fakebird.Exec;
using Fakebird.Types;

namespace Fakebird.Engine;

/// <summary>Runs prepared statements with statement-level atomicity. Callers hold the database lock.</summary>
public static class Executor
{
    static Executor()
    {
        Dml.GetRuntime = GetRuntime;
        Dml.FireTrigger = Routines.Fire;
        Routines.CompileProcedure = Compiler.CompileProcedure;
        Routines.CompileFunction = Compiler.CompileFunction;
        Routines.CompileTrigger = Compiler.CompileTrigger;
    }

    /// <summary>Forces static wiring of the engine services.</summary>
    public static void Init() { }

    static TableRuntime GetRuntime(Database db, Table t)
    {
        if (db.Runtimes.TryGetValue(t.Name, out var rt) && rt.Version == db.SchemaVersion && ReferenceEquals(rt.Table, t)) return rt;
        rt = Compiler.BuildRuntime(db, t);
        db.Runtimes[t.Name] = rt;
        return rt;
    }

    public static Prepared Prepare(Database db, string sql)
    {
        Init();
        return Compiler.Prepare(db, sql);
    }

    /// <summary>Executes a statement; on failure, undoes its partial effects and rethrows.</summary>
    public static ExecResult Run(Prepared p, Transaction tx, Value[] parameters, Ctx? parent = null)
    {
        Init();
        var db = tx.Db;
        if (!tx.Active) throw Err.Random("transaction is not active");
        var c = parent != null ? parent.Child(p.StreamCount) : new Ctx(tx, p.StreamCount);
        c.Tx = tx;
        c.Params = parameters;
        var mark = tx.Mark;
        db.ExecDepth++;
        try
        {
            var r = p.Execute(c);
            if (parent == null && tx.Active) DeferredChecks.Run(c);
            return r;
        }
        catch (FbError)
        {
            if (parent == null) tx.DeferredRefChecks = null;
            if (tx.Active) tx.RollbackTo(mark);
            throw;
        }
        finally
        {
            db.ExecDepth--;
        }
    }

    /// <summary>Convenience: prepare + execute.</summary>
    public static ExecResult Execute(Transaction tx, string sql, params Value[] parameters)
    {
        var p = Prepare(tx.Db, sql);
        if (p.ParamTypes.Length != parameters.Length)
            throw Err.Random($"Expected {p.ParamTypes.Length} parameters, got {parameters.Length}");
        var vals = new Value[parameters.Length];
        for (var i = 0; i < vals.Length; i++) vals[i] = Conv.Cast(parameters[i], p.ParamTypes[i]);
        return Run(p, tx, vals);
    }
}
