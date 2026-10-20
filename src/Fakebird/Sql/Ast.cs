using Fakebird.Types;

namespace Fakebird.Sql;

// =====================================================================================
// Expressions
// =====================================================================================

public abstract class Expr
{
    public int Pos;
    /// <summary>Source position (1-based) for errors that report "At line L, column C"; 0 when unknown.</summary>
    public int Line, Col;
}

public sealed class LiteralExpr(Value value, SqlType type) : Expr
{
    /// <summary>A negative number written with a minus sign (a NegateNode of a literal for Firebird).</summary>
    public bool Negated;
    public readonly Value Value = value;
    public readonly SqlType Type = type;
}

/// <summary>Column or variable reference: [qualifier.]name. NEW/OLD qualifiers are trigger contexts.</summary>
public sealed class ColumnRefExpr(string? qualifier, string name) : Expr
{
    public readonly string? Qualifier = qualifier;
    public readonly string Name = name;
    public override string ToString() => Qualifier == null ? Name : Qualifier + "." + Name;
}

public sealed class StarExpr(string? qualifier) : Expr
{
    public readonly string? Qualifier = qualifier;
}

public sealed class ParamExpr(int index) : Expr
{
    public readonly int Index = index;
}

/// <summary>:name inside PSQL.</summary>
public sealed class VariableExpr(string name) : Expr
{
    public readonly string Name = name;
}

public enum UnaryOp { Neg, Plus, Not }

public sealed class UnaryExpr(UnaryOp op, Expr operand) : Expr
{
    public readonly UnaryOp Op = op;
    public readonly Expr Operand = operand;
}

public enum BinaryOp { Add, Sub, Mul, Div, Concat, And, Or, Eq, Ne, Lt, Le, Gt, Ge }

public sealed class BinaryExpr(BinaryOp op, Expr left, Expr right) : Expr
{
    public readonly BinaryOp Op = op;
    public readonly Expr Left = left;
    public readonly Expr Right = right;
}

public enum Quantifier { Any, All }

public sealed class QuantifiedExpr(BinaryOp op, Expr left, Quantifier q, QueryExpr query) : Expr
{
    public readonly BinaryOp Op = op;
    public readonly Expr Left = left;
    public readonly Quantifier Quantifier = q;
    public readonly QueryExpr Query = query;
}

public sealed class IsNullExpr(Expr operand, bool not) : Expr
{
    public readonly Expr Operand = operand;
    public readonly bool Not = not;
}

public sealed class IsDistinctExpr(Expr left, Expr right, bool not) : Expr
{
    public readonly Expr Left = left;
    public readonly Expr Right = right;
    public readonly bool Not = not;
}

/// <summary>expr IS [NOT] TRUE/FALSE/UNKNOWN</summary>
public sealed class IsBoolExpr(Expr operand, bool? value, bool not) : Expr
{
    public readonly Expr Operand = operand;
    public readonly bool? Value = value;
    public readonly bool Not = not;
}

public sealed class BetweenExpr(Expr operand, Expr low, Expr high, bool not) : Expr
{
    public readonly Expr Operand = operand;
    public readonly Expr Low = low;
    public readonly Expr High = high;
    public readonly bool Not = not;
}

public enum LikeKind { Like, StartingWith, Containing, SimilarTo }

public sealed class LikeExpr(LikeKind kind, Expr operand, Expr pattern, Expr? escape, bool not) : Expr
{
    public readonly LikeKind Kind = kind;
    public readonly Expr Operand = operand;
    public readonly Expr Pattern = pattern;
    public readonly Expr? Escape = escape;
    public readonly bool Not = not;
}

public sealed class InListExpr(Expr operand, List<Expr> list, bool not) : Expr
{
    public readonly Expr Operand = operand;
    public readonly List<Expr> List = list;
    public readonly bool Not = not;
}

public sealed class InQueryExpr(Expr operand, QueryExpr query, bool not) : Expr
{
    public readonly Expr Operand = operand;
    public readonly QueryExpr Query = query;
    public readonly bool Not = not;
}

public sealed class ExistsExpr(QueryExpr query, bool singular) : Expr
{
    public readonly QueryExpr Query = query;
    public readonly bool Singular = singular;
}

public sealed class SubqueryExpr(QueryExpr query) : Expr
{
    public readonly QueryExpr Query = query;
}

public sealed class CaseExpr(Expr? operand, List<(Expr When, Expr Then)> whens, Expr? @else) : Expr
{
    public readonly Expr? Operand = operand;
    public readonly List<(Expr When, Expr Then)> Whens = whens;
    public readonly Expr? Else = @else;
}

