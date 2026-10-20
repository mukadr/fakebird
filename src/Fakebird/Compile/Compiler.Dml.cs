using Fakebird.Engine;
using Fakebird.Exec;
using Fakebird.Sql;
using Fakebird.Types;

namespace Fakebird.Compile;

public sealed partial class Compiler
{
    Table DmlTable(string name)
    {
        if (Db.Tables.TryGetValue(name, out var t))
        {
            if (t.IsSystem) throw new StatusBuilder().Gds(335544352).Str("UPDATE").Str("TABLE").Str(name).Build("28000");
            return t;
        }
        if (Db.Views.ContainsKey(name)) throw Err.Unsupported("DML on views (" + name + ")");
        throw Err.TableUnknown(name);
    }

    int ColumnOf(Table t, string name, string? qualifier = null)
    {
        var i = t.FindColumn(name);
        if (i < 0) throw Err.ColumnUnknown(qualifier == null ? name : qualifier + "." + name);
        return i;
    }

    static SourceItem TableItem(Table t, string? alias, int stream, bool qualifiedOnly = false, string? name = null) => new()
    {
        Alias = alias, Name = name ?? t.Name, Stream = stream, Table = t, QualifiedOnly = qualifiedOnly,
        Cols = t.Columns.Select(c => new ColInfo { Name = c.Name, Type = c.Type, NotNull = c.NotNull, Relation = t.Name, Field = c.Name }).ToList(),
    };

    Scope DmlScope(Scope? parent) => new(parent ?? RootScope);

    /// <summary>RETURNING list bound against the new (or old) row.</summary>
    (XNode[] Nodes, OutCol[] Cols)? BindReturning(ReturningClause? r, Table t, string? alias, int mainStream, int newStream, int oldStream, Scope? parent)
    {
        if (r == null) return null;
        var s = DmlScope(parent);
        s.Add(TableItem(t, alias, mainStream));
        if (newStream >= 0) s.Add(TableItem(t, "NEW", newStream, true, "NEW"));
        if (oldStream >= 0) s.Add(TableItem(t, "OLD", oldStream, true, "OLD"));
        var nodes = new List<XNode>();
        var cols = new List<OutCol>();
        foreach (var item in r.Items)
        {
            if (item.Expr is StarExpr)
            {
                for (var i = 0; i < t.Columns.Count; i++)
                {
                    var n = Col(mainStream, i, t.Columns[i].Type, t.Columns[i].Name, t.Name);
                    n.Field = t.Columns[i].Name;
                    nodes.Add(n);
                    cols.Add(OutColOf(n, null));
                }
                continue;
            }
            var node = Bind(item.Expr, s, null);
            nodes.Add(node);
            cols.Add(OutColOf(node, item.Alias));
        }
        // Firebird describes every RETURNING value as nullable
        foreach (var col in cols) col.Nullable = true;
        return (nodes.ToArray(), cols.ToArray());
    }

    static Value[] EvalAll(Ctx c, XNode[] nodes)
    {
        var r = new Value[nodes.Length];
        for (var i = 0; i < r.Length; i++) r[i] = nodes[i].Eval(c);
        return r;
    }

    static int Kind(bool returning, int plain) => returning ? Wire.Isc.stmt_exec_procedure : plain;

    // ------------------------------------------------------------------ INSERT

