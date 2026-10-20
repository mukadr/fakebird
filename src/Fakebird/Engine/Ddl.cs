using Fakebird.Compile;
using Fakebird.Exec;
using Fakebird.Sql;
using Fakebird.Types;

namespace Fakebird.Engine;

/// <summary>Executes DDL statements against the catalog (immediately, not transactional).</summary>
public static class Ddl
{
    public static void Execute(Database db, Stmt stmt, Ctx c)
    {
        try
        {
            Run(db, stmt, c);
        }
        catch (FbError e) when (FailedCode(stmt) is var (code, name) && code != 0)
        {
            throw Wrap(e, code, name);
        }
    }

    /// <summary>Firebird reports DDL errors as "unsuccessful metadata update / CREATE TABLE T failed / cause".</summary>
    static FbError Wrap(FbError e, int failedCode, string name)
    {
        var inner = e.Status.ToList();
        if (inner.Count > 0 && inner[0].Number == Err.isc_no_meta_update) inner.RemoveAt(0);
        if (inner.Count > 0 && FbMessages.ByCode.TryGetValue(inner[0].Number, out var m) && m.Symbol.EndsWith("_failed", StringComparison.Ordinal))
            return e; // already wrapped (nested DDL)
        var status = new List<StatusArg>
        {
            new(Wire.Isc.arg_gds, Err.isc_no_meta_update, null),
            new(Wire.Isc.arg_gds, failedCode, null),
            new(Wire.Isc.arg_string, 0, name),
        };
        status.AddRange(inner);
        return new FbError(status, SqlCodes.SqlStateOf(status, e.SqlState)) { UserExceptionName = e.UserExceptionName };
    }

    static (int Code, string Name) FailedCode(Stmt stmt)
    {
        var (verb, obj, name) = stmt switch
        {
            CreateTableStmt s => (Verb(s.Mode), "table", s.Name),
            AlterTableStmt s => ("alter", "table", s.Name),
            CreateIndexStmt s => ("create", "index", s.Name),
            AlterIndexStmt s => ("alter", "index", s.Name),
            CreateViewStmt s => (Verb(s.Mode), "view", s.Name),
            CreateSequenceStmt s => (Verb(s.Mode), "sequence", s.Name),
            CreateExceptionStmt s => (Verb(s.Mode), "except", s.Name),
            CreateDomainStmt s => ("create", "domain", s.Name),
            AlterDomainStmt s => ("alter", "domain", s.Name),
            CreateProcedureStmt s => (Verb(s.Mode), "proc", s.Name),
            CreateFunctionStmt s => (Verb(s.Mode), "func", s.Name),
            CreateTriggerStmt s => (Verb(s.Mode), "trigger", s.Name),
            DropStmt s => ("drop", s.Kind switch
            {
                ObjectKind.Table => "table", ObjectKind.View => "view", ObjectKind.Procedure => "proc",
                ObjectKind.Function => "func", ObjectKind.Trigger => "trigger", ObjectKind.Exception => "except",
                ObjectKind.Sequence => "sequence", ObjectKind.Index => "index", ObjectKind.Domain => "domain",
                _ => "",
            }, s.Name),
            _ => ("", "", ""),
        };
        var symbol = $"dsql_{verb}_{obj}_failed";
        return FbMessages.ByCode.Any(kv => kv.Value.Symbol == symbol) ? (SqlCodes.GdsCodeByName(symbol), name) : (0, "");
    }

    static string Verb(CreateMode m) => m switch
    {
        CreateMode.Alter => "alter",
        CreateMode.CreateOrAlter => "create_alter",
        CreateMode.Recreate => "recreate",
        _ => "create",
    };

    static void Run(Database db, Stmt stmt, Ctx c)
    {
        switch (stmt)
        {
            case CreateTableStmt ct: CreateTable(db, ct, c); break;
            case AlterTableStmt at: AlterTable(db, at, c); break;
            case CreateIndexStmt ci: CreateIndex(db, ci, c); break;
            case AlterIndexStmt ai: AlterIndex(db, ai); break;
            case CreateViewStmt cv: CreateView(db, cv); break;
            case CreateSequenceStmt cs: CreateSequence(db, cs); break;
            case CreateExceptionStmt ce: CreateException(db, ce); break;
            case CreateDomainStmt cd: CreateDomain(db, cd); break;
            case AlterDomainStmt ad: AlterDomain(db, ad, c); break;
            case CommentStmt cm: Comment(db, cm); break;
            case CreateProcedureStmt cp: CreateProcedure(db, cp); break;
            case CreateFunctionStmt cf: CreateFunction(db, cf); break;
            case CreateTriggerStmt tr: CreateTrigger(db, tr); break;
            case DropStmt d: Drop(db, d); break;
            default: throw Err.Unsupported("statement " + stmt.GetType().Name);
        }
        db.SchemaChanged();
    }

    /// <summary>An object that already exists, as Firebird's DDL reports it.</summary>
    static FbError Exists(string kind, string name) => kind switch
    {
        "Table" => new StatusBuilder().Gds(FbMessages.dyn_dup_table).Str(name).Build(),
        "Index" => new StatusBuilder().Gds(FbMessages.dyn_dup_index).Str(name).Build(),
        "Procedure" => new StatusBuilder().Gds(FbMessages.dyn_dup_procedure).Str(name).Build(),
        "Function" => new StatusBuilder().Gds(FbMessages.dyn_dup_function).Str(name).Build(),
        "Exception" => new StatusBuilder().Gds(FbMessages.dyn_dup_exception).Str(name).Build(),
        "Sequence" => new StatusBuilder().Gds(FbMessages.dyn_dup_generator).Str(name).Build(),
        // the unique index on the system table's name column
        "Domain" => Err.Unique("RDB$INDEX_2", "RDB$FIELDS", $"(\"RDB$FIELD_NAME\" = '{name}')"),
        _ => new StatusBuilder().Gds(Err.isc_no_meta_update).Gds(Err.isc_random).Str($"{kind} {name} already exists")
            .Build(kind is "View" ? "42S01" : "42000"),
    };

    /// <summary>An object that does not exist, as Firebird's DDL reports it.</summary>
    static FbError Missing(string kind, string name) => kind switch
    {
        "Table" => new StatusBuilder().Gds(Err.isc_sqlerr).Num(-607).Gds(FbMessages.dsql_command_err).Gds(FbMessages.dsql_table_not_found).Str(name).Build(),
        "View" => new StatusBuilder().Gds(Err.isc_sqlerr).Num(-607).Gds(FbMessages.dsql_command_err).Gds(FbMessages.dsql_view_not_found).Str(name).Build(),
        "Procedure" => new StatusBuilder().Gds(FbMessages.dyn_proc_not_found).Str(name).Build(),
        "Function" => new StatusBuilder().Gds(FbMessages.dyn_func_not_found).Str(name).Build(),
        "Trigger" => new StatusBuilder().Gds(FbMessages.dyn_trig_not_found).Str(name).Build(),
        "Sequence" => new StatusBuilder().Gds(FbMessages.gennotdef).Str(name).Build(),
        "Exception" => new StatusBuilder().Gds(FbMessages.dyn_exception_not_found).Build(),
        "Domain" => new StatusBuilder().Gds(FbMessages.dyn_domain_not_found).Build(),
        "Index" => new StatusBuilder().Gds(FbMessages.dyn_index_not_found).Build(),
        _ => Err.MetaUpdate($"{kind} {name} not found"),
    };

    static FbError ColumnMissing(Table t, string name)
        => new StatusBuilder().Gds(FbMessages.dyn_column_does_not_exist).Str(name).Str(t.Name).Build();

