using Fakebird.Sql;
using Fakebird.Types;

namespace Fakebird.Engine;

public sealed class Domain
{
    /// <summary>COMMENT ON: RDB$DESCRIPTION.</summary>
    public string? Description;
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
    /// <summary>RDB$FIELD_POSITION when it is not the column's index: a dropped column leaves a gap.</summary>
    public int? Position;
    /// <summary>COMMENT ON: RDB$DESCRIPTION.</summary>
    public string? Description;
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
    /// <summary>GENERATED ALWAYS AS IDENTITY: explicit values need OVERRIDING SYSTEM VALUE.</summary>
    public bool IdentityAlways;
    public bool IsComputed => ComputedBy != null;
    public Column Clone() => (Column)MemberwiseClone();
}

public sealed class Sequence
{
    /// <summary>COMMENT ON: RDB$DESCRIPTION.</summary>
    public string? Description;
    public string Name = null!;
    public long Value;
    public int Increment = 1;
    public long InitialValue;
    public bool IsIdentity;
    public Sequence Clone() => (Sequence)MemberwiseClone();
}

public sealed class ExceptionDef
{
    public string? Description;
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
    /// <summary>The system triggers that enforce the CHECK on insert and on update (named in its errors).</summary>
    public string? InsertTrigger, UpdateTrigger;
    /// <summary>FOREIGN KEY: the system triggers of its ON UPDATE / ON DELETE actions.</summary>
    public List<string> ActionTriggers = new();
    public Constraint Clone() => (Constraint)MemberwiseClone();
}

public sealed class View
{
    /// <summary>COMMENT ON: RDB$DESCRIPTION.</summary>
    public string? Description;
    public string Name = null!;
    public List<string>? ColumnNames;
    public SelectQuery Query = null!;
    public string Source = null!;
    /// <summary>Resolved output columns (names/types), computed when the view is created.</summary>
    public List<(string Name, SqlType Type)> Columns = new();
    public Dictionary<string, string?>? ColumnDescriptions;
    public View Clone() => (View)MemberwiseClone();
}

public sealed class RoutineParam
{
    public string Name = null!;
    public SqlType Type = null!;
    public bool NotNull;
    public Expr? Default;
    public string? DefaultSource;
    public string? Domain;
}

public sealed class Procedure
{
    /// <summary>COMMENT ON: RDB$DESCRIPTION.</summary>
    public string? Description;
    public string Name = null!;
    public List<RoutineParam> Inputs = new();
    public List<RoutineParam> Outputs = new();
    public BlockStmt Body = null!;
    public string Source = null!;
    /// <summary>Line/column where Source started in its CREATE statement (backups re-parse it there).</summary>
    public int SourceLine = 1, SourceCol = 1;
    /// <summary>Contains SUSPEND (selectable).</summary>
    public bool Selectable;
    // compiled cache (per database instance)
    internal object? Compiled;
    internal int CompiledVersion = -1;
    public Procedure CloneDef() => new()
    {
        Name = Name, Inputs = Inputs, Outputs = Outputs, Body = Body, Source = Source, SourceLine = SourceLine, SourceCol = SourceCol, Selectable = Selectable, Description = Description,
    };
}

public sealed class Function
{
    /// <summary>COMMENT ON: RDB$DESCRIPTION.</summary>
    public string? Description;
    public string Name = null!;
    public List<RoutineParam> Inputs = new();
    public SqlType Returns = null!;
    public BlockStmt Body = null!;
    public string Source = null!;
    /// <summary>Line/column where Source started in its CREATE statement (backups re-parse it there).</summary>
    public int SourceLine = 1, SourceCol = 1;
    public bool Deterministic;
    internal object? Compiled;
    internal int CompiledVersion = -1;
    public Function CloneDef() => new()
    {
        Name = Name, Inputs = Inputs, Returns = Returns, Body = Body, Source = Source, SourceLine = SourceLine, SourceCol = SourceCol, Deterministic = Deterministic, Description = Description,
    };
}

public sealed class Trigger
{
    /// <summary>COMMENT ON: RDB$DESCRIPTION.</summary>
    public string? Description;
    public string Name = null!;
    public string? Table;
    public bool Active = true;
    public TriggerTiming Timing;
    public TriggerEvent Events;
    public DbTriggerEvent DbEvent;
    public int Position;
    public BlockStmt Body = null!;
    public string Source = null!;
    /// <summary>Line/column where Source started in its CREATE statement (backups re-parse it there).</summary>
    public int SourceLine = 1, SourceCol = 1;
    internal object? Compiled;
    internal int CompiledVersion = -1;
    public Trigger CloneDef() => new()
    {
        Name = Name, Table = Table, Active = Active, Timing = Timing, Events = Events, DbEvent = DbEvent,
        Position = Position, Body = Body, Source = Source, SourceLine = SourceLine, SourceCol = SourceCol, Description = Description,
    };
}