    public Prepared CompileInsert(InsertStmt st, Scope? parent)
    {
        var t = DmlTable(st.Table);
        int[] cols;
        if (st.Columns != null)
        {
            cols = st.Columns.Select(c => ColumnOf(t, c)).ToArray();
            if (cols.Distinct().Count() != cols.Length) throw Err.Dsql(-206, "Column used more than once in INSERT");
        }
        else if (st.DefaultValues) cols = [];
        else cols = Enumerable.Range(0, t.Columns.Count).Where(i => !t.Columns[i].IsComputed).ToArray();
        foreach (var ci in cols)
            if (t.Columns[ci].IsComputed) throw Err.Random($"attempted update of read-only column {t.Name}.{t.Columns[ci].Name}");
        XNode?[]? values = null;
        QueryNode? query = null;
        var s = DmlScope(parent);
        if (st.Values != null)
        {
            if (st.Values.Count != cols.Length) throw Err.Dsql(-804, "Count of column list and variable list do not match");
            values = st.Values.Select((v, i) => v is KeywordArg { Keyword: "DEFAULT" } ? null : Bind(v, s, t.Columns[cols[i]].Type)).ToArray();
        }
        else if (st.Query != null)
        {
            _selectListTypes = cols.Select(c => t.Columns[c].Type).ToArray();
            query = CompileSelect(st.Query, parent ?? RootScope);
            if (query.Columns.Length != cols.Length) throw Err.Dsql(-804, "Count of column list and variable list do not match");
        }
        var newStream = NewStream(t.Columns.Count);
        var ret = BindReturning(st.Returning, t, st.Alias, newStream, newStream, -1, parent);
        var width = t.Columns.Count;
        var p = new DelegatePrepared(c =>
        {
            var rt = Dml.GetRuntime(c.Db, t);
            var result = new ExecResult();
            List<Value[]> sources;
            if (query != null)
            {
                sources = query.ToList(c);
                if (ret != null && sources.Count > 1) throw Err.MultipleRows();
            }
            else
            {
                var one = new Value[cols.Length];
                if (values != null)
                    for (var k = 0; k < cols.Length; k++)
                        if (values[k] != null) one[k] = values[k]!.Eval(c);
                sources = [one];
            }
            foreach (var src in sources)
            {
                var vals = new Value[width];
                var provided = new bool[width];
                for (var k = 0; k < cols.Length; k++)
                {
                    if (values != null && values[k] == null) continue;
                    vals[cols[k]] = src[k];
                    provided[cols[k]] = true;
                }
                var row = Dml.PrepareInsertRow(c, rt, vals, provided);
                var stored = Dml.Insert(c, rt, row);
                result.Inserted++;
                if (ret != null)
                {
                    c.Rows[newStream] = stored;
                    result.Rows = [EvalAll(c, ret.Value.Nodes)];
                }
            }
            return result;
        })
        {
            StmtType = Kind(ret != null, Wire.Isc.stmt_insert),
            Outputs = ret?.Cols ?? [],
        };
        return p;
    }

    // ------------------------------------------------------------------ UPDATE / DELETE

    RelNode TargetAccess(Table t, string? alias, Expr? where, Scope s, out int stream)
    {
        stream = NewStream(t.Columns.Count);
        var item = TableItem(t, alias, stream);
        s.Add(item);
        var pool = new List<Conj>();
        if (where != null)
        {
            if (ContainsAggregate(where)) throw Err.Dsql(-104, "Cannot use an aggregate function in a WHERE clause");
            foreach (var c in SplitAnd(where)) pool.Add(MakeConj(c, s));
        }
        var rel = PlanTree(new JLeaf { Item = item, Table = t }, pool, s);
        foreach (var c in pool)
            if (!c.Used) rel = new FilterRel(rel, c.Node);
        return rel;
    }

    RelNode OrderAndRows(RelNode rel, List<OrderItem> orderBy, Scope s)
    {
        if (orderBy.Count == 0) return rel;
        var keys = orderBy.Select(o =>
        {
            var n = Bind(o.Expr, s);
            var k = MakeSortKey(o, n.Type);
            k.Expr = n;
            return k;
        }).ToArray();
        return new SortRel(rel, keys, s.Streams.ToArray());
    }

    static List<(RowSlot Slot, Value[] Row)> CollectTargets(Ctx c, RelNode rel, int stream, (XNode? First, XNode? Skip) limit)
    {
        var list = new List<(RowSlot, Value[])>();
        var rows = Limit.Open(false, c, limit.First, limit.Skip);
        if (rows.Empty) return list;
        var seen = new HashSet<RowSlot>(ReferenceEqualityComparer.Instance);
        var cur = rel.Open(c);
        while (rows.Next(NextTarget)) list.Add((c.Slots[stream]!, c.Rows[stream]));
        return list;

        bool NextTarget()
        {
            while (cur.Next())
            {
                var slot = c.Slots[stream] ?? throw new InvalidOperationException("internal: no slot for DML target");
                if (seen.Add(slot)) return true;
            }
            return false;
        }
    }