    /// <summary>A column added twice: the unique index on RDB$RELATION_FIELDS.</summary>
    static FbError ColumnExists(string table, string name)
        => Err.Unique("RDB$INDEX_15", "RDB$RELATION_FIELDS", $"(\"RDB$FIELD_NAME\" = '{name}', \"RDB$RELATION_NAME\" = '{table}')");

    static void CheckName(Database db, string name)
    {
        if (db.Tables.ContainsKey(name)) throw Exists("Table", name);
        if (db.Views.ContainsKey(name)) throw Exists("View", name);
    }

    public static void SetGenerator(Database db, SetGeneratorStmt sg)
    {
        if (!db.Sequences.TryGetValue(sg.Name, out var s)) throw Err.ObjectUnknown("Generator/Sequence", sg.Name);
        s.Value = sg.Value;
    }

    // ======================================================================== tables

    static Compiler Comp(Database db) => new(db, 0);

    static Column MakeColumn(Database db, Table t, ColumnDef def)
    {
        var col = new Column { Name = def.Name };
        if (def.ComputedBy != null)
        {
            col.ComputedBy = def.ComputedBy;
            col.ComputedBySource = def.ComputedBySource;
            if (def.Type != null) col.Type = Comp(db).ResolveType(def.Type);
            return col;
        }
        var comp = Comp(db);
        col.Type = comp.ResolveType(def.Type!, out var domain);
        if (domain != null)
        {
            col.Domain = domain.Name;
            col.NotNull = domain.NotNull;
        }
        if (def.Collate != null && col.Type.IsTextual) col.Type = col.Type.WithCollation(Compiler.CollationOf(def.Collate));
        if (def.NotNull) col.NotNull = true;
        if (def.Default != null)
        {
            col.Default = def.Default;
            col.DefaultSource = def.DefaultSource;
            // the expression must bind, but like Firebird a default that does not convert to the column type
            // (TIMESTAMP DEFAULT 0) only fails when an insert uses it
            comp.Bind(def.Default, new Scope(null), col.Type);
        }
        if (def.Identity)
        {
            if (!col.Type.IsExact || col.Type.Scale > 0) throw Err.MetaUpdate("Identity column " + def.Name + " must be of exact number type with zero scale");
            var seqName = db.NewGeneratorName();
            while (db.Sequences.ContainsKey(seqName)) seqName = db.NewGeneratorName();
            db.Sequences[seqName] = new Sequence
            {
                // Firebird 4+: START WITH is the first value generated
                Name = seqName, Value = def.IdentityStart - def.IdentityIncrement, InitialValue = def.IdentityStart,
                Increment = def.IdentityIncrement, IsIdentity = true,
            };
            col.IdentitySequence = seqName;
            col.IdentityAlways = def.IdentityAlways;
            col.NotNull = true; // identity columns are implicitly NOT NULL
        }
        return col;
    }

    static void CreateTable(Database db, CreateTableStmt st, Ctx c)
    {
        if (st.Mode == CreateMode.Recreate && db.Tables.ContainsKey(st.Name))
            Drop(db, new DropStmt { Kind = ObjectKind.Table, Name = st.Name });
        if (st.Mode is CreateMode.Alter or CreateMode.CreateOrAlter) throw Err.Unsupported("CREATE OR ALTER TABLE");
        CheckName(db, st.Name);
        if (st.Name.StartsWith("RDB$", StringComparison.Ordinal) || st.Name.StartsWith("MON$", StringComparison.Ordinal))
            throw Err.MetaUpdate("Cannot create a system table " + st.Name);
        var t = new Table { Name = st.Name, IsGtt = st.GlobalTemporary, GttPreserveRows = st.OnCommitPreserve };
        foreach (var cd in st.Columns)
        {
            if (t.FindColumn(cd.Name) >= 0) throw ColumnExists(st.Name, cd.Name);
            var col = MakeColumn(db, t, cd);
            NameColumnConstraints(db, col, cd);
            t.Columns.Add(col);
            t.RebuildColumnIndex();
        }
        db.Tables[t.Name] = t;
        try
        {
            InferComputedTypes(db, t);
            foreach (var cd in st.Columns)
                foreach (var con in cd.Constraints)
                    AddConstraint(db, t, con, c, validate: false);
            foreach (var con in st.Constraints) AddConstraint(db, t, con, c, validate: false);
        }
        catch
        {
            DropTableObjects(db, t);
            db.Tables.Remove(t.Name);
            throw;
        }
    }

    static void InferComputedTypes(Database db, Table t)
    {
        for (var i = 0; i < t.Columns.Count; i++)
        {
            var col = t.Columns[i];
            if (col.ComputedBy == null || col.Type != null) continue;
            var comp = Comp(db);
            var st = comp.NewStream(t.Columns.Count);
            var s = new Scope(null);
            s.Add(new SourceItem
            {
                Name = t.Name, Stream = st, Table = t,
                Cols = t.Columns.Select((cc, k) => new ColInfo { Name = cc.Name, Type = cc.Type ?? SqlType.VarChar(1), Relation = t.Name, Field = cc.Name }).ToList(),
            });
            var node = comp.Bind(col.ComputedBy, s);
            col.Type = node.Type.Kind == TypeKind.Null ? SqlType.VarChar(1) : node.Type;
        }
    }

    static string ConstraintName(Database db, ConstraintDef def) => def.GeneratedName ?? ConstraintName(db, def.Name);

    static string ConstraintName(Database db, string? name)
    {
        if (name != null)
        {
            if (db.Constraints.ContainsKey(name))
                throw Err.Unique("RDB$INDEX_12", "RDB$RELATION_CONSTRAINTS", $"(\"RDB$CONSTRAINT_NAME\" = '{name}')");
            return name;
        }
        string n;
        do n = db.NewConstraintName(); while (db.Constraints.ContainsKey(n) || db.Tables.Values.Any(t => t.Columns.Any(c => c.NotNullConstraint == n)));
        return n;
    }

    /// <summary>
    /// Names a column's constraints like Firebird's defineField: an explicit NOT NULL or a column-level PRIMARY KEY
    /// (not on identity columns) creates a NOT NULL constraint first, then the column constraints follow in order.
    /// </summary>
    static void NameColumnConstraints(Database db, Column col, ColumnDef def)
    {
        if ((def.NotNull || def.Constraints.Any(k => k.Kind == ConstraintKind.PrimaryKey)) && !def.Identity)
            col.NotNullConstraint = ConstraintName(db, def.NotNullName);
        foreach (var con in def.Constraints)
            if (con.Kind != ConstraintKind.NotNull)
                con.GeneratedName = ConstraintName(db, con.Name);
    }

    static string IndexName(Database db, string? requested, string prefix)
    {
        if (requested != null)
        {
            if (db.Indexes.ContainsKey(requested)) throw new StatusBuilder().Gds(FbMessages.dyn_dup_index).Str(requested).Build();
            return requested;
        }
        string n;
        do n = db.NewIndexName(prefix); while (db.Indexes.ContainsKey(n));
        return n;
    }

    static int[] Cols(Table t, List<string> names) => names.Select(n =>
    {
        var i = t.FindColumn(n);
        if (i < 0) throw Err.ColumnUnknown(t.Name + "." + n);
        return i;
    }).ToArray();

    static Index MakeIndex(Database db, Table t, string name, int[] cols, bool unique, bool desc, string? constraint)
    {
        var ix = new Index
        {
            Name = name, Table = t.Name, Columns = cols, Unique = unique, Descending = desc, Constraint = constraint,
            Collations = cols.Select(i => t.Columns[i].Type.Collation).ToArray(),
        };
        db.EnsureOwned(t);
        ix.Rebuild(t.Rows);
        db.Indexes[name] = ix;
        t.Indexes.Add(name);
        return ix;
    }