public sealed class CastExpr(Expr operand, TypeSpec type) : Expr
{
    public readonly Expr Operand = operand;
    public readonly TypeSpec Type = type;
}

public sealed class CollateExpr(Expr operand, string collation) : Expr
{
    public readonly Expr Operand = operand;
    public readonly string Collation = collation;
}

/// <summary>
/// Function call (builtin, aggregate, UDF or PSQL function). Special syntax forms (EXTRACT, SUBSTRING,
/// TRIM, POSITION, DATEADD, ...) are normalized into ordinary argument lists by the parser; keyword-like
/// arguments (date parts, trim specs) are passed as <see cref="KeywordArg"/>.
/// </summary>
public sealed class FuncExpr(string name, List<Expr> args) : Expr
{
    public readonly string Name = name;
    public readonly List<Expr> Args = args;
    public bool Distinct;
    public bool Star;
    public string? Package;
}

/// <summary>A bare keyword used as a function argument (e.g. YEAR in EXTRACT).</summary>
public sealed class KeywordArg(string keyword) : Expr
{
    public readonly string Keyword = keyword;
}

public sealed class WindowSpec
{
    public List<Expr> PartitionBy = new();
    public List<OrderItem> OrderBy = new();
    public FrameSpec? Frame;
}

public enum FrameUnit { Rows, Range }
public enum FrameBoundKind { UnboundedPreceding, Preceding, CurrentRow, Following, UnboundedFollowing }
public sealed record FrameBound(FrameBoundKind Kind, Expr? Offset);
public sealed record FrameSpec(FrameUnit Unit, FrameBound Start, FrameBound End);

public sealed class WindowExpr(FuncExpr func, WindowSpec window) : Expr
{
    public readonly FuncExpr Func = func;
    public readonly WindowSpec Window = window;
}

public sealed class NextValueExpr(string sequence) : Expr
{
    public readonly string Sequence = sequence;
}

/// <summary>CURRENT_USER, CURRENT_TIMESTAMP, ROW_COUNT, SQLCODE, INSERTING, ... </summary>
public sealed class ContextVarExpr(string name, int precision = -1) : Expr
{
    public readonly string Name = name;
    public readonly int Precision = precision;
}

// =====================================================================================
// Types
// =====================================================================================

public sealed class TypeSpec
{
    /// <summary>Name of the column/variable being declared (for "COLUMN name" in limit errors).</summary>
    public string? FieldName;
    /// <summary>Base type keyword (INTEGER, VARCHAR, NUMERIC, BLOB, ...) or null for domain/TYPE OF.</summary>
    public string? BaseType;
    public int? Length;
    public int? Precision;
    public int? Scale;
    public int? SubType;
    public string? Charset;
    public string? Collate;
    public string? Domain;
    /// <summary>TYPE OF COLUMN table.column</summary>
    public string? TypeOfTable;
    public string? TypeOfColumn;
    public bool TypeOf;
    public override string ToString() => BaseType ?? Domain ?? "?";
}

// =====================================================================================
// Queries
// =====================================================================================

public sealed class OrderItem
{
    public Expr Expr = null!;
    public bool Descending;
    public bool? NullsFirst;
}

public abstract class QueryExpr
{
    public int Pos;
}

public sealed class SelectItem
{
    public Expr Expr = null!;
    public string? Alias;
}

public sealed class LimitSpec
{
    public Expr? First;   // FIRST / FETCH / ROWS m TO n (count)
    public Expr? Skip;    // SKIP / OFFSET
    // ROWS m [TO n]
    public Expr? RowsFrom;
    public Expr? RowsTo;
    public bool IsEmpty => First == null && Skip == null && RowsFrom == null;
}

public sealed class QuerySpec : QueryExpr
{
    public bool Distinct;
    public List<SelectItem> Items = new();
    public List<TableRef> From = new();
    public Expr? Where;
    public List<Expr> GroupBy = new();
    public Expr? Having;
    public LimitSpec Limit = new();
    public Dictionary<string, WindowSpec>? NamedWindows;
    /// <summary>PSQL: SELECT ... INTO :vars FROM ...</summary>
    public List<Expr>? Into;
}

public enum SetOpKind { Union, UnionAll }

public sealed class SetOpQuery(SetOpKind kind, QueryExpr left, QueryExpr right) : QueryExpr
{
    public readonly SetOpKind Kind = kind;
    public readonly QueryExpr Left = left;
    public readonly QueryExpr Right = right;
}