    public Prepared CompileUpdate(UpdateStmt st, Scope? parent)
    {
        if (st.CurrentOf != null) throw Err.Unsupported("WHERE CURRENT OF");
        var t = DmlTable(st.Table);
        var s = DmlScope(parent);
        var rel = TargetAccess(t, st.Alias, st.Where, s, out var stream);
        var sets = new List<(int Col, XNode? Value)>();
        foreach (var a in st.Set)
        {
            if (a.Target.Qualifier != null && a.Target.Qualifier != (st.Alias ?? t.Name))
                throw Err.ColumnUnknown(a.Target.ToString());
            var ci = ColumnOf(t, a.Target.Name, a.Target.Qualifier);
            if (t.Columns[ci].IsComputed) throw Err.Random($"attempted update of read-only column {t.Name}.{t.Columns[ci].Name}");
            if (sets.Any(x => x.Col == ci)) throw Err.Dsql(-206, "Column used more than once in SET");
            sets.Add((ci, a.Value is KeywordArg { Keyword: "DEFAULT" } ? null : Bind(a.Value, s, t.Columns[ci].Type)));
        }
        rel = OrderAndRows(rel, st.OrderBy, s);
        var limit = BindLimit(new LimitSpec { RowsFrom = st.Rows, RowsTo = st.RowsTo }, s);
        var newStream = NewStream(t.Columns.Count);
        var ret = BindReturning(st.Returning, t, st.Alias, newStream, newStream, stream, parent);
        return new DelegatePrepared(c =>
        {
            c.Db.EnsureOwned(t);
            var rt = Dml.GetRuntime(c.Db, t);
            var targets = CollectTargets(c, rel, stream, limit);
            if (ret != null && targets.Count > 1) throw Err.MultipleRows();
            var result = new ExecResult();
            var vals = new Value[sets.Count];
            foreach (var (slot, _) in targets)
            {
                var cur = slot.Visible(c.Tx);
                if (cur == null) continue;
                c.Rows[stream] = cur;
                for (var k = 0; k < sets.Count; k++)
                    vals[k] = sets[k].Value == null
                        ? rt.Defaults[sets[k].Col]?.Eval(c) ?? Value.Null
                        : sets[k].Value!.Eval(c);
                var nr = (Value[])cur.Clone();
                for (var k = 0; k < sets.Count; k++) nr[sets[k].Col] = Dml.CastColumn(t, sets[k].Col, vals[k]);
                var stored = Dml.Update(c, rt, slot, cur, nr);
                result.Updated++;
                if (ret != null)
                {
                    c.Rows[stream] = cur;
                    c.Rows[newStream] = stored;
                    result.Rows = [EvalAll(c, ret.Value.Nodes)];
                }
            }
            return result;
        })
        {
            StmtType = Kind(ret != null, Wire.Isc.stmt_update),
            Outputs = ret?.Cols ?? [],
        };
    }

    public Prepared CompileDelete(DeleteStmt st, Scope? parent)
    {
        if (st.CurrentOf != null) throw Err.Unsupported("WHERE CURRENT OF");
        var t = DmlTable(st.Table);
        var s = DmlScope(parent);
        var rel = TargetAccess(t, st.Alias, st.Where, s, out var stream);
        rel = OrderAndRows(rel, st.OrderBy, s);
        var limit = BindLimit(new LimitSpec { RowsFrom = st.Rows, RowsTo = st.RowsTo }, s);
        var ret = BindReturning(st.Returning, t, st.Alias, stream, -1, stream, parent);
        return new DelegatePrepared(c =>
        {
            c.Db.EnsureOwned(t);
            var rt = Dml.GetRuntime(c.Db, t);
            var targets = CollectTargets(c, rel, stream, limit);
            if (ret != null && targets.Count > 1) throw Err.MultipleRows();
            var result = new ExecResult();
            foreach (var (slot, _) in targets)
            {
                var cur = slot.Visible(c.Tx);
                if (cur == null) continue;
                if (ret != null)
                {
                    c.Rows[stream] = cur;
                    result.Rows = [EvalAll(c, ret.Value.Nodes)];
                }
                Dml.Delete(c, rt, slot, cur);
                result.Deleted++;
            }
            return result;
        })
        {
            StmtType = Kind(ret != null, Wire.Isc.stmt_delete),
            Outputs = ret?.Cols ?? [],
        };
    }