    static void AddConstraint(Database db, Table t, ConstraintDef def, Ctx c, bool validate)
    {
        switch (def.Kind)
        {
            case ConstraintKind.PrimaryKey:
            case ConstraintKind.Unique:
            {
                var isPk = def.Kind == ConstraintKind.PrimaryKey;
                if (isPk && t.PrimaryKey != null)
                    throw new StatusBuilder().Gds(FbMessages.integ_fail).Num(3).Gds(FbMessages.primary_key_exists).Build();
                var cols = Cols(t, def.Columns);
                foreach (var other in new[] { t.PrimaryKey }.Concat(t.Uniques))
                    if (other != null && db.Constraints[other].Columns.Order().SequenceEqual(cols.Order()))
                        throw new StatusBuilder().Gds(FbMessages.dyn_126).Build();
                // a named constraint gives its name to its index (checked before the constraint's name)
                def.IndexName ??= def.Name;
                if (def.IndexName != null && db.Indexes.ContainsKey(def.IndexName))
                    throw new StatusBuilder().Gds(FbMessages.dyn_dup_index).Str(def.IndexName).Build();
                var name = ConstraintName(db, def);
                if (isPk)
                    foreach (var ci in cols)
                    {
                        if (validate && AnyRow(db, t, c, r => r[ci].IsNull))
                            throw Err.MetaUpdate($"Column {t.Columns[ci].Name} contains NULL values: cannot make it a primary key");
                        t.Columns[ci].NotNull = true;
                    }
                var ixName = IndexName(db, def.IndexName, isPk ? "RDB$PRIMARY" : "RDB$");
                var ix = MakeIndex(db, t, ixName, cols, true, def.IndexDescending, name);
                if (validate) ValidateUnique(db, t, ix, c, name);
                db.Constraints[name] = new Constraint
                {
                    Name = name, Type = isPk ? ConstraintType.PrimaryKey : ConstraintType.Unique, Table = t.Name, Columns = cols, IndexName = ixName,
                };
                if (isPk) t.PrimaryKey = name;
                else t.Uniques.Add(name);
                break;
            }
            case ConstraintKind.ForeignKey:
            {
                var cols = Cols(t, def.Columns);
                def.IndexName ??= def.Name;
                var parent = def.RefTable == t.Name ? t : db.Tables.TryGetValue(def.RefTable!, out var p) ? p : throw Err.TableUnknown(def.RefTable!);
                int[] refCols;
                Constraint? target = null;
                if (def.RefColumns == null)
                {
                    if (parent.PrimaryKey == null) throw Err.MetaUpdate($"could not find UNIQUE or PRIMARY KEY constraint in table {parent.Name} with specified columns");
                    target = db.Constraints[parent.PrimaryKey];
                    refCols = target.Columns;
                }
                else
                {
                    refCols = Cols(parent, def.RefColumns);
                    foreach (var cn in new[] { parent.PrimaryKey }.Concat(parent.Uniques))
                    {
                        if (cn == null) continue;
                        var con = db.Constraints[cn];
                        if (con.Columns.SequenceEqual(refCols)) { target = con; break; }
                    }
                    if (target == null) throw Err.MetaUpdate($"could not find UNIQUE or PRIMARY KEY constraint in table {parent.Name} with specified columns");
                }
                if (refCols.Length != cols.Length) throw Err.MetaUpdate("Foreign key column count does not match");
                var name = ConstraintName(db, def);
                var ixName = IndexName(db, def.IndexName, "RDB$FOREIGN");
                MakeIndex(db, t, ixName, cols, false, def.IndexDescending, name);
                var fk = new Constraint
                {
                    Name = name, Type = ConstraintType.ForeignKey, Table = t.Name, Columns = cols, IndexName = ixName,
                    RefTable = parent.Name, RefConstraint = target.Name, RefColumns = refCols, OnDelete = def.OnDelete, OnUpdate = def.OnUpdate,
                };
                db.Constraints[name] = fk;
                // each referential action is a system trigger in Firebird: it takes a CHECK_n name
                if (def.OnUpdate != RefAction.NoAction) fk.ActionTriggers.Add(db.NewCheckTriggerName());
                if (def.OnDelete != RefAction.NoAction) fk.ActionTriggers.Add(db.NewCheckTriggerName());
                t.ForeignKeys.Add(name);
                parent.ReferencedBy.Add(name);
                if (validate)
                {
                    var parentIx = db.Indexes[target.IndexName!];
                    foreach (var row in VisibleRows(t, c))
                    {
                        var vals = cols.Select(ci => row[ci]).ToArray();
                        if (vals.Any(v => v.IsNull)) continue;
                        var key = parentIx.MakeKey(vals.Select((v, i) => Conv.Cast(v, parent.Columns[refCols[i]].Type)).ToArray());
                        var found = parentIx.Lookup(key)?.Any(s => parentIx.RowHasKey(s.Visible(c.Tx), key)) ?? false;
                        if (!found)
                        {
                            RemoveConstraint(db, t, name);
                            throw Err.ForeignKeyMissing(name, t.Name, Dml.KeyText(t, cols, row));
                        }
                    }
                }
                break;
            }
            case ConstraintKind.Check:
            {
                var name = ConstraintName(db, def);
                var con = new Constraint
                {
                    Name = name, Type = ConstraintType.Check, Table = t.Name, Check = def.Check, CheckSource = def.CheckSource,
                    InsertTrigger = db.NewCheckTriggerName(), UpdateTrigger = db.NewCheckTriggerName(),
                };
                // validate expression compiles
                var comp = Comp(db);
                var st = comp.NewStream(t.Columns.Count);
                var s = new Scope(null);
                s.Add(new SourceItem
                {
                    Name = t.Name, Stream = st, Table = t,
                    Cols = t.Columns.Select(cc => new ColInfo { Name = cc.Name, Type = cc.Type, Relation = t.Name, Field = cc.Name }).ToList(),
                });
                // the expression must bind; like Firebird, the rows already in the table are not checked
                comp.BindBool(def.Check!, s);
                db.Constraints[name] = con;
                t.Checks.Add(name);
                break;
            }
            case ConstraintKind.NotNull:
                foreach (var ci in Cols(t, def.Columns)) t.Columns[ci].NotNull = true;
                break;
        }
    }

    static IEnumerable<Value[]> VisibleRows(Table t, Ctx c)
    {
        foreach (var s in t.Rows.ToArray())
        {
            var d = s.Visible(c.Tx);
            if (d != null) yield return d;
        }
    }

    static bool AnyRow(Database db, Table t, Ctx c, Func<Value[], bool> pred)
    {
        foreach (var s in t.Rows)
        {
            if (s.LatestCommitted is { } d && pred(d)) return true;
            if (s.Owner != null && s.Pending is { } p && !s.PendingDelete && pred(p)) return true;
        }
        return false;
    }

    static void ValidateUnique(Database db, Table t, Index ix, Ctx c, string name)
    {
        var seen = new HashSet<IndexKey>();
        foreach (var row in VisibleRows(t, c))
        {
            var key = ix.KeyOf(row);
            if (key.AllNull) continue;
            if (!seen.Add(key))
            {
                db.Indexes.Remove(ix.Name);
                t.Indexes.Remove(ix.Name);
                throw Err.Unique(name, t.Name, Dml.KeyText(t, ix.Columns, row));
            }
        }
    }

