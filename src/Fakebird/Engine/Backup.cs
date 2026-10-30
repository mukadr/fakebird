using System.Globalization;
using System.Text.Json;
using Fakebird.Sql;
using Fakebird.Types;

namespace Fakebird.Engine;

/// <summary>
/// Backup files: the committed state of databases as JSON. Definitions are written field by field together with
/// their source text (what the RDB$ tables show); expressions, views and PSQL bodies are parsed again from that
/// text on restore. Rows are arrays of [RDB$DB_KEY, column values...].
/// <para>
/// Snapshots are written once, before the databases, and a database names the snapshot it was cloned from (or
/// the latest one taken of it) as its base. A table still sharing the base's rows is written as "shared"; in
/// other tables a row whose values are still the base's row with that key is written as just the key. On
/// restore those tables and rows share the restored snapshot's storage again, as they did before the backup.
/// </para>
/// </summary>
internal static class Backup
{
    public const string Format = "fakebird-backup";
    public const int Version = 3;

    // ======================================================================== write

    /// <summary>The snapshot a database or snapshot is written against: its id in the backup, and whether the
    /// database is a clone of it (else the snapshot was taken of the database).</summary>
    public readonly record struct BaseRef(int Id, Database Snapshot, bool IsClone);

    /// <summary>
    /// Writes a frozen database (committed rows only, no GTT rows): a snapshot (with its <paramref name="id"/>) or
    /// the <see cref="Database.Freeze"/> of a live database, whose settings that freezing does not carry are passed.
    /// </summary>
    public static void Write(Utf8JsonWriter w, Database db, string name, ushort? defaultTimeZone, long nextTransactionId,
        BaseRef? @base, Dictionary<object, byte[]> baseDefs, int? id = null)
    {
        w.WriteStartObject();
        if (id != null) w.WriteNumber("id", id.Value);
        w.WriteString("name", name);
        if (@base is { } b)
        {
            w.WriteNumber("base", b.Id);
            Bool(w, "clone", b.IsClone);
        }
        w.WriteString("defaultCharset", db.DefaultCharset.ToString());
        Str(w, "defaultTimeZone", defaultTimeZone is { } z ? TimeZones.Name(z) : null);
        w.WriteNumber("nextTransactionId", nextTransactionId);
        w.WriteStartObject("counters");
        w.WriteNumber("constraint", db.ConstraintNameCounter);
        w.WriteNumber("index", db.IndexNameCounter);
        w.WriteNumber("generator", db.GeneratorNameCounter);
        w.WriteNumber("checkTrigger", db.CheckTriggerCounter);
        w.WriteNumber("exception", db.ExceptionCounter);
        w.WriteEndObject();

        var bs = @base?.Snapshot;
        Defs(w, "domains", db.Domains.Values, d => d.Name, Domain, bs?.Domains, baseDefs);
        Defs(w, "sequences", db.Sequences.Values, x => x.Name, Sequence, bs?.Sequences, baseDefs);
        Defs(w, "exceptions", db.Exceptions.Values, x => x.Name, Exception, bs?.Exceptions, baseDefs);
        Defs(w, "tables", db.Tables.Values.Where(t => !t.IsSystem), t => t.Name, Table, bs?.Tables, baseDefs, t => Rows(w, t, bs));
        Defs(w, "constraints", db.Constraints.Values, c => c.Name, Constraint, bs?.Constraints, baseDefs);
        Defs(w, "indexes", db.Indexes.Values, ix => ix.Name, Index, bs?.Indexes, baseDefs);
        Defs(w, "views", db.Views.Values, v => v.Name, View, bs?.Views, baseDefs);
        Defs(w, "procedures", db.Procedures.Values, p => p.Name, Procedure, bs?.Procedures, baseDefs);
        Defs(w, "functions", db.Functions.Values, f => f.Name, Function, bs?.Functions, baseDefs);
        Defs(w, "triggers", db.Triggers.Values, t => t.Name, Trigger, bs?.Triggers, baseDefs);
        w.WriteEndObject();
    }