    // ------------------------------------------------------------------ UPDATE OR INSERT

    public Prepared CompileUpdateOrInsert(UpdateOrInsertStmt st, Scope? parent)
    {
        var t = DmlTable(st.Table);
        var cols = st.Columns != null
            ? st.Columns.Select(c => ColumnOf(t, c)).ToArray()
            : Enumerable.Range(0, t.Columns.Count).Where(i => !t.Columns[i].IsComputed).ToArray();
        if (st.Values.Count != cols.Length) throw Err.Dsql(-804, "Count of column list and variable list do not match");
        var s = DmlScope(parent);
        var values = st.Values.Select((v, i) => v is KeywordArg { Keyword: "DEFAULT" } ? null : Bind(v, s, t.Columns[cols[i]].Type)).ToArray();
        int[] matching;
        if (st.Matching != null) matching = st.Matching.Select(c => ColumnOf(t, c)).ToArray();
        else
        {
            if (t.PrimaryKey == null) throw Err.Dsql(-104, "UPDATE OR INSERT without MATCHING could not be used with views or tables without primary key");
            matching = Db.Constraints[t.PrimaryKey].Columns;
        }
        foreach (var m in matching)
            if (!cols.Contains(m)) throw Err.Dsql(-104, "UPDATE OR INSERT field list does not match primary key of table " + t.Name);
        var newStream = NewStream(t.Columns.Count);
        var oldStream = NewStream(t.Columns.Count);
        var ret = BindReturning(st.Returning, t, null, newStream, newStream, oldStream, parent);
        // Firebird describes unaliased OLD.<col> of UPDATE OR INSERT ... RETURNING as CONSTANT
        if (ret != null && st.Returning!.Items.Count == ret.Value.Cols.Length)
            for (var i = 0; i < ret.Value.Cols.Length; i++)
                if (st.Returning.Items[i] is { Alias: null, Expr: ColumnRefExpr { Qualifier: "OLD" } })
                    (ret.Value.Cols[i].Name, ret.Value.Cols[i].Field, ret.Value.Cols[i].Relation) = ("CONSTANT", "CONSTANT", "");
        var width = t.Columns.Count;
        return new DelegatePrepared(c =>
        {
            c.Db.EnsureOwned(t);
            var rt = Dml.GetRuntime(c.Db, t);
            var vals = new Value[width];
            var provided = new bool[width];
            for (var k = 0; k < cols.Length; k++)
            {
                if (values[k] == null) continue;
                vals[cols[k]] = values[k]!.Eval(c);
                provided[cols[k]] = true;
            }
            // find matching rows (IS NOT DISTINCT FROM on the matching columns)
            var keyVals = matching.Select(m => provided[m] ? Dml.CastColumn(t, m, vals[m]) : Value.Null).ToArray();
            var targets = new List<(RowSlot, Value[])>();
            foreach (var slot in t.Rows.ToArray())
            {
                if (t.IsGtt && slot.Attachment != c.Tx.Att.Id) continue;
                var row = slot.Visible(c.Tx);
                if (row == null) continue;
                var ok = true;
                for (var k = 0; k < matching.Length && ok; k++)
                {
                    var a = row[matching[k]];
                    var b = keyVals[k];
                    ok = a.IsNull ? b.IsNull : !b.IsNull && Conv.Compare(a, b, t.Columns[matching[k]].Type.Collation) == 0;
                }
                if (ok) targets.Add((slot, row));
            }
            var result = new ExecResult();
            if (targets.Count == 0)
            {
                var row = Dml.PrepareInsertRow(c, rt, vals, provided);
                var stored = Dml.Insert(c, rt, row);
                result.Inserted++;
                if (ret != null)
                {
                    c.Rows[newStream] = stored;
                    c.Rows[oldStream] = new Value[width];
                    result.Rows = [EvalAll(c, ret.Value.Nodes)];
                }
                return result;
            }
            if (ret != null && targets.Count > 1) throw Err.MultipleRows();
            foreach (var (slot, row) in targets)
            {
                var nr = (Value[])row.Clone();
                for (var k = 0; k < cols.Length; k++)
                {
                    var ci = cols[k];
                    nr[ci] = values[k] == null ? (rt.Defaults[ci]?.Eval(c) ?? Value.Null) : vals[ci];
                    nr[ci] = Dml.CastColumn(t, ci, nr[ci]);
                }
                var stored = Dml.Update(c, rt, slot, row, nr);
                result.Updated++;
                if (ret != null)
                {
                    c.Rows[newStream] = stored;
                    c.Rows[oldStream] = row;
                    result.Rows = [EvalAll(c, ret.Value.Nodes)];
                }
            }
            return result;
        })
        {
            StmtType = Kind(ret != null, Wire.Isc.stmt_update),
            Outputs = ret?.Cols ?? [],
        };
    }