    static void RemoveConstraint(Database db, Table t, string name)
    {
        var con = db.Constraints[name];
        switch (con.Type)
        {
            case ConstraintType.PrimaryKey:
            case ConstraintType.Unique:
                foreach (var refName in t.ReferencedBy.ToArray())
                {
                    var fk = db.Constraints[refName];
                    if (fk.RefConstraint == name && fk.Table != t.Name)
                        throw new StatusBuilder().Gds(FbMessages.integ_fail).Num(1).Gds(FbMessages.primary_key_ref).Build();
                }
                foreach (var refName in t.ReferencedBy.ToArray())
                    if (db.Constraints[refName].RefConstraint == name) RemoveConstraint(db, t, refName);
                if (t.PrimaryKey == name) t.PrimaryKey = null;
                t.Uniques.Remove(name);
                RemoveIndex(db, t, con.IndexName!);
                break;
            case ConstraintType.ForeignKey:
                t.ForeignKeys.Remove(name);
                if (db.Tables.TryGetValue(con.RefTable!, out var parent)) parent.ReferencedBy.Remove(name);
                RemoveIndex(db, t, con.IndexName!);
                break;
            case ConstraintType.Check:
                t.Checks.Remove(name);
                break;
        }
        db.Constraints.Remove(name);
    }

    static void RemoveIndex(Database db, Table t, string name)
    {
        db.Indexes.Remove(name);
        t.Indexes.Remove(name);
    }

    static void DropTableObjects(Database db, Table t)
    {
        foreach (var name in t.ForeignKeys.ToArray()) RemoveConstraint(db, t, name);
        foreach (var name in t.Checks.ToArray()) db.Constraints.Remove(name);
        foreach (var name in t.Uniques.ToArray()) { db.Constraints.Remove(name); }
        if (t.PrimaryKey != null) db.Constraints.Remove(t.PrimaryKey);
        foreach (var ix in t.Indexes) db.Indexes.Remove(ix);
        foreach (var tr in t.Triggers) db.Triggers.Remove(tr);
        foreach (var col in t.Columns)
            if (col.IdentitySequence != null) db.Sequences.Remove(col.IdentitySequence);
    }

    // ------------------------------------------------------------------ ALTER TABLE

    /// <summary>Rebuilds rows with a new column layout. map[newIndex] = old index or -1 (filled by <paramref name="fill"/>).</summary>
    static void Relayout(Database db, Table t, List<Column> newCols, int[] map, Func<int, Value>? fill, Ctx c)
    {
        db.EnsureOwned(t);
        Value[] Convert(Value[] old)
        {
            var nr = new Value[newCols.Count];
            for (var i = 0; i < nr.Length; i++) nr[i] = map[i] >= 0 ? old[map[i]] : fill?.Invoke(i) ?? Value.Null;
            return nr;
        }
        foreach (var s in t.Rows)
        {
            RowVersion? head = null, tail = null;
            for (var v = s.Committed; v != null; v = v.Older)
            {
                var nv = new RowVersion { Data = v.Data == null ? null : Convert(v.Data), Seq = v.Seq };
                if (head == null) head = nv;
                else tail!.Older = nv;
                tail = nv;
            }
            s.Committed = head;
            if (s.Pending != null) s.Pending = Convert(s.Pending);
        }
        // remap column positions of indexes and constraints
        var oldToNew = new Dictionary<int, int>();
        for (var i = 0; i < map.Length; i++) if (map[i] >= 0) oldToNew[map[i]] = i;
        int[] Remap(int[] cols) => cols.Select(x => oldToNew.TryGetValue(x, out var n) ? n : throw Err.MetaUpdate("column is used by a constraint or index")).ToArray();
        foreach (var con in db.Constraints.Values)
        {
            if (con.Table == t.Name && con.Type != ConstraintType.Check) con.Columns = Remap(con.Columns);
            if (con.Type == ConstraintType.ForeignKey && con.RefTable == t.Name) con.RefColumns = Remap(con.RefColumns!);
        }
        t.Columns = newCols;
        t.RebuildColumnIndex();
        foreach (var ixName in t.Indexes)
        {
            var ix = db.Indexes[ixName];
            ix.Columns = Remap(ix.Columns);
            ix.Collations = ix.Columns.Select(i => t.Columns[i].Type.Collation).ToArray();
            ix.Rebuild(t.Rows);
        }
        _ = c;
    }

    static void AlterTable(Database db, AlterTableStmt st, Ctx c)
    {
        var t = db.Tables.TryGetValue(st.Name, out var x) ? x : throw Err.TableUnknown(st.Name);
        if (t.IsSystem) throw Err.MetaUpdate("Cannot alter system table " + t.Name);
        foreach (var action in st.Actions)
        {
            switch (action)
            {
                case AddColumnAction add:
                {
                    if (t.FindColumn(add.Column.Name) >= 0) throw ColumnExists(t.Name, add.Column.Name);
                    var col = MakeColumn(db, t, add.Column);
                    NameColumnConstraints(db, col, add.Column);
                    var newCols = t.Columns.ToList();
                    newCols.Add(col);
                    var map = Enumerable.Range(0, t.Columns.Count).Append(-1).ToArray();
                    // existing rows read the (column or domain) default only when the new column is NOT NULL
                    Value fill = Value.Null;
                    var def = col.Default ?? (col.Domain != null && db.Domains.TryGetValue(col.Domain, out var dd) ? dd.Default : null);
                    if (col.NotNull && def != null && col.ComputedBy == null)
                    {
                        var node = Comp(db).Bind(def, new Scope(null), col.Type);
                        fill = Conv.Cast(node.Eval(c), col.Type);
                    }
                    if (col.NotNull && fill.IsNull && t.Rows.Any(r => !r.IsDead) && VisibleRows(t, c).Any())
                        throw new StatusBuilder().Gds(Err.isc_no_meta_update).Gds(Err.isc_random)
                            .Str($"Cannot make field {col.Name} of table {t.Name} NOT NULL because there are NULLs present").Build("22006");
                    col.Position = t.Columns.Select((x, k) => x.Position ?? k).DefaultIfEmpty(-1).Max() + 1;
                    Relayout(db, t, newCols, map, _ => fill, c);
                    if (col.ComputedBy != null)
                    {
                        col.Type = null!;
                        InferComputedTypes(db, t);
                        RecomputeComputed(db, t, c);
                    }
                    foreach (var con in add.Column.Constraints) AddConstraint(db, t, con, c, validate: true);
                    break;
                }
                case DropColumnAction drop:
                {
                    var ci = t.FindColumn(drop.Name);
                    if (ci < 0) throw ColumnMissing(t, drop.Name);
                    // a column a view reads cannot go (the view's name comes padded, like Firebird's CHAR field)
                    foreach (var v in db.Views.Values)
                        if (ViewDeps.Of(db, v.Query).Any(x => x.Table == t.Name && x.Column == drop.Name))
                            throw new StatusBuilder().Gds(FbMessages.dyn_52).Str(drop.Name).Str(t.Name).Str(v.Name.PadRight(63)).Build();
                    // a key column referenced by another table's foreign key: the system trigger on RDB$RELATION_FIELDS
                    if (db.Constraints.Values.Any(k => k.Type == ConstraintType.ForeignKey && k.RefTable == t.Name && k.Table != t.Name && k.RefColumns!.Contains(ci)))
                        throw new StatusBuilder().Gds(FbMessages.integ_fail).Num(1).Gds(FbMessages.primary_key_ref)
                            .Gds(Err.isc_stack_trace).Str("At trigger 'RDB$TRIGGER_23'").Build();
                    foreach (var con in db.Constraints.Values.ToArray())
                    {
                        var uses = (con.Table == t.Name && con.Columns.Contains(ci)) ||
                                   (con.Type == ConstraintType.ForeignKey && con.RefTable == t.Name && con.RefColumns!.Contains(ci));
                        if (!uses) continue;
                        // Firebird drops single-column constraints together with the column
                        if (con.Table == t.Name && con.Columns.Length == 1) RemoveConstraint(db, t, con.Name);
                        else throw Err.MetaUpdate($"Column {drop.Name} is used in constraint {con.Name}");
                    }
                    foreach (var ixName in t.Indexes.ToArray())
                        if (db.Indexes[ixName].Columns.Contains(ci))
                            throw Err.MetaUpdate($"Column {drop.Name} is used in index {ixName}");
                    var col = t.Columns[ci];
                    // the other columns keep their positions: Firebird leaves the gap
                    for (var k = 0; k < t.Columns.Count; k++) t.Columns[k].Position ??= k;
                    var newCols = t.Columns.Where((_, i) => i != ci).ToList();
                    var map = Enumerable.Range(0, t.Columns.Count).Where(i => i != ci).ToArray();
                    Relayout(db, t, newCols, map, null, c);
                    if (col.IdentitySequence != null) db.Sequences.Remove(col.IdentitySequence);
                    break;
                }
                case AddConstraintAction ac:
                    AddConstraint(db, t, ac.Constraint, c, validate: true);
                    break;
                case DropConstraintAction dc:
                    if (!db.Constraints.TryGetValue(dc.Name, out var dcon) || dcon.Table != t.Name)
                        throw new StatusBuilder().Gds(FbMessages.dyn_130).Str(dc.Name).Build();
                    RemoveConstraint(db, t, dc.Name);
                    break;
                case AlterColumnAction ac:
                    AlterColumn(db, t, ac, c);
                    break;
            }
        }
    }

