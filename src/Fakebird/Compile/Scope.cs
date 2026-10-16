using Fakebird.Engine;
using Fakebird.Exec;
using Fakebird.Sql;
using Fakebird.Types;

namespace Fakebird.Compile;

public sealed class ColInfo
{
    public string Name = null!;
    public SqlType Type = null!;
    public bool NotNull;
    public string Relation = "";
    public string Field = "";
}

/// <summary>A record source visible by name in a query scope.</summary>
public sealed class SourceItem
{
    public string? Alias;
    /// <summary>Underlying object name (table, view, procedure, CTE).</summary>
    public string Name = null!;
    public int Stream;
    public List<ColInfo> Cols = new();
    public Table? Table;
    /// <summary>NEW/OLD/cursor contexts: only resolvable with a qualifier.</summary>
    public bool QualifiedOnly;
    /// <summary>Hidden from '*' expansion.</summary>
    public bool Hidden;
    /// <summary>Columns merged by USING/NATURAL joins: hidden from unqualified lookup and '*'.</summary>
    public HashSet<int>? HiddenCols;
    Dictionary<string, int>? _index;

    public int Find(string col)
    {
        if (_index == null)
        {
            _index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < Cols.Count; i++) _index.TryAdd(Cols[i].Name, i);
        }
        return _index.TryGetValue(col, out var x) ? x : -1;
    }

    public bool Matches(string qualifier) => Alias != null ? Alias == qualifier : Name == qualifier;
}

/// <summary>Aggregation binding state of a grouped query.</summary>
public sealed class AggBinding
{
    public int Stream;
    public readonly List<string> KeyCanon = new();
    public readonly Dictionary<string, int> AggIndex = new(StringComparer.Ordinal);
    public readonly List<AggSpec> Aggs = new();
    public readonly List<FuncExpr> AggExprs = new();
    public readonly HashSet<(int, int)> GroupedColumns = new();
    public int KeyCount;
}

public sealed class Scope
{
    public readonly Scope? Parent;
    public readonly List<SourceItem> Items = new();
    /// <summary>Streams bound by this query level (for frame save/restore).</summary>
    public readonly List<int> Streams = new();
    public bool Correlated;
    public HashSet<int>? OuterStreams;
    /// <summary>Set while binding expressions evaluated after GROUP BY.</summary>
    public AggBinding? Agg;
    public bool AggMode;
    public Dictionary<WindowExpr, XNode>? Windows;

    public Scope(Scope? parent) => Parent = parent;

    public void Add(SourceItem item)
    {
        Items.Add(item);
        if (!Streams.Contains(item.Stream)) Streams.Add(item.Stream);
    }
}

/// <summary>PSQL routine variables / cursors visible to all statements of the routine.</summary>
public sealed class RoutineScope
{
    public int VarStream;
    public readonly List<(string Name, SqlType Type, bool NotNull)> Vars = new();
    public readonly Dictionary<string, int> VarIndex = new(StringComparer.Ordinal);
    public readonly Dictionary<string, (int Index, int Stream, List<ColInfo> Cols, QueryNode? Query)> Cursors = new(StringComparer.Ordinal);
    public readonly List<string> Labels = new();
    public int[] OutputVars = [];
    public bool IsTrigger;
    public bool IsFunction;
    public SqlType? FunctionReturns;
    public bool HasSuspend;
    public int CursorCount;
    public Dictionary<string, SubRoutineDecl>? SubRoutines;
    public Dictionary<string, object>? CompiledSubRoutines;
    public RoutineScope? Parent;

    public int Declare(string name, SqlType type, bool notNull)
    {
        if (VarIndex.ContainsKey(name)) throw Err.Dsql(-637, "Variable " + name + " is already declared");
        var i = Vars.Count;
        Vars.Add((name, type, notNull));
        VarIndex[name] = i;
        return i;
    }
}