    // ------------------------------------------------------------------ MERGE

    public Prepared CompileMerge(MergeStmt st, Scope? parent)
    {
        var t = DmlTable(st.Table);
        var s = DmlScope(parent);
        var source = BuildRef(st.Source, s);
        var tStream = NewStream(t.Columns.Count);
        var targetItem = TableItem(t, st.Alias, tStream);
        s.Add(targetItem);
        var join = new JJoin { Kind = JoinKind.Left, L = source, R = new JLeaf { Item = targetItem, Table = t }, Ast = new JoinRef() };
        BindJoinConditions(source, s);
        foreach (var c in SplitAnd(st.On)) join.On.Add(MakeConj(c, s));
        var rel = PlanTree(join, new List<Conj>(), s);
        var sourceStreams = ItemsOf(source).Select(i => i.Stream).ToArray();
        var whens = new List<(bool Matched, XNode? Cond, bool Delete, (int Col, XNode? Value)[]? Set, int[]? InsCols, XNode?[]? InsValues)>();
        foreach (var w in st.Whens)
        {
            var cond = w.Condition == null ? null : BindBool(w.Condition, s);
            if (w.Matched)
            {
                (int, XNode?)[]? set = null;
                if (!w.Delete)
                    set = w.Set!.Select(a =>
                    {
                        var ci = ColumnOf(t, a.Target.Name, a.Target.Qualifier);
                        return (ci, a.Value is KeywordArg { Keyword: "DEFAULT" } ? null : (XNode?)Bind(a.Value, s, t.Columns[ci].Type));
                    }).ToArray();
                whens.Add((true, cond, w.Delete, set, null, null));
            }
            else
            {
                var cols = w.InsertColumns != null
                    ? w.InsertColumns.Select(c => ColumnOf(t, c)).ToArray()
                    : Enumerable.Range(0, t.Columns.Count).Where(i => !t.Columns[i].IsComputed).ToArray();
                if (w.InsertValues!.Count != cols.Length) throw Err.Dsql(-804, "Count of column list and variable list do not match");
                var vals = w.InsertValues.Select((v, i) => v is KeywordArg { Keyword: "DEFAULT" } ? null : Bind(v, s, t.Columns[cols[i]].Type)).ToArray();
                whens.Add((false, cond, false, null, cols, vals));
            }
        }
        var newStream = NewStream(t.Columns.Count);
        var ret = BindReturning(st.Returning, t, st.Alias, newStream, newStream, tStream, parent);
        var saveStreams = s.Streams.ToArray();
        var width = t.Columns.Count;
        return new DelegatePrepared(c =>
        {
            c.Db.EnsureOwned(t);
            var rt = Dml.GetRuntime(c.Db, t);
            var rows = new List<(Frame Frame, RowSlot? Slot)>();
            var cur = rel.Open(c);
            while (cur.Next()) rows.Add((Frames.Save(c, saveStreams), c.Slots[tStream]));
            var touched = new HashSet<RowSlot>(ReferenceEqualityComparer.Instance);
            var result = new ExecResult();
            foreach (var (frame, slot) in rows)
            {
                Frames.Restore(c, saveStreams, frame);
                if (slot != null)
                {
                    var current = slot.Visible(c.Tx);
                    if (current == null) continue;
                    c.Rows[tStream] = current;
                    foreach (var w in whens)
                    {
                        if (!w.Matched || (w.Cond != null && !w.Cond.IsTrue(c))) continue;
                        if (!touched.Add(slot)) throw Err.Random("Multiple source records cannot match the same target during MERGE");
                        if (w.Delete)
                        {
                            if (ret != null) result.Rows = [EvalAll(c, ret.Value.Nodes)];
                            Dml.Delete(c, rt, slot, current);
                            result.Deleted++;
                        }
                        else
                        {
                            var vals = w.Set!.Select(x => x.Value == null ? rt.Defaults[x.Col]?.Eval(c) ?? Value.Null : x.Value.Eval(c)).ToArray();
                            var nr = (Value[])current.Clone();
                            for (var k = 0; k < vals.Length; k++) nr[w.Set![k].Col] = Dml.CastColumn(t, w.Set[k].Col, vals[k]);
                            var stored = Dml.Update(c, rt, slot, current, nr);
                            result.Updated++;
                            if (ret != null)
                            {
                                c.Rows[newStream] = stored;
                                result.Rows = [EvalAll(c, ret.Value.Nodes)];
                            }
                        }
                        break;
                    }
                }
                else
                {
                    c.Rows[tStream] = new Value[width];
                    foreach (var w in whens)
                    {
                        if (w.Matched || (w.Cond != null && !w.Cond.IsTrue(c))) continue;
                        var vals = new Value[width];
                        var provided = new bool[width];
                        for (var k = 0; k < w.InsCols!.Length; k++)
                        {
                            if (w.InsValues![k] == null) continue;
                            vals[w.InsCols[k]] = w.InsValues[k]!.Eval(c);
                            provided[w.InsCols[k]] = true;
                        }
                        var row = Dml.PrepareInsertRow(c, rt, vals, provided);
                        var stored = Dml.Insert(c, rt, row);
                        result.Inserted++;
                        if (ret != null)
                        {
                            c.Rows[newStream] = stored;
                            result.Rows = [EvalAll(c, ret.Value.Nodes)];
                        }
                        break;
                    }
                }
            }
            if (ret != null && result.Inserted + result.Updated + result.Deleted > 1) throw Err.MultipleRows();
            return result;
        })
        {
            StmtType = Kind(ret != null, Wire.Isc.stmt_update),
            Outputs = ret?.Cols ?? [],
        };
    }