    /// <summary>AlterDomainNode::checkUpdate: the type changes Firebird allows for a column or domain.</summary>
    internal static void CheckTypeChange(string column, SqlType from, SqlType to)
    {
        static bool Text(SqlType t) => t.Kind is TypeKind.Char or TypeKind.VarChar;
        static bool DateTime(SqlType t) => t.Kind is TypeKind.Date or TypeKind.Time or TypeKind.Timestamp;
        FbError Invalid() => new StatusBuilder().Gds(FbMessages.dyn_invalid_dtype_conversion).Str(column).Str(DtypeName(from)).Str(DtypeName(to)).Build();
        FbError TooSmall(int len) => new StatusBuilder().Gds(FbMessages.dyn_char_fld_too_small).Str(column).Num(len).Build();
        var fromLen = Text(from) ? from.Length : TypeRules.StringLength(from);
        if (from.Kind == TypeKind.Blob || to.Kind == TypeKind.Blob)
            throw new StatusBuilder().Gds(FbMessages.dyn_dtype_invalid).Str(column).Build();
        if (Text(from))
        {
            if (!Text(to)) throw new StatusBuilder().Gds(FbMessages.dyn_dtype_conv_invalid).Str(column).Build();
            if (to.Length < fromLen) throw TooSmall(fromLen);
            return;
        }
        if (Text(to))
        {
            if (from.Kind == TypeKind.Boolean) throw Invalid();
            if (to.Length < fromLen) throw TooSmall(fromLen);
            return;
        }
        if (DateTime(from))
        {
            var ok = to.Kind == TypeKind.Date ? from.Kind != TypeKind.Time
                : to.Kind == TypeKind.Time ? from.Kind != TypeKind.Date
                : to.Kind == TypeKind.Timestamp && from.Kind != TypeKind.Time;
            if (!ok) throw Invalid();
            return;
        }
        if (from.Kind == TypeKind.Boolean)
        {
            if (to.Kind != TypeKind.Boolean) throw Invalid();
            return;
        }
        // numbers: the storage may only grow
        var allowed = to.Kind switch
        {
            TypeKind.SmallInt => from.Kind is TypeKind.SmallInt,
            TypeKind.Integer => from.Kind is TypeKind.SmallInt or TypeKind.Integer,
            TypeKind.Float => from.Kind is TypeKind.Float or TypeKind.SmallInt,
            TypeKind.BigInt => from.Kind is TypeKind.SmallInt or TypeKind.Integer or TypeKind.BigInt,
            TypeKind.Double => from.Kind is TypeKind.Double or TypeKind.Float or TypeKind.SmallInt or TypeKind.Integer,
            TypeKind.Int128 => from.Kind is TypeKind.SmallInt or TypeKind.Integer or TypeKind.BigInt or TypeKind.Int128,
            _ => false,
        };
        if (!allowed) throw Invalid();
        // checkUpdateNumericType: NUMERIC/DECIMAL may not lose integral digits
        if (to.Kind is not (TypeKind.Float or TypeKind.Double) && from.SubType != 0 && to.SubType != 0
            && from.Precision - from.Scale > to.Precision - to.Scale)
        {
            var diff = to.Precision - (from.Precision - from.Scale);
            var code = FbMessages.dyn_scale_too_big;
            if (diff < 0)
            {
                code = FbMessages.dyn_precision_too_small;
                diff = to.Precision + to.Scale - diff;
            }
            throw new StatusBuilder().Gds(code).Str(column).Num(diff).Build();
        }
    }

    /// <summary>DSC_dtype_names: the storage type's name.</summary>
    static string DtypeName(SqlType t) => t.Kind switch
    {
        TypeKind.Char => "CHAR",
        TypeKind.VarChar => "VARCHAR",
        TypeKind.SmallInt => "SMALLINT",
        TypeKind.Integer => "INTEGER",
        TypeKind.BigInt => "BIGINT",
        TypeKind.Int128 => "INT128",
        TypeKind.Float => "FLOAT",
        TypeKind.Double => "DOUBLE PRECISION",
        TypeKind.Date => "DATE",
        TypeKind.Time => "TIME",
        TypeKind.Timestamp => "TIMESTAMP",
        TypeKind.Blob => "BLOB",
        TypeKind.Boolean => "BOOLEAN",
        _ => "<dtype_unknown>",
    };

    static void RecomputeComputed(Database db, Table t, Ctx c)
    {
        var rt = Compiler.BuildRuntime(db, t);
        if (!rt.HasComputed) return;
        db.EnsureOwned(t);
        foreach (var s in t.Rows)
        {
            for (var v = s.Committed; v != null; v = v.Older)
                if (v.Data != null)
                {
                    var nr = (Value[])v.Data.Clone();
                    foreach (var (col, expr) in rt.Computed) nr[col] = Coercion.ToType(expr.Eval(c, nr), t.Columns[col].Type);
                    v.Data = nr;
                }
        }
    }

