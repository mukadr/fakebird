using Fakebird.Sql;
using Fakebird.Engine;
using Fakebird.Types;

namespace Fakebird.Exec;

/// <summary>
/// Execution context of one request (statement, procedure invocation or trigger firing). Current rows of
/// all record streams of the compiled request are kept in <see cref="Rows"/>, indexed by stream number.
/// PSQL variables live in a dedicated (mutable) stream.
/// </summary>
public sealed class Ctx
{
    public Value[][] Rows;
    /// <summary>Slot of the current row per stream (set by table scans that feed DML).</summary>
    public RowSlot?[] Slots;
    public Value[] Params = [];
    public string? FlowLabel;
    public Value ReturnValue;
    public Transaction Tx;
    public Attachment Att => Tx.Att;
    public Database Db => Tx.Db;
    public int RowCount;
    public FbError? CurrentError;
    public TriggerEvent TriggerAction;
    /// <summary>SUSPEND sink for selectable procedures / EXECUTE BLOCK.</summary>
    public List<Value[]>? Output;
    public object?[]? Cursors;
    public int Depth;
    /// <summary>Innermost PSQL routine invocation (for error stack traces).</summary>
    public PsqlFrame? Frame;
    public Dictionary<object, object>? Cache;
    Value _now;
    bool _hasNow;

    public Ctx(Transaction tx, int streams)
    {
        Tx = tx;
        Rows = new Value[streams][];
        Slots = new RowSlot?[streams];
    }

    /// <summary>CURRENT_TIMESTAMP is stable during a request.</summary>
    public Value Now
    {
        get
        {
            if (!_hasNow)
            {
                _now = Conv.Now();
                _hasNow = true;
            }
            return _now;
        }
    }

    public void SetNow(Value now)
    {
        _now = now;
        _hasNow = true;
    }

    /// <summary>Creates a child context (procedure/trigger/function call) sharing transaction and timestamp.</summary>
    public Ctx Child(int streams)
    {
        var c = new Ctx(Tx, streams) { Depth = Depth + 1, Frame = Frame };
        if (_hasNow) c.SetNow(_now);
        if (c.Depth > 1000) throw Err.Random("Too many concurrent executions of the same request (recursion depth exceeded)");
        return c;
    }
}