/// <summary>A full query: WITH + query expression + ORDER BY + limits.</summary>
public sealed class SelectQuery : QueryExpr
{
    public List<CteDef>? With;
    public bool Recursive;
    public QueryExpr Body = null!;
    public List<OrderItem> OrderBy = new();
    public LimitSpec Limit = new();
    public bool ForUpdate;
    public bool WithLock;
}

public sealed class CteDef
{
    public string Name = null!;
    public List<string>? Columns;
    public SelectQuery Query = null!;
}

public abstract class TableRef
{
    public int Pos;
    public int Line, Col;
}

/// <summary>Table, view, CTE or selectable procedure (when Args != null).</summary>
public sealed class NamedTableRef : TableRef
{
    public string Name = null!;
    public string? Alias;
    public List<Expr>? Args;
}

public sealed class DerivedTableRef : TableRef
{
    public SelectQuery Query = null!;
    public string? Alias;
    public List<string>? Columns;
    public bool Lateral;
}

public enum JoinKind { Inner, Left, Right, Full, Cross }

public sealed class JoinRef : TableRef
{
    public JoinKind Kind;
    public TableRef Left = null!;
    public TableRef Right = null!;
    public Expr? On;
    public List<string>? Using;
    public bool Natural;
}

// =====================================================================================
// Statements
// =====================================================================================

public abstract class Stmt
{
    public int Pos;
    /// <summary>Source position of a PSQL statement (reported in error stack traces).</summary>
    public int Line, Col;
}

public sealed class SelectStmt : Stmt
{
    public SelectQuery Query = null!;
    /// <summary>PSQL: SELECT ... INTO :vars</summary>
    public List<Expr>? Into;
}

public sealed class ReturningClause
{
    public List<SelectItem> Items = new();
    public List<Expr>? Into;
}

public sealed class InsertStmt : Stmt
{
    public string Table = null!;
    public string? Alias;
    public List<string>? Columns;
    public List<Expr>? Values;
    public SelectQuery? Query;
    public bool DefaultValues;
    public ReturningClause? Returning;
    public string? Overriding; // SYSTEM / USER (FB4) - ignored
}

public sealed class Assignment
{
    public ColumnRefExpr Target = null!;
    public Expr Value = null!;
}

public sealed class UpdateStmt : Stmt
{
    public string Table = null!;
    public string? Alias;
    public List<Assignment> Set = new();
    public Expr? Where;
    public string? CurrentOf;
    public List<OrderItem> OrderBy = new();
    public Expr? Rows;
    public Expr? RowsTo;
    public ReturningClause? Returning;
}

public sealed class DeleteStmt : Stmt
{
    public string Table = null!;
    public string? Alias;
    public Expr? Where;
    public string? CurrentOf;
    public List<OrderItem> OrderBy = new();
    public Expr? Rows;
    public Expr? RowsTo;
    public ReturningClause? Returning;
}

public sealed class UpdateOrInsertStmt : Stmt
{
    public string Table = null!;
    public List<string>? Columns;
    public List<Expr> Values = new();
    public List<string>? Matching;
    public ReturningClause? Returning;
}

public sealed class MergeWhen
{
    public bool Matched;
    public Expr? Condition;
    public bool Delete;
    public List<Assignment>? Set;
    public List<string>? InsertColumns;
    public List<Expr>? InsertValues;
}

public sealed class MergeStmt : Stmt
{
    public string Table = null!;
    public string? Alias;
    public TableRef Source = null!;
    public Expr On = null!;
    public List<MergeWhen> Whens = new();
    public ReturningClause? Returning;
}

public sealed class ExecProcStmt : Stmt
{
    public string Name = null!;
    public List<Expr> Args = new();
    /// <summary>PSQL: RETURNING_VALUES :a, :b</summary>
    public List<Expr>? ReturningValues;
}

public sealed class ParamDecl
{
    public string Name = null!;
    public TypeSpec Type = null!;
    public bool NotNull;
    public Expr? Default;
    /// <summary>EXECUTE BLOCK input parameter value (= ?)</summary>
    public Expr? Value;
}

public sealed class ExecBlockStmt : Stmt
{
    public List<ParamDecl> Inputs = new();
    public List<ParamDecl> Outputs = new();
    public BlockStmt Body = null!;
}

// ---- transaction control

public sealed class SetTransactionStmt : Stmt { }
public sealed class CommitStmt : Stmt { public bool Retain; }
public sealed class RollbackStmt : Stmt { public bool Retain; public string? ToSavepoint; }
public sealed class SavepointStmt : Stmt { public string Name = null!; }
public sealed class ReleaseSavepointStmt : Stmt { public string Name = null!; public bool Only; }