    static void AlterColumn(Database db, Table t, AlterColumnAction ac, Ctx c)
    {
        var ci = t.FindColumn(ac.Name);
        if (ci < 0) throw ColumnMissing(t, ac.Name);
        var col = t.Columns[ci];
        if (ac.NewName != null)
        {
            if (t.FindColumn(ac.NewName) >= 0) throw ColumnExists(t.Name, ac.NewName);
            col.Name = ac.NewName;
            t.RebuildColumnIndex();
        }
        if (ac.Position != null)
        {
            var pos = Math.Clamp(ac.Position.Value - 1, 0, t.Columns.Count - 1);
            var order = Enumerable.Range(0, t.Columns.Count).Where(i => i != ci).ToList();
            order.Insert(pos, ci);
            Relayout(db, t, order.Select(i => t.Columns[i]).ToList(), order.ToArray(), null, c);
            // moving a column renumbers the positions
            for (var k = 0; k < t.Columns.Count; k++) t.Columns[k].Position = k;
        }
        if (ac.NewType != null)
        {
            if (db.Constraints.Values.Any(k => k.IndexName != null && (k.Table == t.Name && k.Columns.Contains(ci)
                    || k.Type == ConstraintType.ForeignKey && k.RefTable == t.Name && k.RefColumns!.Contains(ci))))
                throw new StatusBuilder().Gds(FbMessages.integ_fail).Num(1).Gds(FbMessages.integ_index_seg_mod).Build();
            var newType = Comp(db).ResolveType(ac.NewType, out var dom);
            CheckTypeChange(col.Name, col.Type, newType);
            // Firebird: a column that becomes text without a character set gets NONE (unless it was text)
            if (newType.IsString && dom == null && ac.NewType.Charset == null && !col.Type.IsString)
                newType = newType.Kind == TypeKind.Char ? SqlType.Char(newType.Length, Charset.None) : SqlType.VarChar(newType.Length, Charset.None);
            foreach (var row in VisibleRows(t, c)) Conv.Cast(row[ci], newType);
            col.Type = newType;
            if (dom != null) col.Domain = dom.Name;
            var idx = ci;
            var map = Enumerable.Range(0, t.Columns.Count).ToArray();
            map[idx] = -1;
            // convert values in place through a relayout that casts the changed column
            db.EnsureOwned(t);
            foreach (var s in t.Rows)
            {
                for (var v = s.Committed; v != null; v = v.Older)
                    if (v.Data != null)
                    {
                        var nr = (Value[])v.Data.Clone();
                        nr[idx] = Conv.Cast(nr[idx], newType);
                        v.Data = nr;
                    }
                if (s.Pending != null)
                {
                    var np = (Value[])s.Pending.Clone();
                    np[idx] = Conv.Cast(np[idx], newType);
                    s.Pending = np;
                }
            }
            foreach (var ixName in t.Indexes)
            {
                var ix = db.Indexes[ixName];
                ix.Collations = ix.Columns.Select(i => t.Columns[i].Type.Collation).ToArray();
                ix.Rebuild(t.Rows);
            }
        }
        if (ac.SetDefault)
        {
            col.Default = ac.Default;
            col.DefaultSource = ac.DefaultSource;
        }
        if (ac.DropDefault)
        {
            col.Default = null;
            col.DefaultSource = null;
        }
        if (ac.NotNull == true)
        {
            if (VisibleRows(t, c).Any(r => r[ci].IsNull))
                throw Err.MetaUpdate($"Cannot make field {col.Name} of table {t.Name} NOT NULL because there are NULLs present");
            col.NotNull = true;
        }
        else if (ac.NotNull == false)
        {
            if (t.PrimaryKey != null && db.Constraints[t.PrimaryKey].Columns.Contains(ci))
                throw Err.MetaUpdate($"Column {col.Name} is part of the primary key");
            col.NotNull = false;
        }
        if (ac.RestartIdentity)
        {
            if (col.IdentitySequence == null) throw Err.MetaUpdate($"Column {col.Name} is not an identity column");
            var seq = db.Sequences[col.IdentitySequence];
            seq.Value = (ac.RestartWith ?? seq.InitialValue) - seq.Increment; // the next value is the restart value
        }
        if (ac.ComputedBy != null)
        {
            if (col.ComputedBy == null) throw Err.MetaUpdate($"Cannot change {col.Name} to a computed column");
            // the new expression must bind against the table, like when the column was created
            var comp = Comp(db);
            var cs = new Scope(null);
            cs.Add(new SourceItem
            {
                Name = t.Name, Stream = comp.NewStream(t.Columns.Count), Table = t,
                Cols = t.Columns.Select(cc => new ColInfo { Name = cc.Name, Type = cc.Type, Relation = t.Name, Field = cc.Name }).ToList(),
            });
            comp.Bind(ac.ComputedBy, cs);
            col.ComputedBy = ac.ComputedBy;
            col.ComputedBySource = ac.ComputedBySource;
            RecomputeComputed(db, t, c);
        }
    }

    // ------------------------------------------------------------------ indexes

    static void CreateIndex(Database db, CreateIndexStmt st, Ctx c)
    {
        // Firebird looks the columns up first: a missing table reports "Unknown columns in index"
        var t = db.Tables.TryGetValue(st.Table, out var x) ? x : throw new StatusBuilder().Gds(FbMessages.dyn_120).Str(st.Name).Build();
        if (db.Indexes.ContainsKey(st.Name)) throw Exists("Index", st.Name);
        if (st.ComputedBy != null)
        {
            if (st.Unique) throw Err.Unsupported("unique expression indexes");
            // expression indexes only affect plans: register a placeholder that keeps no entries
            var ph = new Index { Name = st.Name, Table = t.Name, Columns = [], Active = false, ExpressionSource = st.ComputedBySource };
            db.Indexes[st.Name] = ph;
            t.Indexes.Add(st.Name);
            return;
        }
        if (st.Columns.Any(n => t.FindColumn(n) < 0)) throw new StatusBuilder().Gds(FbMessages.dyn_120).Str(st.Name).Build();
        var cols = Cols(t, st.Columns);
        var ix = MakeIndex(db, t, st.Name, cols, st.Unique, st.Descending, null);
        if (st.Unique) ValidateUnique(db, t, ix, c, st.Name);
    }

    static void AlterIndex(Database db, AlterIndexStmt st)
    {
        if (!db.Indexes.TryGetValue(st.Name, out var ix)) throw Missing("Index", st.Name);
        if (ix.Constraint != null && !st.Active) throw Err.MetaUpdate($"Cannot deactivate index {st.Name} used by constraint {ix.Constraint}");
        if (ix.Columns.Length == 0) return;
        ix.Active = st.Active;
        if (st.Active) ix.Rebuild(db.Tables[ix.Table].Rows);
    }

    // ------------------------------------------------------------------ views

    static void CreateView(Database db, CreateViewStmt st)
    {
        var exists = db.Views.ContainsKey(st.Name);
        switch (st.Mode)
        {
            case CreateMode.Create when exists || db.Tables.ContainsKey(st.Name): throw Exists("View", st.Name);
            case CreateMode.Alter when !exists: throw Missing("View", st.Name);
        }
        if (!exists && db.Tables.ContainsKey(st.Name)) throw Exists("Table", st.Name);
        var q = new Compiler(db, 0).CompileSelect(st.Query, null);
        if (st.Columns != null && st.Columns.Count != q.Columns.Length)
            throw new StatusBuilder().Gds(Err.isc_sqlerr).Num(-607).Gds(FbMessages.dsql_command_err).Gds(FbMessages.num_field_err).Build();
        var view = new View { Name = st.Name, ColumnNames = st.Columns, Query = st.Query, Source = st.Source };
        for (var i = 0; i < q.Columns.Length; i++) view.Columns.Add((st.Columns?[i] ?? q.Columns[i].Name, q.Columns[i].Type));
        if (view.Columns.Select(cc => cc.Name).Distinct().Count() != view.Columns.Count)
            throw Err.MetaUpdate("duplicate column names in view " + st.Name);
        db.Views[st.Name] = view;
    }

    // ------------------------------------------------------------------ sequences / exceptions / domains

    static void CreateSequence(Database db, CreateSequenceStmt st)
    {
        var exists = db.Sequences.TryGetValue(st.Name, out var seq);
        if (st.Mode == CreateMode.Alter || (st.Mode == CreateMode.CreateOrAlter && exists))
        {
            if (!exists) throw Missing("Sequence", st.Name);
            if (st.IncrementBy != null) seq!.Increment = st.IncrementBy.Value;
            // Firebird 4+: the next value after a restart is the restart value
            if (st.Restart || st.Mode == CreateMode.CreateOrAlter && st.StartWith != null)
                seq!.Value = (st.StartWith ?? seq.InitialValue) - seq.Increment;
            if (st.StartWith != null) seq!.InitialValue = st.StartWith.Value;
            return;
        }
        if (st.Mode == CreateMode.Recreate && exists) db.Sequences.Remove(st.Name);
        else if (exists) throw Exists("Sequence", st.Name);
        if (st.IncrementBy == 0) throw Err.MetaUpdate("INCREMENT BY 0 is an illegal option for sequence " + st.Name);
        // Firebird 4+: START WITH (default 1) is the first NEXT VALUE
        var start = st.StartWith ?? 1;
        var increment = st.IncrementBy ?? 1;
        db.Sequences[st.Name] = new Sequence { Name = st.Name, Value = start - increment, InitialValue = start, Increment = increment };
    }

