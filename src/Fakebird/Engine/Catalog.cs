using Fakebird.Sql;
using Fakebird.Types;

namespace Fakebird.Engine;

public sealed class Domain
{
    public string Name = null!;
    public SqlType Type = null!;
    public Expr? Default;
    public string? DefaultSource;
    public bool NotNull;
    public Expr? Check;
    public string? CheckSource;
    public Domain Clone() => (Domain)MemberwiseClone();
}

public sealed class Column
{
    public string Name = null!;
    public SqlType Type = null!;
    public bool NotNull;
    public string? NotNullConstraint;
    public Expr? Default;
    public string? DefaultSource;
    public string? Domain;
    public Expr? ComputedBy;
    public string? ComputedBySource;
    /// <summary>Identity: name of the backing sequence.</summary>
    public string? IdentitySequence;
    public bool IsComputed => ComputedBy != null;
    public Column Clone() => (Column)MemberwiseClone();
}

public sealed class Sequence
{
    public string Name = null!;
    public long Value;
    public int Increment = 1;
    public long InitialValue;
    public bool IsIdentity;
    public Sequence Clone() => (Sequence)MemberwiseClone();
}

public sealed class ExceptionDef
{
    public string Name = null!;
    public string Message = null!;
    public int Number;
}

public enum ConstraintType { PrimaryKey, Unique, ForeignKey, Check, NotNull }

public sealed class Constraint
{
    public string Name = null!;
    public ConstraintType Type;
    public string Table = null!;
    public int[] Columns = [];
    public string? IndexName;
    // FK
    public string? RefTable;
    public string? RefConstraint;
    public int[]? RefColumns;
    public RefAction OnDelete;
    public RefAction OnUpdate;
    // CHECK
    public Expr? Check;
    public string? CheckSource;
    public Constraint Clone() => (Constraint)MemberwiseClone();
}

public sealed class View
{
    public string Name = null!;
    public List<string>? ColumnNames;
    public SelectQuery Query = null!;
    public string Source = null!;
    /// <summary>Resolved output columns (names/types), computed when the view is created.</summary>
    public List<(string Name, SqlType Type)> Columns = new();
    public View Clone() => (View)MemberwiseClone();
}

public sealed class RoutineParam
{
    public string Name = null!;
    public SqlType Type = null!;
    public bool NotNull;
    public Expr? Default;
    public string? Domain;
}

public sealed class Procedure
{
    public string Name = null!;
    public List<RoutineParam> Inputs = new();
    public List<RoutineParam> Outputs = new();
    public BlockStmt Body = null!;
    public string Source = null!;
    /// <summary>Contains SUSPEND (selectable).</summary>
    public bool Selectable;
    // compiled cache (per database instance)
    internal object? Compiled;
    internal int CompiledVersion = -1;
    public Procedure CloneDef() => new()
    {
        Name = Name, Inputs = Inputs, Outputs = Outputs, Body = Body, Source = Source, Selectable = Selectable,
    };
}

public sealed class Function
{
    public string Name = null!;
    public List<RoutineParam> Inputs = new();
    public SqlType Returns = null!;
    public BlockStmt Body = null!;
    public string Source = null!;
    public bool Deterministic;
    internal object? Compiled;
    internal int CompiledVersion = -1;
    public Function CloneDef() => new()
    {
        Name = Name, Inputs = Inputs, Returns = Returns, Body = Body, Source = Source, Deterministic = Deterministic,
    };
}

public sealed class Trigger
{
    public string Name = null!;
    public string? Table;
    public bool Active = true;
    public TriggerTiming Timing;
    public TriggerEvent Events;
    public DbTriggerEvent DbEvent;
    public int Position;
    public BlockStmt Body = null!;
    public string Source = null!;
    internal object? Compiled;
    internal int CompiledVersion = -1;
    public Trigger CloneDef() => new()
    {
        Name = Name, Table = Table, Active = Active, Timing = Timing, Events = Events, DbEvent = DbEvent,
        Position = Position, Body = Body, Source = Source,
    };
}