    /// <summary>
    /// Writes a catalog section. A definition that reads exactly like the base snapshot's is written as its name
    /// and "same": the restore then takes the base's definition, sharing its parsed expressions and PSQL as a clone
    /// does. <paramref name="baseDefs"/> caches the base definitions' JSON across the databases of one backup.
    /// </summary>
    static void Defs<T>(Utf8JsonWriter w, string section, IEnumerable<T> items, Func<T, string> nameOf,
        Action<Utf8JsonWriter, T> def, Dictionary<string, T>? @base, Dictionary<object, byte[]> baseDefs, Action<T>? after = null)
        where T : class
    {
        w.WriteStartArray(section);
        foreach (var x in items)
        {
            w.WriteStartObject();
            if (@base != null && @base.TryGetValue(nameOf(x), out var b)
                && (ReferenceEquals(x, b) || Json(x, def).AsSpan().SequenceEqual(BaseJson(b))))
            {
                w.WriteString("name", nameOf(x));
                w.WriteBoolean("same", true);
            }
            else def(w, x);
            after?.Invoke(x);
            w.WriteEndObject();
        }
        w.WriteEndArray();

        byte[] BaseJson(T b)
        {
            if (!baseDefs.TryGetValue(b, out var json)) baseDefs[b] = json = Json(b, def);
            return json;
        }
    }