// ---- DDL

public enum CreateMode { Create, Alter, CreateOrAlter, Recreate }

public sealed class ColumnDef
{
    public string Name = null!;
    public TypeSpec? Type;
    public Expr? ComputedBy;
    public string? ComputedBySource;
    public Expr? Default;
    public string? DefaultSource;
    public bool NotNull;
    public string? NotNullName;
    public bool Identity;
    public long IdentityStart;
    public int IdentityIncrement = 1;
    public List<ConstraintDef> Constraints = new();
    public string? Collate;
}

public enum ConstraintKind { PrimaryKey, Unique, ForeignKey, Check, NotNull }
public enum RefAction { NoAction, Cascade, SetNull, SetDefault }

public sealed class ConstraintDef
{
    public string? Name;
    public ConstraintKind Kind;
    public List<string> Columns = new();
    public string? RefTable;
    public List<string>? RefColumns;
    public RefAction OnDelete;
    public RefAction OnUpdate;
    public Expr? Check;
    public string? CheckSource;
    public string? IndexName;
    public bool IndexDescending;
    /// <summary>System name (INTEG_n) assigned in Firebird's order before the constraint is created.</summary>
    public string? GeneratedName;
}

public sealed class CreateTableStmt : Stmt
{
    public CreateMode Mode;
    public string Name = null!;
    public List<ColumnDef> Columns = new();
    public List<ConstraintDef> Constraints = new();
    public bool GlobalTemporary;
    public bool OnCommitPreserve;
}

public abstract class AlterTableAction { }
public sealed class AddColumnAction : AlterTableAction { public ColumnDef Column = null!; }
public sealed class DropColumnAction : AlterTableAction { public string Name = null!; }
public sealed class AddConstraintAction : AlterTableAction { public ConstraintDef Constraint = null!; }
public sealed class DropConstraintAction : AlterTableAction { public string Name = null!; }
public sealed class AlterColumnAction : AlterTableAction
{
    public string Name = null!;
    public string? NewName;
    public TypeSpec? NewType;
    public int? Position;
    public bool SetDefault;
    public Expr? Default;
    public string? DefaultSource;
    public bool DropDefault;
    public bool? NotNull;
    public bool RestartIdentity;
    public long? RestartWith;
    public Expr? ComputedBy;
    public string? ComputedBySource;
}

public sealed class AlterTableStmt : Stmt
{
    public string Name = null!;
    public List<AlterTableAction> Actions = new();
}

public sealed class CreateIndexStmt : Stmt
{
    public string Name = null!;
    public string Table = null!;
    public bool Unique;
    public bool Descending;
    public List<string> Columns = new();
    public Expr? ComputedBy;
}

public sealed class AlterIndexStmt : Stmt
{
    public string Name = null!;
    public bool Active;
}

public sealed class CreateViewStmt : Stmt
{
    public CreateMode Mode;
    public string Name = null!;
    public List<string>? Columns;
    public SelectQuery Query = null!;
    public string Source = null!;
    public bool WithCheckOption;
}

public sealed class CreateSequenceStmt : Stmt
{
    public CreateMode Mode;
    public string Name = null!;
    public long? StartWith;
    public bool Restart;
    public int? IncrementBy;
}

public sealed class SetGeneratorStmt : Stmt
{
    public string Name = null!;
    public long Value;
}

public sealed class CreateExceptionStmt : Stmt
{
    public CreateMode Mode;
    public string Name = null!;
    public string Message = null!;
}

public sealed class CreateDomainStmt : Stmt
{
    public string Name = null!;
    public TypeSpec Type = null!;
    public Expr? Default;
    public string? DefaultSource;
    public bool NotNull;
    public Expr? Check;
    public string? CheckSource;
    public string? Collate;
}

public sealed class AlterDomainStmt : Stmt
{
    public string Name = null!;
    public string? NewName;
    public TypeSpec? NewType;
    public bool SetDefault;
    public Expr? Default;
    public bool DropDefault;
    public bool? NotNull;
    public bool DropConstraint;
    public Expr? AddCheck;
}

public sealed class CreateProcedureStmt : Stmt
{
    public CreateMode Mode;
    public string Name = null!;
    public List<ParamDecl> Inputs = new();
    public List<ParamDecl> Outputs = new();
    public BlockStmt Body = null!;
    public string Source = null!;
}

public sealed class CreateFunctionStmt : Stmt
{
    public CreateMode Mode;
    public string Name = null!;
    public List<ParamDecl> Inputs = new();
    public TypeSpec Returns = null!;
    public bool Deterministic;
    public BlockStmt Body = null!;
    public string Source = null!;
}

