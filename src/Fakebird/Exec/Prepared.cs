using Fakebird.Types;

namespace Fakebird.Exec;

public sealed class ExecResult
{
    public List<Value[]>? Rows;
    public int Inserted, Updated, Deleted, Selected;
    public static readonly ExecResult Empty = new();
}

/// <summary>A compiled statement ready to execute (any number of times) in a context.</summary>
public abstract class Prepared
{
    public int StmtType;
    public OutCol[] Outputs = [];
    public Types.SqlType[] ParamTypes = [];
    public int StreamCount;
    /// <summary>Schema version the statement was compiled against.</summary>
    public int Version;
    /// <summary>A DML RETURNING cursor: the statement runs at execute, so its errors are not left for the fetch.</summary>
    public bool ReturningCursor;
    public abstract ExecResult Execute(Ctx c);
}

public sealed class SelectPrepared : Prepared
{
    public QueryNode Query = null!;

    public override ExecResult Execute(Ctx c)
    {
        var rows = Query.ToList(c);
        return new ExecResult { Rows = rows, Selected = rows.Count };
    }
}

public sealed class DelegatePrepared(Func<Ctx, ExecResult> run) : Prepared
{
    public override ExecResult Execute(Ctx c) => run(c);
}