    static byte[] Json<T>(T x, Action<Utf8JsonWriter, T> def)
    {
        var buf = new System.Buffers.ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(buf))
        {
            w.WriteStartObject();
            def(w, x);
            w.WriteEndObject();
        }
        return buf.WrittenSpan.ToArray();
    }

    static void Domain(Utf8JsonWriter w, Domain d)
    {
        w.WriteString("name", d.Name);
        Str(w, "description", d.Description);
        Type(w, "type", d.Type);
        Str(w, "defaultSource", d.DefaultSource);
        Bool(w, "notNull", d.NotNull);
        Str(w, "checkSource", d.CheckSource);
    }

    static void Sequence(Utf8JsonWriter w, Sequence s)
    {
        w.WriteString("name", s.Name);
        Str(w, "description", s.Description);
        w.WriteNumber("value", s.Value);
        w.WriteNumber("increment", s.Increment);
        w.WriteNumber("initialValue", s.InitialValue);
        Bool(w, "identity", s.IsIdentity);
    }

    static void Exception(Utf8JsonWriter w, ExceptionDef x)
    {
        w.WriteString("name", x.Name);
        Str(w, "description", x.Description);
        w.WriteString("message", x.Message);
        w.WriteNumber("number", x.Number);
    }

    static void Table(Utf8JsonWriter w, Table t)
    {
        w.WriteString("name", t.Name);
        Str(w, "description", t.Description);
        Bool(w, "gtt", t.IsGtt);
        Bool(w, "gttPreserveRows", t.GttPreserveRows);
        Str(w, "primaryKey", t.PrimaryKey);
        Names(w, "uniques", t.Uniques);
        Names(w, "foreignKeys", t.ForeignKeys);
        Names(w, "checks", t.Checks);
        Names(w, "indexes", t.Indexes);
        Names(w, "referencedBy", t.ReferencedBy);
        Names(w, "triggers", t.Triggers);
        Array(w, "columns", t.Columns, c =>
        {
            w.WriteString("name", c.Name);
            if (c.Position is { } pos) w.WriteNumber("position", pos);
            Str(w, "description", c.Description);
            if (c.Type != null) Type(w, "type", c.Type);
            Bool(w, "notNull", c.NotNull);
            Str(w, "notNullConstraint", c.NotNullConstraint);
            Str(w, "defaultSource", c.DefaultSource);
            Str(w, "domain", c.Domain);
            Str(w, "computedBySource", c.ComputedBySource);
            Str(w, "identitySequence", c.IdentitySequence);
            Bool(w, "identityAlways", c.IdentityAlways);
        });
    }

    /// <summary>A table's rows: "shared" while it still uses the base's rows, else the rows, those unchanged
    /// since the base as their key alone.</summary>
    static void Rows(Utf8JsonWriter w, Table t, Database? @base)
    {
        var bt = @base?.Tables.GetValueOrDefault(t.Name);
        if (bt != null && t.Shared && ReferenceEquals(t.Rows, bt.Rows))
        {
            w.WriteBoolean("shared", true);
            return;
        }
        var baseRows = bt?.Rows.Where(r => r.LatestCommitted != null).ToDictionary(r => r.Key, r => r.LatestCommitted);
        w.WriteStartArray("rows");
        foreach (var slot in t.Rows)
        {
            if (slot.LatestCommitted is not { } data) continue;
            if (baseRows != null && baseRows.TryGetValue(slot.Key, out var same) && ReferenceEquals(same, data))
            {
                w.WriteNumberValue(slot.Key);
                continue;
            }
            w.WriteStartArray();
            w.WriteNumberValue(slot.Key);
            foreach (var v in data) WriteValue(w, v);
            w.WriteEndArray();
        }
        w.WriteEndArray();
    }

    static void Constraint(Utf8JsonWriter w, Constraint c)
    {
        w.WriteString("name", c.Name);
        w.WriteString("type", c.Type.ToString());
        w.WriteString("table", c.Table);
        Ints(w, "columns", c.Columns);
        Str(w, "indexName", c.IndexName);
        Str(w, "refTable", c.RefTable);
        Str(w, "refConstraint", c.RefConstraint);
        if (c.RefColumns != null) Ints(w, "refColumns", c.RefColumns);
        w.WriteString("onDelete", c.OnDelete.ToString());
        w.WriteString("onUpdate", c.OnUpdate.ToString());
        Str(w, "checkSource", c.CheckSource);
        Str(w, "insertTrigger", c.InsertTrigger);
        Str(w, "updateTrigger", c.UpdateTrigger);
        Names(w, "actionTriggers", c.ActionTriggers);
    }

    static void Index(Utf8JsonWriter w, Index ix)
    {
        w.WriteString("name", ix.Name);
        w.WriteString("table", ix.Table);
        Ints(w, "columns", ix.Columns);
        Bool(w, "unique", ix.Unique);
        Bool(w, "descending", ix.Descending);
        w.WriteBoolean("active", ix.Active);
        Str(w, "expressionSource", ix.ExpressionSource);
        Str(w, "description", ix.Description);
        Str(w, "constraint", ix.Constraint);
        Names(w, "collations", ix.Collations.Select(c => c.ToString()));
    }

    static void View(Utf8JsonWriter w, View v)
    {
        w.WriteString("name", v.Name);
        Str(w, "description", v.Description);
        if (v.ColumnNames != null) Names(w, "columnNames", v.ColumnNames);
        w.WriteString("source", v.Source);
        Array(w, "columns", v.Columns, c =>
        {
            w.WriteString("name", c.Name);
            Type(w, "type", c.Type);
        });
        if (v.ColumnDescriptions != null)
        {
            w.WriteStartObject("columnDescriptions");
            foreach (var (k, d) in v.ColumnDescriptions) w.WriteString(k, d);
            w.WriteEndObject();
        }
    }

    static void Procedure(Utf8JsonWriter w, Procedure p)
    {
        w.WriteString("name", p.Name);
        Str(w, "description", p.Description);
        Params(w, "inputs", p.Inputs);
        Params(w, "outputs", p.Outputs);
        Body(w, p.Source, p.SourceLine, p.SourceCol);
        Bool(w, "selectable", p.Selectable);
    }

    static void Function(Utf8JsonWriter w, Function f)
    {
        w.WriteString("name", f.Name);
        Str(w, "description", f.Description);
        Params(w, "inputs", f.Inputs);
        Type(w, "returns", f.Returns);
        Body(w, f.Source, f.SourceLine, f.SourceCol);
        Bool(w, "deterministic", f.Deterministic);
    }

    static void Trigger(Utf8JsonWriter w, Trigger t)
    {
        w.WriteString("name", t.Name);
        Str(w, "description", t.Description);
        Str(w, "table", t.Table);
        w.WriteBoolean("active", t.Active);
        w.WriteString("timing", t.Timing.ToString());
        w.WriteString("events", t.Events.ToString());
        w.WriteString("dbEvent", t.DbEvent.ToString());
        w.WriteNumber("position", t.Position);
        Body(w, t.Source, t.SourceLine, t.SourceCol);
    }

    static void Array<T>(Utf8JsonWriter w, string name, IEnumerable<T> items, Action<T> body)
    {
        w.WriteStartArray(name);
        foreach (var x in items)
        {
            w.WriteStartObject();
            body(x);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    static void Str(Utf8JsonWriter w, string name, string? value)
    {
        if (value != null) w.WriteString(name, value);
    }

    static void Bool(Utf8JsonWriter w, string name, bool value)
    {
        if (value) w.WriteBoolean(name, true);
    }

    static void Names(Utf8JsonWriter w, string name, IEnumerable<string> values)
    {
        w.WriteStartArray(name);
        foreach (var v in values) w.WriteStringValue(v);
        w.WriteEndArray();
    }

    static void Ints(Utf8JsonWriter w, string name, int[] values)
    {
        w.WriteStartArray(name);
        foreach (var v in values) w.WriteNumberValue(v);
        w.WriteEndArray();
    }

    static void Type(Utf8JsonWriter w, string name, SqlType t)
    {
        w.WriteStartObject(name);
        w.WriteString("kind", t.Kind.ToString());
        if (t.Length != 0) w.WriteNumber("length", t.Length);
        if (t.Scale != 0) w.WriteNumber("scale", t.Scale);
        if (t.Precision != 0) w.WriteNumber("precision", t.Precision);
        if (t.SubType != 0) w.WriteNumber("subType", t.SubType);
        w.WriteString("charset", t.Charset.ToString());
        w.WriteString("collation", t.Collation.ToString());
        w.WriteEndObject();
    }

    static void Params(Utf8JsonWriter w, string name, List<RoutineParam> ps) => Array(w, name, ps, p =>
    {
        w.WriteString("name", p.Name);
        Type(w, "type", p.Type);
        Bool(w, "notNull", p.NotNull);
        Str(w, "defaultSource", p.DefaultSource);
        Str(w, "domain", p.Domain);
    });

    static void Body(Utf8JsonWriter w, string source, int line, int col)
    {
        w.WriteString("source", source);
        w.WriteNumber("sourceLine", line);
        w.WriteNumber("sourceCol", col);
    }

    /// <summary>NULL, booleans, scale-0 integers and text as plain JSON; everything else tagged by its kind.</summary>
    static void WriteValue(Utf8JsonWriter w, Value v)
    {
        switch (v.Tag)
        {
            case VTag.Null: w.WriteNullValue(); return;
            case VTag.Bool: w.WriteBooleanValue(v.AsBool); return;
            case VTag.String: w.WriteStringValue(v.AsString); return;
            case VTag.Int when v.Scale == 0: w.WriteNumberValue(v.L); return;
        }
        w.WriteStartObject();
        switch (v.Tag)
        {
            case VTag.Int: w.WriteNumber("int", v.L); break;
            case VTag.Int128: w.WriteString("int128", v.AsInt128.ToString(CultureInfo.InvariantCulture)); break;
            case VTag.Double:
                var name = v.IsFloat ? "float" : "double";
                if (double.IsFinite(v.AsDouble)) w.WriteNumber(name, v.AsDouble);
                else w.WriteString(name, v.AsDouble.ToString(CultureInfo.InvariantCulture));
                break;
            case VTag.Bytes: w.WriteBase64String("bytes", v.AsBytes); break;
            case VTag.Date: w.WriteNumber("date", v.L); break;
            case VTag.Time: w.WriteNumber("time", v.L); break;
            case VTag.Timestamp: w.WriteNumber("timestamp", v.L); break;
            case VTag.TimestampTz: w.WriteNumber("timestampTz", v.L); break;
            case VTag.TimeTz: w.WriteNumber("timeTz", v.L); break;
            default: throw new InvalidOperationException("Unexpected value " + v.Tag);
        }
        if (v.Scale != 0 && v.Tag is VTag.Int or VTag.Int128) w.WriteNumber("scale", v.Scale);
        if (v.IsTz) w.WriteString("zone", TimeZones.Name(v.Zone));
        w.WriteEndObject();
    }

    // ======================================================================== read

    /// <summary>Reads a database, or a snapshot when <paramref name="snapshot"/>; <paramref name="snapshots"/>
    /// holds the snapshots read so far, by id.</summary>
    public static Database Read(JsonElement e, IReadOnlyDictionary<int, Database> snapshots, bool snapshot)
    {
        var @base = e.TryGetProperty("base", out var bid) ? snapshots.GetValueOrDefault(bid.GetInt32())
            ?? throw new FormatException("Unknown base snapshot " + bid.GetInt32()) : null;
        var db = new Database(e.GetProperty("name").GetString()!)
        {
            DefaultCharset = Enum<Charset>(e, "defaultCharset"),
            DefaultTimeZone = Str(e, "defaultTimeZone") is { } tz ? TimeZones.Parse(tz) : null,
            NextTransactionId = e.GetProperty("nextTransactionId").GetInt64(),
        };
        var counters = e.GetProperty("counters");
        db.ConstraintNameCounter = counters.GetProperty("constraint").GetInt32();
        db.IndexNameCounter = counters.GetProperty("index").GetInt32();
        db.GeneratorNameCounter = counters.GetProperty("generator").GetInt32();
        db.CheckTriggerCounter = counters.GetProperty("checkTrigger").GetInt32();
        db.ExceptionCounter = counters.GetProperty("exception").GetInt32();

        foreach (var d in Items(e, "domains"))
        {
            if (Same(d, @base?.Domains) is { } bd)
            {
                db.Domains[bd.Name] = bd.Clone();
                continue;
            }
            var dom = new Domain
            {
                Name = Str(d, "name")!, Description = Str(d, "description"), Type = Type(d.GetProperty("type")),
                DefaultSource = Str(d, "defaultSource"), NotNull = Bool(d, "notNull"), CheckSource = Str(d, "checkSource"),
            };
            if (dom.DefaultSource != null) dom.Default = Parser.ParseDefaultSource(dom.DefaultSource, parameter: false);
            if (dom.CheckSource != null) dom.Check = Parser.ParseExprSource(dom.CheckSource);
            db.Domains[dom.Name] = dom;
        }
        foreach (var s in Items(e, "sequences"))
        {
            if (Same(s, @base?.Sequences) is { } bs)
            {
                db.Sequences[bs.Name] = bs.Clone();
                continue;
            }
            var seq = new Sequence
            {
                Name = Str(s, "name")!, Description = Str(s, "description"), Value = s.GetProperty("value").GetInt64(),
                Increment = s.GetProperty("increment").GetInt32(), InitialValue = s.GetProperty("initialValue").GetInt64(),
                IsIdentity = Bool(s, "identity"),
            };
            db.Sequences[seq.Name] = seq;
        }
        foreach (var x in Items(e, "exceptions"))
        {
            if (Same(x, @base?.Exceptions) is { } bx)
            {
                db.Exceptions[bx.Name] = bx; // never changed in place, as in CopyCatalogInto
                continue;
            }
            var ex = new ExceptionDef
            {
                Name = Str(x, "name")!, Description = Str(x, "description"), Message = Str(x, "message")!,
                Number = x.GetProperty("number").GetInt32(),
            };
            db.Exceptions[ex.Name] = ex;
        }
        long maxKey = 0;
        foreach (var t in Items(e, "tables"))
        {
            var table = Same(t, @base?.Tables)?.CloneDef() ?? ReadTable(t);
            db.Tables[table.Name] = table;
            var bt = @base?.Tables.GetValueOrDefault(table.Name);
            if (Bool(t, "shared"))
            {
                table.Rows = (bt ?? throw new FormatException($"Table {table.Name} is not in the base snapshot")).Rows;
                table.Shared = true;
                continue;
            }
            Dictionary<long, Value[]>? baseRows = null;
            foreach (var r in t.GetProperty("rows").EnumerateArray())
            {
                if (r.ValueKind == JsonValueKind.Number)
                {
                    baseRows ??= bt?.Rows.Where(x => x.LatestCommitted != null).ToDictionary(x => x.Key, x => x.LatestCommitted!)
                        ?? throw new FormatException($"Table {table.Name} is not in the base snapshot");
                    var k = r.GetInt64();
                    table.Rows.Add(new RowSlot
                    {
                        Key = k,
                        Committed = new RowVersion
                        {
                            Data = baseRows.TryGetValue(k, out var same) ? same : throw new FormatException($"Row {k} of {table.Name} is not in the base snapshot"),
                        },
                    });
                    maxKey = Math.Max(maxKey, k);
                    continue;
                }
                var data = new Value[r.GetArrayLength() - 1];
                var i = -1;
                long key = 0;
                foreach (var v in r.EnumerateArray())
                {
                    if (i < 0) key = v.GetInt64();
                    else data[i] = ReadValue(v);
                    i++;
                }
                maxKey = Math.Max(maxKey, key);
                table.Rows.Add(new RowSlot { Key = key, Committed = new RowVersion { Data = data } });
            }
        }
        RowSlot.KeepKeysAbove(maxKey);
                foreach (var c in Items(e, "constraints"))
        {
            if (Same(c, @base?.Constraints) is { } bc)
            {
                db.Constraints[bc.Name] = bc.Clone();
                continue;
            }
            var con = new Constraint
            {
                Name = Str(c, "name")!, Type = Enum<ConstraintType>(c, "type"), Table = Str(c, "table")!, Columns = Ints(c, "columns"),
                IndexName = Str(c, "indexName"), RefTable = Str(c, "refTable"), RefConstraint = Str(c, "refConstraint"),
                RefColumns = c.TryGetProperty("refColumns", out _) ? Ints(c, "refColumns") : null,
                OnDelete = Enum<RefAction>(c, "onDelete"), OnUpdate = Enum<RefAction>(c, "onUpdate"),
                CheckSource = Str(c, "checkSource"), InsertTrigger = Str(c, "insertTrigger"), UpdateTrigger = Str(c, "updateTrigger"),
                ActionTriggers = Names(c, "actionTriggers"),
            };
            if (con.CheckSource != null) con.Check = Parser.ParseExprSource(con.CheckSource);
            db.Constraints[con.Name] = con;
        }
        foreach (var x in Items(e, "indexes"))
        {
            var ix = Same(x, @base?.Indexes)?.CloneDef() ?? new Index
            {
                Name = Str(x, "name")!, Table = Str(x, "table")!, Columns = Ints(x, "columns"), Unique = Bool(x, "unique"),
                Descending = Bool(x, "descending"), Active = x.GetProperty("active").GetBoolean(),
                ExpressionSource = Str(x, "expressionSource"), Description = Str(x, "description"), Constraint = Str(x, "constraint"),
                Collations = Names(x, "collations").Select(System.Enum.Parse<Collation>).ToArray(),
            };
            if (db.Tables.TryGetValue(ix.Table, out var t))
            {
                // a table still sharing the base's rows shares its index maps too, as after FromSnapshot
                if (t.Shared && @base!.Indexes.TryGetValue(ix.Name, out var bix) && SameKeys(ix, bix))
                {
                    ix.Map = bix.Map;
                    ix.Shared = true;
                }
                else ix.Rebuild(t.Rows);
            }
            db.Indexes[ix.Name] = ix;
        }
        foreach (var v in Items(e, "views"))
        {
            if (Same(v, @base?.Views) is { } bv)
            {
                db.Views[bv.Name] = bv.Clone();
                continue;
            }
            var view = new View
            {
                Name = Str(v, "name")!, Description = Str(v, "description"),
                ColumnNames = v.TryGetProperty("columnNames", out _) ? Names(v, "columnNames") : null,
                Source = Str(v, "source")!,
                Columns = Items(v, "columns").Select(c => (Str(c, "name")!, Type(c.GetProperty("type")))).ToList(),
            };
            view.Query = Parser.ParseViewSource(view.Source);
            if (v.TryGetProperty("columnDescriptions", out var cd))
            {
                view.ColumnDescriptions = new Dictionary<string, string?>(StringComparer.Ordinal);
                foreach (var p in cd.EnumerateObject()) view.ColumnDescriptions[p.Name] = p.Value.GetString();
            }
            db.Views[view.Name] = view;
        }
        foreach (var p in Items(e, "procedures"))
        {
            if (Same(p, @base?.Procedures) is { } bp)
            {
                db.Procedures[bp.Name] = bp.CloneDef();
                continue;
            }
            var (source, line, col) = Body(p);
            var proc = new Procedure
            {
                Name = Str(p, "name")!, Description = Str(p, "description"), Inputs = Params(p, "inputs"),
                Outputs = Params(p, "outputs"), Source = source, SourceLine = line, SourceCol = col,
                Body = Parser.ParseBodySource(source, line, col), Selectable = Bool(p, "selectable"),
            };
            db.Procedures[proc.Name] = proc;
        }
        foreach (var f in Items(e, "functions"))
        {
            if (Same(f, @base?.Functions) is { } bf)
            {
                db.Functions[bf.Name] = bf.CloneDef();
                continue;
            }
            var (source, line, col) = Body(f);
            var fn = new Function
            {
                Name = Str(f, "name")!, Description = Str(f, "description"), Inputs = Params(f, "inputs"),
                Returns = Type(f.GetProperty("returns")), Source = source, SourceLine = line, SourceCol = col,
                Body = Parser.ParseBodySource(source, line, col), Deterministic = Bool(f, "deterministic"),
            };
            db.Functions[fn.Name] = fn;
        }
        foreach (var t in Items(e, "triggers"))
        {
            if (Same(t, @base?.Triggers) is { } bt)
            {
                db.Triggers[bt.Name] = bt.CloneDef();
                continue;
            }
            var (source, line, col) = Body(t);
            var tr = new Trigger
            {
                Name = Str(t, "name")!, Description = Str(t, "description"), Table = Str(t, "table"),
                Active = t.GetProperty("active").GetBoolean(), Timing = Enum<TriggerTiming>(t, "timing"),
                Events = Enum<TriggerEvent>(t, "events"), DbEvent = Enum<DbTriggerEvent>(t, "dbEvent"),
                Position = t.GetProperty("position").GetInt32(), Source = source, SourceLine = line, SourceCol = col,
                Body = Parser.ParseBodySource(source, line, col),
            };
            db.Triggers[tr.Name] = tr;
        }
        if (snapshot) db.MarkFrozen(@base);
        else if (@base != null && Bool(e, "clone")) db.SetOrigin(@base);
        else db.LastSnapshot = @base;
        return db;
    }

    static Table ReadTable(JsonElement t)
    {
        var table = new Table
        {
            Name = Str(t, "name")!, Description = Str(t, "description"), IsGtt = Bool(t, "gtt"),
            GttPreserveRows = Bool(t, "gttPreserveRows"), PrimaryKey = Str(t, "primaryKey"), Uniques = Names(t, "uniques"),
            ForeignKeys = Names(t, "foreignKeys"), Checks = Names(t, "checks"), Indexes = Names(t, "indexes"),
            ReferencedBy = Names(t, "referencedBy"), Triggers = Names(t, "triggers"),
        };
        foreach (var c in Items(t, "columns"))
        {
            var col = new Column
            {
                Name = Str(c, "name")!, Position = c.TryGetProperty("position", out var pos) ? pos.GetInt32() : null,
                Description = Str(c, "description"), Type = c.TryGetProperty("type", out var ty) ? Type(ty) : null!,
                NotNull = Bool(c, "notNull"), NotNullConstraint = Str(c, "notNullConstraint"),
                DefaultSource = Str(c, "defaultSource"), Domain = Str(c, "domain"),
                ComputedBySource = Str(c, "computedBySource"), IdentitySequence = Str(c, "identitySequence"),
                IdentityAlways = Bool(c, "identityAlways"),
            };
            if (col.DefaultSource != null) col.Default = Parser.ParseDefaultSource(col.DefaultSource, parameter: false);
            if (col.ComputedBySource != null) col.ComputedBy = Parser.ParseExprSource(col.ComputedBySource);
            table.Columns.Add(col);
        }
        table.RebuildColumnIndex();
        return table;
    }

    /// <summary>The base's definition when the backup wrote it as "same".</summary>
    static T? Same<T>(JsonElement e, Dictionary<string, T>? @base) where T : class
    {
        if (!Bool(e, "same")) return null;
        var name = Str(e, "name")!;
        return @base?.GetValueOrDefault(name) ?? throw new FormatException($"{name} is not in the base snapshot");
    }

    static bool SameKeys(Index a, Index b) => a.Table == b.Table && a.Columns.AsSpan().SequenceEqual(b.Columns)
        && a.Collations.AsSpan().SequenceEqual(b.Collations) && a.ExpressionSource == b.ExpressionSource;

    static IEnumerable<JsonElement> Items(JsonElement e, string name) => e.GetProperty(name).EnumerateArray();

    static string? Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? v.GetString() : null;

    static bool Bool(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.GetBoolean();

    static List<string> Names(JsonElement e, string name) => Items(e, name).Select(v => v.GetString()!).ToList();

    static int[] Ints(JsonElement e, string name) => Items(e, name).Select(v => v.GetInt32()).ToArray();

    static T Enum<T>(JsonElement e, string name) where T : struct, System.Enum => System.Enum.Parse<T>(Str(e, name)!);

    static int Int(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? v.GetInt32() : 0;

    static SqlType Type(JsonElement e) => new(Enum<TypeKind>(e, "kind"), Int(e, "length"), Int(e, "scale"),
        Int(e, "precision"), Int(e, "subType"), Enum<Charset>(e, "charset"), Enum<Collation>(e, "collation"));

    static List<RoutineParam> Params(JsonElement e, string name) => Items(e, name).Select(p =>
    {
        var rp = new RoutineParam
        {
            Name = Str(p, "name")!, Type = Type(p.GetProperty("type")), NotNull = Bool(p, "notNull"),
            DefaultSource = Str(p, "defaultSource"), Domain = Str(p, "domain"),
        };
        if (rp.DefaultSource != null) rp.Default = Parser.ParseDefaultSource(rp.DefaultSource, parameter: true);
        return rp;
    }).ToList();

    static (string Source, int Line, int Col) Body(JsonElement e)
        => (Str(e, "source")!, e.GetProperty("sourceLine").GetInt32(), e.GetProperty("sourceCol").GetInt32());

    static Value ReadValue(JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Null: return Value.Null;
            case JsonValueKind.True: return Value.True;
            case JsonValueKind.False: return Value.False;
            case JsonValueKind.String: return Value.Str(e.GetString()!);
            case JsonValueKind.Number: return Value.Int(e.GetInt64());
        }
        var scale = Int(e, "scale");
        var zone = Str(e, "zone") is { } z ? TimeZones.Parse(z) : (ushort)0;
        foreach (var p in e.EnumerateObject())
            switch (p.Name)
            {
                case "int": return Value.Int(p.Value.GetInt64(), scale);
                case "int128": return Value.Wide(Int128.Parse(p.Value.GetString()!, CultureInfo.InvariantCulture), scale);
                case "float": return Value.Flt((float)Double(p.Value));
                case "double": return Value.Dbl(Double(p.Value));
                case "bytes": return Value.Bytes(p.Value.GetBytesFromBase64());
                case "date": return Value.Date(p.Value.GetInt64());
                case "time": return Value.Time(p.Value.GetInt64());
                case "timestamp": return Value.Timestamp(p.Value.GetInt64());
                case "timestampTz": return Value.TimestampTz(p.Value.GetInt64(), zone);
                case "timeTz": return Value.TimeTz(p.Value.GetInt64(), zone);
            }
        throw new FormatException("Unknown value in backup: " + e.GetRawText());
    }

    static double Double(JsonElement e)
        => e.ValueKind == JsonValueKind.String ? double.Parse(e.GetString()!, CultureInfo.InvariantCulture) : e.GetDouble();
}