public enum TriggerTiming { Before, After }

[Flags]
public enum TriggerEvent { None = 0, Insert = 1, Update = 2, Delete = 4 }

public enum DbTriggerEvent { None, Connect, Disconnect, TransactionStart, TransactionCommit, TransactionRollback }

public sealed class CreateTriggerStmt : Stmt
{
    public CreateMode Mode;
    public string Name = null!;
    public string? Table;
    public bool? Active;
    public TriggerTiming Timing;
    public TriggerEvent Events;
    public DbTriggerEvent DbEvent;
    public bool IsDdlTrigger;
    public int? Position;
    public BlockStmt? Body;
    public string Source = null!;
}

public enum ObjectKind { Table, View, Procedure, Function, Trigger, Exception, Sequence, Index, Domain, Package, Role, User, Filter, ExternalFunction }

public sealed class DropStmt : Stmt
{
    public ObjectKind Kind;
    public string Name = null!;
    public bool IfExists;
}

/// <summary>Statements accepted and ignored (GRANT, REVOKE, COMMENT ON, SET STATISTICS, ...).</summary>
public sealed class NoOpStmt : Stmt
{
    public string What = "";
}

public sealed class SetSqlDialectStmt : Stmt { public int Dialect; }

// =====================================================================================
// PSQL
// =====================================================================================

public sealed class BlockStmt : Stmt
{
    public List<PsqlDecl> Declarations = new();
    public List<Stmt> Body = new();
    public List<WhenHandler> Handlers = new();
}

public abstract class PsqlDecl { }

public sealed class VarDecl : PsqlDecl
{
    public string Name = null!;
    public TypeSpec Type = null!;
    public bool NotNull;
    public Expr? Default;
}

public sealed class CursorDecl : PsqlDecl
{
    public string Name = null!;
    public SelectQuery Query = null!;
    public bool Scroll;
}

public sealed class SubRoutineDecl : PsqlDecl
{
    public bool IsFunction;
    public string Name = null!;
    public List<ParamDecl> Inputs = new();
    public List<ParamDecl> Outputs = new();
    public TypeSpec? Returns;
    public BlockStmt? Body; // null = forward declaration
}

public sealed class WhenHandler
{
    /// <summary>Conditions; empty = ANY.</summary>
    public List<WhenCondition> Conditions = new();
    public bool Any;
    public Stmt Body = null!;
}

public sealed record WhenCondition(string Kind, string? Name, int Code);

public sealed class AssignStmt : Stmt
{
    public Expr Target = null!;
    public Expr Value = null!;
}

public sealed class IfStmt : Stmt
{
    public Expr Condition = null!;
    public Stmt Then = null!;
    public Stmt? Else;
}

public sealed class WhileStmt : Stmt
{
    public string? Label;
    public Expr Condition = null!;
    public Stmt Body = null!;
}

public sealed class ForSelectStmt : Stmt
{
    public string? Label;
    public SelectQuery Query = null!;
    public List<Expr> Into = new();
    public string? CursorName;
    public Stmt Body = null!;
}

public sealed class ForExecStmt : Stmt
{
    public string? Label;
    public ExecStatementStmt Exec = null!;
    public Stmt Body = null!;
}

public sealed class ExecStatementStmt : Stmt
{
    public Expr Sql = null!;
    public List<Expr> Args = new();
    public List<string?> ArgNames = new();
    public List<Expr>? Into;
    public bool Autonomous;
}

public sealed class SuspendStmt : Stmt { }
public sealed class ExitStmt : Stmt { }
public sealed class LeaveStmt : Stmt { public string? Label; }
public sealed class ContinueStmt : Stmt { public string? Label; }
public sealed class ReturnStmt : Stmt { public Expr Value = null!; }

public sealed class RaiseStmt : Stmt
{
    /// <summary>null = re-raise current exception.</summary>
    public string? Name;
    public Expr? Message;
    public List<Expr>? Using;
}

public sealed class OpenCursorStmt : Stmt { public string Name = null!; }
public sealed class CloseCursorStmt : Stmt { public string Name = null!; }

public sealed class FetchCursorStmt : Stmt
{
    public string Name = null!;
    public List<Expr> Into = new();
}

public sealed class AutonomousStmt : Stmt { public Stmt Body = null!; }

public sealed class PostEventStmt : Stmt { public Expr Name = null!; }

/// <summary>A function call used as a statement (e.g. RDB$SET_CONTEXT(...);).</summary>
public sealed class CallStmt : Stmt { public Expr Call = null!; }