    static void CreateException(Database db, CreateExceptionStmt st)
    {
        var exists = db.Exceptions.TryGetValue(st.Name, out var ex);
        switch (st.Mode)
        {
            case CreateMode.Create when exists: throw Exists("Exception", st.Name);
            case CreateMode.Alter when !exists: throw Missing("Exception", st.Name);
        }
        if (st.Message.Length > 1021) throw Err.MetaUpdate("Exception message is too long");
        db.Exceptions[st.Name] = new ExceptionDef { Name = st.Name, Message = st.Message, Number = ex?.Number ?? ++db.ExceptionCounter };
    }

    static void CreateDomain(Database db, CreateDomainStmt st)
    {
        if (db.Domains.ContainsKey(st.Name)) throw Exists("Domain", st.Name);
        var comp = Comp(db);
        var type = comp.ResolveType(st.Type);
        if (st.Collate != null && type.IsTextual) type = type.WithCollation(Compiler.CollationOf(st.Collate));
        var d = new Domain
        {
            Name = st.Name, Type = type, Default = st.Default, DefaultSource = st.DefaultSource, NotNull = st.NotNull,
            Check = st.Check, CheckSource = st.CheckSource,
        };
        if (d.Check != null) ValidateDomainCheck(db, d);
        db.Domains[st.Name] = d;
    }

    static void ValidateDomainCheck(Database db, Domain d)
    {
        var comp = Comp(db);
        var st = comp.NewStream(1);
        var s = new Scope(null);
        s.Add(new SourceItem { Name = "VALUE", Stream = st, Cols = [new ColInfo { Name = "VALUE", Type = d.Type }] });
        comp.BindBool(d.Check!, s);
    }

    static void Comment(Database db, CommentStmt st)
    {
        switch (st.Kind)
        {
            case "TABLE":
                (db.Tables.TryGetValue(st.Name, out var t) && !t.IsSystem ? t : throw Missing("Table", st.Name)).Description = st.Text;
                break;
            case "VIEW":
                (db.Views.TryGetValue(st.Name, out var v) ? v : throw Missing("View", st.Name)).Description = st.Text;
                break;
            case "COLUMN":
                if (db.Tables.TryGetValue(st.Name, out var tc))
                {
                    var ci = tc.FindColumn(st.Sub!);
                    if (ci < 0) throw ColumnMissing(tc, st.Sub!);
                    tc.Columns[ci].Description = st.Text;
                }
                else if (db.Views.TryGetValue(st.Name, out var vc))
                {
                    var ci = vc.Columns.FindIndex(x => x.Name == st.Sub);
                    if (ci < 0) throw new StatusBuilder().Gds(FbMessages.dyn_column_does_not_exist).Str(st.Sub!).Str(vc.Name).Build();
                    (vc.ColumnDescriptions ??= new())[st.Sub!] = st.Text;
                }
                else throw Missing("Table", st.Name);
                break;
            case "PROCEDURE":
                (db.Procedures.TryGetValue(st.Name, out var p) ? p : throw Missing("Procedure", st.Name)).Description = st.Text;
                break;
            case "FUNCTION":
                (db.Functions.TryGetValue(st.Name, out var f) ? f : throw Missing("Function", st.Name)).Description = st.Text;
                break;
            case "TRIGGER":
                (db.Triggers.TryGetValue(st.Name, out var tr) ? tr : throw Missing("Trigger", st.Name)).Description = st.Text;
                break;
            case "DOMAIN":
                (db.Domains.TryGetValue(st.Name, out var dm) ? dm : throw Missing("Domain", st.Name)).Description = st.Text;
                break;
            case "SEQUENCE":
                (db.Sequences.TryGetValue(st.Name, out var sq) ? sq : throw Missing("Sequence", st.Name)).Description = st.Text;
                break;
            case "EXCEPTION":
                (db.Exceptions.TryGetValue(st.Name, out var ex) ? ex : throw Missing("Exception", st.Name)).Description = st.Text;
                break;
            case "INDEX":
                (db.Indexes.TryGetValue(st.Name, out var ix) ? ix : throw Missing("Index", st.Name)).Description = st.Text;
                break;
        }
    }

    static void AlterDomain(Database db, AlterDomainStmt st, Ctx c)
    {
        if (!db.Domains.TryGetValue(st.Name, out var d)) throw Missing("Domain", st.Name);
        if (st.SetDefault)
        {
            d.Default = st.Default;
            d.DefaultSource = st.DefaultSource;
        }
        if (st.DropDefault) { d.Default = null; d.DefaultSource = null; }
        if (st.NotNull != null) d.NotNull = st.NotNull.Value;
        if (st.DropConstraint) { d.Check = null; d.CheckSource = null; }
        if (st.AddCheck != null)
        {
            d.Check = st.AddCheck;
            d.CheckSource = st.CheckSource;
            ValidateDomainCheck(db, d);
        }
        if (st.NewType != null) d.Type = Comp(db).ResolveType(st.NewType);
        foreach (var t in db.Tables.Values)
            foreach (var col in t.Columns)
                if (col.Domain == d.Name)
                {
                    if (st.NewType != null) col.Type = d.Type;
                    if (st.NotNull != null) col.NotNull = d.NotNull;
                }
        if (st.NewName != null)
        {
            db.Domains.Remove(d.Name);
            foreach (var t in db.Tables.Values)
                foreach (var col in t.Columns)
                    if (col.Domain == d.Name) col.Domain = st.NewName;
            d.Name = st.NewName;
            db.Domains[d.Name] = d;
        }
    }

    // ------------------------------------------------------------------ routines / triggers

    static List<RoutineParam> Params(Database db, List<ParamDecl> decls)
    {
        var comp = Comp(db);
        var names = new HashSet<string>(StringComparer.Ordinal);
        return decls.Select(p =>
        {
            if (!names.Add(p.Name)) throw Err.MetaUpdate("duplicate parameter name " + p.Name);
            var t = comp.ResolveType(p.Type, out var dom);
            return new RoutineParam { Name = p.Name, Type = t, NotNull = p.NotNull || (dom?.NotNull ?? false), Default = p.Default, DefaultSource = p.DefaultSource, Domain = dom?.Name };
        }).ToList();
    }

    static bool HasSuspend(Stmt s) => s switch
    {
        SuspendStmt => true,
        BlockStmt b => b.Body.Any(HasSuspend) || b.Handlers.Any(h => HasSuspend(h.Body)),
        IfStmt i => HasSuspend(i.Then) || (i.Else != null && HasSuspend(i.Else)),
        WhileStmt w => HasSuspend(w.Body),
        ForSelectStmt f => HasSuspend(f.Body),
        ForExecStmt fe => HasSuspend(fe.Body),
        AutonomousStmt a => HasSuspend(a.Body),
        _ => false,
    };

    static void CreateProcedure(Database db, CreateProcedureStmt st)
    {
        var exists = db.Procedures.TryGetValue(st.Name, out var old);
        switch (st.Mode)
        {
            case CreateMode.Create when exists: throw Exists("Procedure", st.Name);
            case CreateMode.Alter when !exists: throw Missing("Procedure", st.Name);
        }
        var p = new Procedure
        {
            Name = st.Name, Inputs = Params(db, st.Inputs), Outputs = Params(db, st.Outputs), Body = st.Body, Source = st.Source,
            Selectable = HasSuspend(st.Body),
        };
        db.Procedures[st.Name] = p;
        try
        {
            db.SchemaChanged();
            Compiler.CompileProcedure(db, p);
        }
        catch
        {
            if (old != null) db.Procedures[st.Name] = old;
            else db.Procedures.Remove(st.Name);
            throw;
        }
    }