    // ------------------------------------------------------------------ EXECUTE PROCEDURE

    public Prepared CompileExecProc(ExecProcStmt st, Scope? parent)
    {
        var s = DmlScope(parent);
        if (Routine?.SubRoutines != null && Routine.SubRoutines.TryGetValue(st.Name, out var sub) && !sub.IsFunction)
        {
            var cr = GetSubRoutine(sub);
            var subArgs = BindProcArgs(sub.Inputs.Select(i => new RoutineParam { Name = i.Name, Type = ResolveType(i.Type), Default = i.Default }).ToList(), st.Args, s, st.Name);
            var subOuts = sub.Outputs.Select(o => new OutCol { Name = o.Name, Field = o.Name, Type = ResolveType(o.Type) }).ToArray();
            return new DelegatePrepared(c => new ExecResult { Rows = [Routines.RunExecutable(c, cr, EvalAll(c, subArgs))] })
            {
                StmtType = Wire.Isc.stmt_exec_procedure,
                Outputs = subOuts,
            };
        }
        var proc = Db.Procedures.TryGetValue(st.Name, out var p) ? p : throw Err.ObjectUnknown("Procedure", st.Name);
        var args = BindProcArgs(proc.Inputs, st.Args, s, proc.Name);
        var outs = proc.Outputs.Select(o => new OutCol { Name = o.Name, Field = o.Name, Relation = proc.Name, Type = o.Type }).ToArray();
        return new DelegatePrepared(c =>
        {
            var row = Routines.CallExecutable(c, proc, EvalAll(c, args));
            return new ExecResult { Rows = outs.Length > 0 ? [row] : [] };
        })
        {
            StmtType = Wire.Isc.stmt_exec_procedure,
            Outputs = outs,
        };
    }

    // ======================================================================== table runtime

    public static TableRuntime BuildRuntime(Database db, Table t)
    {
        var rt = new TableRuntime { Table = t, Version = db.SchemaVersion };
        var n = t.Columns.Count;
        rt.Defaults = new XNode?[n];
        rt.Identity = new Sequence?[n];
        var computed = new List<(int, RowExpr)>();
        var domainChecks = new List<(int, string, RowExpr)>();
        for (var i = 0; i < n; i++)
        {
            var col = t.Columns[i];
            Domain? dom = col.Domain != null && db.Domains.TryGetValue(col.Domain, out var d) ? d : null;
            var def = col.Default ?? dom?.Default;
            if (def != null)
                rt.Defaults[i] = new Compiler(db, 0).Bind(def, new Scope(null), col.Type);
            if (col.IdentitySequence != null) rt.Identity[i] = db.Sequences[col.IdentitySequence];
            if (col.ComputedBy != null) computed.Add((i, RowExprFor(db, t, col.ComputedBy)));
            if (dom?.Check != null)
            {
                var comp = new Compiler(db, 0);
                var st = comp.NewStream(1);
                var s = new Scope(null);
                s.Add(new SourceItem
                {
                    Name = "VALUE", Stream = st,
                    Cols = [new ColInfo { Name = "VALUE", Type = col.Type, Relation = t.Name, Field = col.Name }],
                });
                var node = comp.BindBool(dom.Check, s);
                domainChecks.Add((i, dom.Name, new RowExpr { Node = node, Streams = comp.StreamCount }));
            }
        }
        rt.Computed = computed.ToArray();
        rt.DomainChecks = domainChecks.ToArray();
        rt.Checks = t.Checks.Select(name =>
        {
            var con = db.Constraints[name];
            return (con, RowExprFor(db, t, con.Check!, bool_: true));
        }).ToArray();
        var uniques = new List<(string, Engine.Index)>();
        if (t.PrimaryKey != null) uniques.Add((t.PrimaryKey, db.Indexes[db.Constraints[t.PrimaryKey].IndexName!]));
        foreach (var u in t.Uniques) uniques.Add((u, db.Indexes[db.Constraints[u].IndexName!]));
        foreach (var ixName in t.Indexes)
        {
            var ix = db.Indexes[ixName];
            if (ix.Unique && ix.Constraint == null && ix.Active) uniques.Add((ix.Name, ix));
        }
        rt.Uniques = uniques.ToArray();
        rt.Fks = t.ForeignKeys.Select(name =>
        {
            var con = db.Constraints[name];
            var parent = db.GetTable(con.RefTable!);
            var refCon = db.Constraints[con.RefConstraint!];
            return new FkInfo { Constraint = con, Columns = con.Columns, Parent = parent, ParentIndex = db.Indexes[refCon.IndexName!] };
        }).ToArray();
        rt.Refs = t.ReferencedBy.Select(name =>
        {
            var con = db.Constraints[name];
            return new RefInfo
            {
                Constraint = con, Child = db.GetTable(con.Table), ChildColumns = con.Columns,
                ChildIndex = db.Indexes[con.IndexName!], ParentColumns = con.RefColumns!,
                Parent = t, ParentIndex = db.Indexes[db.Constraints[con.RefConstraint!].IndexName!],
            };
        }).ToArray();
        for (var slot = 0; slot < 6; slot++) rt.Triggers[slot] = [];
        var triggers = t.Triggers.Select(x => db.Triggers[x]).Where(x => x.Active)
            .OrderBy(x => x.Position).ThenBy(x => x.Name, StringComparer.Ordinal).ToList();
        foreach (var timing in new[] { TriggerTiming.Before, TriggerTiming.After })
            foreach (var ev in new[] { TriggerEvent.Insert, TriggerEvent.Update, TriggerEvent.Delete })
                rt.Triggers[TableRuntime.TriggerSlot(timing, ev)] = triggers.Where(x => x.Timing == timing && (x.Events & ev) != 0).ToArray();
        return rt;
    }

    static RowExpr RowExprFor(Database db, Table t, Expr e, bool bool_ = false)
    {
        var comp = new Compiler(db, 0);
        var st = comp.NewStream(t.Columns.Count);
        var s = new Scope(null);
        s.Add(TableItem(t, null, st));
        var node = bool_ ? comp.BindBool(e, s) : comp.Bind(e, s);
        return new RowExpr { Node = node, Streams = comp.StreamCount };
    }
}