    static void CreateFunction(Database db, CreateFunctionStmt st)
    {
        var exists = db.Functions.TryGetValue(st.Name, out var old);
        switch (st.Mode)
        {
            case CreateMode.Create when exists: throw Exists("Function", st.Name);
            case CreateMode.Alter when !exists: throw Missing("Function", st.Name);
        }
        if (Builtins.Find(st.Name) != null) throw Err.MetaUpdate("Function " + st.Name + " conflicts with a built-in function");
        var f = new Function
        {
            Name = st.Name, Inputs = Params(db, st.Inputs), Returns = Comp(db).ResolveType(st.Returns), Body = st.Body,
            Source = st.Source, Deterministic = st.Deterministic,
        };
        db.Functions[st.Name] = f;
        try
        {
            db.SchemaChanged();
            Compiler.CompileFunction(db, f);
        }
        catch
        {
            if (old != null) db.Functions[st.Name] = old;
            else db.Functions.Remove(st.Name);
            throw;
        }
    }

    static void CreateTrigger(Database db, CreateTriggerStmt st)
    {
        var exists = db.Triggers.TryGetValue(st.Name, out var old);
        switch (st.Mode)
        {
            case CreateMode.Create when exists: throw Exists("Trigger", st.Name);
            case CreateMode.Alter when !exists: throw Missing("Trigger", st.Name);
        }
        if (st.DbEvent != DbTriggerEvent.None) throw Err.Unsupported("database triggers (ON CONNECT/TRANSACTION ...)");
        Trigger t;
        if (st.Mode == CreateMode.Alter || (st.Mode == CreateMode.CreateOrAlter && exists && st.Body == null))
        {
            t = old!.CloneDef();
            if (st.Active != null) t.Active = st.Active.Value;
            if (st.Position != null) t.Position = st.Position.Value;
            if (st.Events != TriggerEvent.None)
            {
                t.Events = st.Events;
                t.Timing = st.Timing;
            }
            if (st.Body != null)
            {
                t.Body = st.Body;
                t.Source = st.Source;
            }
        }
        else
        {
            var tableName = st.Table ?? throw Err.MetaUpdate("Trigger " + st.Name + " requires a table");
            if (db.Views.ContainsKey(tableName)) throw Err.Unsupported("triggers on views");
            t = new Trigger
            {
                Name = st.Name, Table = tableName, Active = st.Active ?? true, Timing = st.Timing, Events = st.Events,
                Position = st.Position ?? 0, Body = st.Body!, Source = st.Source,
            };
        }
        var table = db.Tables.TryGetValue(t.Table!, out var tt) ? tt : throw Err.TableUnknown(t.Table!);
        if (old != null && old.Table != null && db.Tables.TryGetValue(old.Table, out var oldTable)) oldTable.Triggers.Remove(old.Name);
        db.Triggers[t.Name] = t;
        table.Triggers.Add(t.Name);
        try
        {
            db.SchemaChanged();
            Compiler.CompileTrigger(db, t, table);
        }
        catch
        {
            table.Triggers.Remove(t.Name);
            if (old != null)
            {
                db.Triggers[old.Name] = old;
                if (old.Table != null && db.Tables.TryGetValue(old.Table, out var ot)) ot.Triggers.Add(old.Name);
            }
            else db.Triggers.Remove(t.Name);
            throw;
        }
    }

    // ------------------------------------------------------------------ DROP

    static bool ViewReferences(SelectQuery q, string name) => RefsQuery(q, name);

    static bool RefsQuery(QueryExpr q, string name) => q switch
    {
        SelectQuery sq => (sq.With?.Any(w => RefsQuery(w.Query, name)) ?? false) || RefsQuery(sq.Body, name),
        SetOpQuery so => RefsQuery(so.Left, name) || RefsQuery(so.Right, name),
        QuerySpec qs => qs.From.Any(f => RefsTable(f, name)),
        _ => false,
    };

    static bool RefsTable(TableRef t, string name) => t switch
    {
        NamedTableRef n => n.Name == name,
        JoinRef j => RefsTable(j.Left, name) || RefsTable(j.Right, name),
        DerivedTableRef d => RefsQuery(d.Query, name),
        _ => false,
    };

    static void Drop(Database db, DropStmt st)
    {
        switch (st.Kind)
        {
            case ObjectKind.Table:
            {
                if (!db.Tables.TryGetValue(st.Name, out var t)) throw Missing("Table", st.Name);
                if (t.IsSystem) throw Err.MetaUpdate("Cannot drop system table " + t.Name);
                foreach (var r in t.ReferencedBy)
                {
                    var fk = db.Constraints[r];
                    if (fk.Table != t.Name)
                        throw new StatusBuilder().Gds(FbMessages.integ_fail).Num(1).Gds(FbMessages.primary_key_ref).Build();
                }
                foreach (var v in db.Views.Values)
                    if (ViewReferences(v.Query, t.Name))
                        throw Err.MetaUpdate($"cannot delete: COLUMN {t.Name} is used in VIEW {v.Name}");
                DropTableObjects(db, t);
                db.Tables.Remove(t.Name);
                break;
            }
            case ObjectKind.View:
                if (!db.Views.ContainsKey(st.Name)) throw Missing("View", st.Name);
                foreach (var v in db.Views.Values)
                    if (v.Name != st.Name && ViewReferences(v.Query, st.Name))
                        throw Err.MetaUpdate($"cannot delete: VIEW {st.Name} is used in VIEW {v.Name}");
                db.Views.Remove(st.Name);
                break;
            case ObjectKind.Procedure:
                if (!db.Procedures.Remove(st.Name)) throw Missing("Procedure", st.Name);
                break;
            case ObjectKind.Function:
                if (!db.Functions.Remove(st.Name)) throw Missing("Function", st.Name);
                break;
            case ObjectKind.Trigger:
            {
                if (!db.Triggers.TryGetValue(st.Name, out var tr)) throw Missing("Trigger", st.Name);
                db.Triggers.Remove(st.Name);
                if (tr.Table != null && db.Tables.TryGetValue(tr.Table, out var t)) t.Triggers.Remove(st.Name);
                break;
            }
            case ObjectKind.Exception:
                if (!db.Exceptions.Remove(st.Name)) throw Missing("Exception", st.Name);
                break;
            case ObjectKind.Sequence:
            {
                if (!db.Sequences.TryGetValue(st.Name, out var seq)) throw Missing("Sequence", st.Name);
                if (seq.IsIdentity) throw Err.MetaUpdate("Cannot drop identity sequence " + st.Name);
                db.Sequences.Remove(st.Name);
                break;
            }
            case ObjectKind.Index:
            {
                if (!db.Indexes.TryGetValue(st.Name, out var ix)) throw Missing("Index", st.Name);
                if (ix.Constraint != null) throw Err.MetaUpdate($"Cannot drop index {st.Name} used by constraint {ix.Constraint}");
                RemoveIndex(db, db.Tables[ix.Table], st.Name);
                break;
            }
            case ObjectKind.Domain:
            {
                if (!db.Domains.ContainsKey(st.Name)) throw Missing("Domain", st.Name);
                foreach (var t in db.Tables.Values)
                    foreach (var col in t.Columns)
                        if (col.Domain == st.Name)
                            throw new StatusBuilder().Gds(FbMessages.dyn_43).Str(st.Name).Str(t.Name).Str(col.Name).Build();
                db.Domains.Remove(st.Name);
                break;
            }
            default:
                throw Err.Unsupported("DROP " + st.Kind);
        }
    }
}
