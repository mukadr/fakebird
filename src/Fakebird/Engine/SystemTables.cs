using Fakebird.Types;

namespace Fakebird.Engine;

/// <summary>Read-only virtual RDB$ relations computed from the catalog on each scan.</summary>
internal static class SystemTables
{
    static readonly SqlType Name = SqlType.Char(63, Charset.UnicodeFss);
    static readonly SqlType Small = SqlType.SmallInt;
    static readonly SqlType Int = SqlType.Integer;
    static readonly SqlType Big = SqlType.BigInt;
    static readonly SqlType Text = SqlType.BlobText;
    static readonly SqlType Bool = SqlType.Boolean;

    static void Add(Database db, string name, (string Name, SqlType Type)[] cols, Func<Database, List<Value[]>> rows)
    {
        var t = new Table { Name = name, IsSystem = true, Virtual = rows };
        foreach (var (n, ty) in cols) t.Columns.Add(new Column { Name = n, Type = ty });
        t.RebuildColumnIndex();
        db.Tables[name] = t;
    }

    static Value S(string? s) => s == null ? Value.Null : Value.Str(s);
    static Value N(string? s, int len = 63) => s == null ? Value.Null : Value.Str(s.PadRight(len));
    static Value I(long v) => Value.Int(v);

    public static void Register(Database db)
    {
        Add(db, "RDB$DATABASE",
        [
            ("RDB$DESCRIPTION", Text), ("RDB$RELATION_ID", Small), ("RDB$SECURITY_CLASS", Name),
            ("RDB$CHARACTER_SET_NAME", Name), ("RDB$LINGER", Int), ("RDB$SQL_SECURITY", Bool),
        ], _ => [[Value.Null, I(128), N("SQL$1"), N("UTF8"), Value.Null, Value.Null]]);

        Add(db, "RDB$RELATIONS",
        [
            ("RDB$VIEW_BLR", SqlType.BlobBinary), ("RDB$VIEW_SOURCE", Text), ("RDB$DESCRIPTION", Text), ("RDB$RELATION_ID", Small),
            ("RDB$SYSTEM_FLAG", Small), ("RDB$DBKEY_LENGTH", Small), ("RDB$FORMAT", Small), ("RDB$FIELD_ID", Small),
            ("RDB$RELATION_NAME", Name), ("RDB$SECURITY_CLASS", Name), ("RDB$EXTERNAL_FILE", SqlType.VarChar(255)),
            ("RDB$RUNTIME", SqlType.BlobBinary), ("RDB$EXTERNAL_DESCRIPTION", SqlType.BlobBinary), ("RDB$OWNER_NAME", Name),
            ("RDB$DEFAULT_CLASS", Name), ("RDB$FLAGS", Small), ("RDB$RELATION_TYPE", Small), ("RDB$SQL_SECURITY", Bool),
        ], d =>
        {
            var rows = new List<Value[]>();
            var id = 128;
            foreach (var t in d.Tables.Values.OrderBy(x => x.Name, StringComparer.Ordinal))
                rows.Add([Value.Null, Value.Null, Value.Null, I(t.IsSystem ? 0 : id++), I(t.IsSystem ? 1 : 0), I(8), I(1), I(t.Columns.Count),
                    N(t.Name), Value.Null, Value.Null, Value.Null, Value.Null, N("SYSDBA"), Value.Null, I(1),
                    I(t.IsGtt ? (t.GttPreserveRows ? 4 : 5) : 0), Value.Null]);
            foreach (var v in d.Views.Values.OrderBy(x => x.Name, StringComparer.Ordinal))
                rows.Add([Value.Null, S(v.Source), Value.Null, I(id++), I(0), I(8), I(1), I(v.Columns.Count),
                    N(v.Name), Value.Null, Value.Null, Value.Null, Value.Null, N("SYSDBA"), Value.Null, I(1), I(1), Value.Null]);
            return rows;
        });

        Add(db, "RDB$RELATION_FIELDS",
        [
            ("RDB$FIELD_NAME", Name), ("RDB$RELATION_NAME", Name), ("RDB$FIELD_SOURCE", Name), ("RDB$BASE_FIELD", Name),
            ("RDB$QUERY_NAME", Name), ("RDB$FIELD_POSITION", Small), ("RDB$UPDATE_FLAG", Small), ("RDB$FIELD_ID", Small),
            ("RDB$VIEW_CONTEXT", Small), ("RDB$DESCRIPTION", Text), ("RDB$DEFAULT_VALUE", SqlType.BlobBinary),
            ("RDB$SYSTEM_FLAG", Small), ("RDB$SECURITY_CLASS", Name), ("RDB$COMPLEX_NAME", Name), ("RDB$NULL_FLAG", Small),
            ("RDB$DEFAULT_SOURCE", Text), ("RDB$COLLATION_ID", Small), ("RDB$GENERATOR_NAME", Name), ("RDB$IDENTITY_TYPE", Small),
        ], d =>
        {
            var rows = new List<Value[]>();
            foreach (var t in d.Tables.Values)
                for (var i = 0; i < t.Columns.Count; i++)
                {
                    var c = t.Columns[i];
                    rows.Add([N(c.Name), N(t.Name), N(FieldSource(t.Name, c, i)), Value.Null, Value.Null, I(i), I(1), I(i), Value.Null,
                        Value.Null, Value.Null, I(t.IsSystem ? 1 : 0), Value.Null, Value.Null, c.NotNull ? I(1) : Value.Null,
                        c.DefaultSource == null ? Value.Null : S("DEFAULT " + c.DefaultSource), I(0), N(c.IdentitySequence),
                        c.IdentitySequence == null ? Value.Null : I(c.IdentityAlways ? 0 : 1)]);
                }
            foreach (var v in d.Views.Values)
                for (var i = 0; i < v.Columns.Count; i++)
                    rows.Add([N(v.Columns[i].Name), N(v.Name), N($"RDB$V_{v.Name}_{i}"), Value.Null, Value.Null, I(i), I(1), I(i), Value.Null,
                        Value.Null, Value.Null, I(0), Value.Null, Value.Null, Value.Null, Value.Null, I(0), Value.Null, Value.Null]);
            return rows;
        });

        Add(db, "RDB$FIELDS",
        [
            ("RDB$FIELD_NAME", Name), ("RDB$QUERY_NAME", Name), ("RDB$VALIDATION_SOURCE", Text), ("RDB$COMPUTED_SOURCE", Text),
            ("RDB$DEFAULT_SOURCE", Text), ("RDB$FIELD_LENGTH", Small), ("RDB$FIELD_SCALE", Small), ("RDB$FIELD_TYPE", Small),
            ("RDB$FIELD_SUB_TYPE", Small), ("RDB$DESCRIPTION", Text), ("RDB$SYSTEM_FLAG", Small), ("RDB$SEGMENT_LENGTH", Small),
            ("RDB$NULL_FLAG", Small), ("RDB$CHARACTER_LENGTH", Small), ("RDB$COLLATION_ID", Small), ("RDB$CHARACTER_SET_ID", Small),
            ("RDB$FIELD_PRECISION", Small), ("RDB$DIMENSIONS", Small),
            // the BLR itself is not modelled: non-null (the source text) exactly when Firebird has one
            ("RDB$COMPUTED_BLR", SqlType.BlobBinary), ("RDB$VALIDATION_BLR", SqlType.BlobBinary), ("RDB$DEFAULT_VALUE", SqlType.BlobBinary),
        ], d =>
        {
            var rows = new List<Value[]>();
            Value[] Field(string name, SqlType t, string? computed, string? defSrc, string? check, bool notNull) =>
            [
                N(name), Value.Null, check == null ? Value.Null : S("CHECK (" + check + ")"), computed == null ? Value.Null : S("(" + computed + ")"),
                defSrc == null ? Value.Null : S("DEFAULT " + defSrc), I(t.ByteLength), I(-t.Scale), I(BlrType(t)), I(SubType(t)),
                Value.Null, I(0), t.Kind == TypeKind.Blob ? I(80) : Value.Null, notNull ? I(1) : Value.Null,
                t.IsString ? I(t.Length) : Value.Null, t.IsTextual ? I(0) : Value.Null, t.IsTextual || t.IsString ? I(t.WireCharsetId) : Value.Null,
                t.IsExact ? I(t.Precision > 0 ? t.Precision : t.Kind switch { TypeKind.SmallInt => 4, TypeKind.Integer => 9, _ => 18 }) : Value.Null,
                Value.Null,
                Blr(computed), Blr(check), Blr(defSrc),
            ];
            static Value Blr(string? source) => source == null ? Value.Null : Value.Bytes(System.Text.Encoding.UTF8.GetBytes(source));
            foreach (var dom in d.Domains.Values) rows.Add(Field(dom.Name, dom.Type, null, dom.DefaultSource, dom.CheckSource, dom.NotNull));
            foreach (var t in d.Tables.Values)
                for (var i = 0; i < t.Columns.Count; i++)
                {
                    var c = t.Columns[i];
                    if (c.Domain != null) continue;
                    rows.Add(Field(FieldSource(t.Name, c, i), c.Type, c.ComputedBySource, c.DefaultSource, null, false));
                }
            foreach (var v in d.Views.Values)
                for (var i = 0; i < v.Columns.Count; i++) rows.Add(Field($"RDB$V_{v.Name}_{i}", v.Columns[i].Type, null, null, null, false));
            return rows;
        });

        Add(db, "RDB$GENERATORS",
        [
            ("RDB$GENERATOR_NAME", Name), ("RDB$GENERATOR_ID", Small), ("RDB$SYSTEM_FLAG", Small), ("RDB$DESCRIPTION", Text),
            ("RDB$SECURITY_CLASS", Name), ("RDB$OWNER_NAME", Name), ("RDB$INITIAL_VALUE", Big), ("RDB$GENERATOR_INCREMENT", Int),
        ], d => d.Sequences.Values.Select((s, i) => new[]
        {
            N(s.Name), I(i + 1), I(s.IsIdentity ? 6 : 0), Value.Null, Value.Null, N("SYSDBA"), I(s.InitialValue), I(s.Increment),
        }).ToList());

        Add(db, "RDB$PROCEDURES",
        [
            ("RDB$PROCEDURE_NAME", Name), ("RDB$PROCEDURE_ID", Small), ("RDB$PROCEDURE_INPUTS", Small), ("RDB$PROCEDURE_OUTPUTS", Small),
            ("RDB$DESCRIPTION", Text), ("RDB$PROCEDURE_SOURCE", Text), ("RDB$SECURITY_CLASS", Name), ("RDB$OWNER_NAME", Name),
            ("RDB$PROCEDURE_TYPE", Small), ("RDB$VALID_BLR", Small), ("RDB$SYSTEM_FLAG", Small), ("RDB$PACKAGE_NAME", Name),
            ("RDB$PRIVATE_FLAG", Small), ("RDB$ENGINE_NAME", Name), ("RDB$ENTRYPOINT", SqlType.VarChar(255)),
        ], d => d.Procedures.Values.Select((p, i) => new[]
        {
            N(p.Name), I(i + 1), I(p.Inputs.Count), I(p.Outputs.Count), Value.Null, S(p.Source), Value.Null, N("SYSDBA"),
            I(p.Selectable ? 1 : 2), I(1), I(0), Value.Null, Value.Null, Value.Null, Value.Null,
        }).ToList());

        Add(db, "RDB$PROCEDURE_PARAMETERS",
        [
            ("RDB$PARAMETER_NAME", Name), ("RDB$PROCEDURE_NAME", Name), ("RDB$PARAMETER_NUMBER", Small), ("RDB$PARAMETER_TYPE", Small),
            ("RDB$FIELD_SOURCE", Name), ("RDB$DESCRIPTION", Text), ("RDB$SYSTEM_FLAG", Small), ("RDB$DEFAULT_SOURCE", Text),
            ("RDB$COLLATION_ID", Small), ("RDB$NULL_FLAG", Small), ("RDB$PARAMETER_MECHANISM", Small), ("RDB$FIELD_NAME", Name),
            ("RDB$RELATION_NAME", Name), ("RDB$PACKAGE_NAME", Name),
        ], d =>
        {
            var rows = new List<Value[]>();
            foreach (var p in d.Procedures.Values)
            {
                for (var i = 0; i < p.Inputs.Count; i++)
                    rows.Add([N(p.Inputs[i].Name), N(p.Name), I(i), I(0), N(p.Inputs[i].Domain ?? "RDB$P_" + p.Name + "_I" + i), Value.Null, I(0),
                        Value.Null, Value.Null, p.Inputs[i].NotNull ? I(1) : Value.Null, I(0), Value.Null, Value.Null, Value.Null]);
                for (var i = 0; i < p.Outputs.Count; i++)
                    rows.Add([N(p.Outputs[i].Name), N(p.Name), I(i), I(1), N(p.Outputs[i].Domain ?? "RDB$P_" + p.Name + "_O" + i), Value.Null, I(0),
                        Value.Null, Value.Null, p.Outputs[i].NotNull ? I(1) : Value.Null, I(0), Value.Null, Value.Null, Value.Null]);
            }
            return rows;
        });

        Add(db, "RDB$FUNCTIONS",
        [
            ("RDB$FUNCTION_NAME", Name), ("RDB$FUNCTION_TYPE", Small), ("RDB$QUERY_NAME", Name), ("RDB$DESCRIPTION", Text),
            ("RDB$MODULE_NAME", SqlType.VarChar(255)), ("RDB$ENTRYPOINT", SqlType.Char(255)), ("RDB$RETURN_ARGUMENT", Small),
            ("RDB$SYSTEM_FLAG", Small), ("RDB$ENGINE_NAME", Name), ("RDB$PACKAGE_NAME", Name), ("RDB$PRIVATE_FLAG", Small),
            ("RDB$FUNCTION_SOURCE", Text), ("RDB$FUNCTION_ID", Small), ("RDB$VALID_BLR", Small), ("RDB$SECURITY_CLASS", Name),
            ("RDB$OWNER_NAME", Name), ("RDB$LEGACY_FLAG", Small), ("RDB$DETERMINISTIC_FLAG", Small),
        ], d => d.Functions.Values.Select((f, i) => new[]
        {
            N(f.Name), Value.Null, Value.Null, Value.Null, Value.Null, Value.Null, I(0), I(0), Value.Null, Value.Null, Value.Null,
            S(f.Source), I(i + 1), I(1), Value.Null, N("SYSDBA"), I(0), I(f.Deterministic ? 1 : 0),
        }).ToList());

        Add(db, "RDB$TRIGGERS",
        [
            ("RDB$TRIGGER_NAME", Name), ("RDB$RELATION_NAME", Name), ("RDB$TRIGGER_SEQUENCE", Small), ("RDB$TRIGGER_TYPE", Big),
            ("RDB$TRIGGER_SOURCE", Text), ("RDB$TRIGGER_BLR", SqlType.BlobBinary), ("RDB$DESCRIPTION", Text), ("RDB$TRIGGER_INACTIVE", Small),
            ("RDB$SYSTEM_FLAG", Small), ("RDB$FLAGS", Small), ("RDB$VALID_BLR", Small), ("RDB$DEBUG_INFO", SqlType.BlobBinary),
            ("RDB$ENGINE_NAME", Name), ("RDB$ENTRYPOINT", SqlType.Char(255)), ("RDB$SQL_SECURITY", Bool),
        ], d => d.Triggers.Values.Select(t => new[]
        {
            N(t.Name), N(t.Table), I(t.Position), I(TriggerType(t)), S(t.Source), Value.Null, Value.Null, I(t.Active ? 0 : 1),
            I(0), I(1), I(1), Value.Null, Value.Null, Value.Null, Value.Null,
        }).ToList());

        Add(db, "RDB$INDICES",
        [
            ("RDB$INDEX_NAME", Name), ("RDB$RELATION_NAME", Name), ("RDB$INDEX_ID", Small), ("RDB$UNIQUE_FLAG", Small),
            ("RDB$DESCRIPTION", Text), ("RDB$SEGMENT_COUNT", Small), ("RDB$INDEX_INACTIVE", Small), ("RDB$INDEX_TYPE", Small),
            ("RDB$FOREIGN_KEY", Name), ("RDB$SYSTEM_FLAG", Small), ("RDB$EXPRESSION_BLR", SqlType.BlobBinary),
            ("RDB$EXPRESSION_SOURCE", Text), ("RDB$STATISTICS", SqlType.Double),
        ], d => d.Indexes.Values.Select((ix, i) =>
        {
            string? fk = null;
            if (ix.Constraint != null && d.Constraints.TryGetValue(ix.Constraint, out var con) && con.Type == ConstraintType.ForeignKey)
                fk = d.Constraints[con.RefConstraint!].IndexName;
            return new[]
            {
                N(ix.Name), N(ix.Table), I(i + 1), I(ix.Unique ? 1 : 0), Value.Null, I(ix.Columns.Length), I(ix.Active ? 0 : 1),
                I(ix.Descending ? 1 : 0), N(fk), I(0), Value.Null, Value.Null, Value.Dbl(0),
            };
        }).ToList());

        Add(db, "RDB$INDEX_SEGMENTS",
        [("RDB$INDEX_NAME", Name), ("RDB$FIELD_NAME", Name), ("RDB$FIELD_POSITION", Small), ("RDB$STATISTICS", SqlType.Double)],
        d =>
        {
            var rows = new List<Value[]>();
            foreach (var ix in d.Indexes.Values)
            {
                if (!d.Tables.TryGetValue(ix.Table, out var t)) continue;
                for (var i = 0; i < ix.Columns.Length; i++)
                    rows.Add([N(ix.Name), N(t.Columns[ix.Columns[i]].Name), I(i), Value.Dbl(0)]);
            }
            return rows;
        });

        Add(db, "RDB$RELATION_CONSTRAINTS",
        [
            ("RDB$CONSTRAINT_NAME", Name), ("RDB$CONSTRAINT_TYPE", SqlType.Char(11, Charset.Ascii)), ("RDB$RELATION_NAME", Name),
            ("RDB$DEFERRABLE", SqlType.Char(3, Charset.Ascii)), ("RDB$INITIALLY_DEFERRED", SqlType.Char(3, Charset.Ascii)), ("RDB$INDEX_NAME", Name),
        ], d =>
        {
            var rows = d.Constraints.Values.Select(c => new[]
            {
                N(c.Name), N(c.Type switch
                {
                    ConstraintType.PrimaryKey => "PRIMARY KEY", ConstraintType.Unique => "UNIQUE", ConstraintType.ForeignKey => "FOREIGN KEY",
                    ConstraintType.Check => "CHECK", _ => "NOT NULL",
                }, 11), N(c.Table), N("NO", 3), N("NO", 3), N(c.IndexName),
            }).ToList();
            foreach (var t in d.Tables.Values)
                foreach (var col in t.Columns)
                    if (col.NotNull && col.NotNullConstraint != null)
                        rows.Add([N(col.NotNullConstraint), N("NOT NULL", 11), N(t.Name), N("NO", 3), N("NO", 3), Value.Null]);
            return rows;
        });

        Add(db, "RDB$REF_CONSTRAINTS",
        [
            ("RDB$CONSTRAINT_NAME", Name), ("RDB$CONST_NAME_UQ", Name), ("RDB$MATCH_OPTION", SqlType.Char(7, Charset.Ascii)),
            ("RDB$UPDATE_RULE", SqlType.Char(11, Charset.Ascii)), ("RDB$DELETE_RULE", SqlType.Char(11, Charset.Ascii)),
        ], d => d.Constraints.Values.Where(c => c.Type == ConstraintType.ForeignKey).Select(c => new[]
        {
            N(c.Name), N(c.RefConstraint), N("FULL", 7), N(Rule(c.OnUpdate), 11), N(Rule(c.OnDelete), 11),
        }).ToList());

        Add(db, "RDB$CHECK_CONSTRAINTS", [("RDB$CONSTRAINT_NAME", Name), ("RDB$TRIGGER_NAME", Name)],
            d => d.Constraints.Values.Where(c => c.Type == ConstraintType.Check).Select(c => new[] { N(c.Name), N("CHECK_" + c.Name) }).ToList());

        Add(db, "RDB$EXCEPTIONS",
        [
            ("RDB$EXCEPTION_NAME", Name), ("RDB$EXCEPTION_NUMBER", Int), ("RDB$MESSAGE", SqlType.VarChar(1021)), ("RDB$DESCRIPTION", Text),
            ("RDB$SYSTEM_FLAG", Small), ("RDB$SECURITY_CLASS", Name), ("RDB$OWNER_NAME", Name),
        ], d => d.Exceptions.Values.Select(e => new[] { N(e.Name), I(e.Number), S(e.Message), Value.Null, I(0), Value.Null, N("SYSDBA") }).ToList());

        Add(db, "RDB$CHARACTER_SETS",
        [
            ("RDB$CHARACTER_SET_NAME", Name), ("RDB$FORM_OF_USE", Name), ("RDB$NUMBER_OF_CHARACTERS", Int), ("RDB$DEFAULT_COLLATE_NAME", Name),
            ("RDB$CHARACTER_SET_ID", Small), ("RDB$SYSTEM_FLAG", Small), ("RDB$DESCRIPTION", Text), ("RDB$FUNCTION_NAME", Name),
            ("RDB$BYTES_PER_CHARACTER", Small),
        ], _ =>
        [
            [N("NONE"), Value.Null, Value.Null, N("NONE"), I(0), I(1), Value.Null, Value.Null, I(1)],
            [N("OCTETS"), Value.Null, Value.Null, N("OCTETS"), I(1), I(1), Value.Null, Value.Null, I(1)],
            [N("ASCII"), Value.Null, Value.Null, N("ASCII"), I(2), I(1), Value.Null, Value.Null, I(1)],
            [N("UNICODE_FSS"), Value.Null, Value.Null, N("UNICODE_FSS"), I(3), I(1), Value.Null, Value.Null, I(3)],
            [N("UTF8"), Value.Null, Value.Null, N("UTF8"), I(4), I(1), Value.Null, Value.Null, I(4)],
            [N("WIN1252"), Value.Null, Value.Null, N("WIN1252"), I(53), I(1), Value.Null, Value.Null, I(1)],
        ]);

        Add(db, "RDB$COLLATIONS",
        [("RDB$COLLATION_NAME", Name), ("RDB$COLLATION_ID", Small), ("RDB$CHARACTER_SET_ID", Small), ("RDB$SYSTEM_FLAG", Small)],
        _ =>
        [
            [N("NONE"), I(0), I(0), I(1)], [N("OCTETS"), I(0), I(1), I(1)], [N("UTF8"), I(0), I(4), I(1)],
            [N("UNICODE"), I(1), I(4), I(1)], [N("UNICODE_CI"), I(2), I(4), I(1)], [N("UNICODE_CI_AI"), I(3), I(4), I(1)],
        ]);

        Add(db, "RDB$TYPES",
        [("RDB$FIELD_NAME", Name), ("RDB$TYPE", Small), ("RDB$TYPE_NAME", Name), ("RDB$DESCRIPTION", Text), ("RDB$SYSTEM_FLAG", Small)],
        _ =>
        [
            [N("RDB$FIELD_TYPE"), I(7), N("SHORT"), Value.Null, I(1)], [N("RDB$FIELD_TYPE"), I(8), N("LONG"), Value.Null, I(1)],
            [N("RDB$FIELD_TYPE"), I(16), N("INT64"), Value.Null, I(1)], [N("RDB$FIELD_TYPE"), I(10), N("FLOAT"), Value.Null, I(1)],
            [N("RDB$FIELD_TYPE"), I(27), N("DOUBLE"), Value.Null, I(1)], [N("RDB$FIELD_TYPE"), I(12), N("DATE"), Value.Null, I(1)],
            [N("RDB$FIELD_TYPE"), I(13), N("TIME"), Value.Null, I(1)], [N("RDB$FIELD_TYPE"), I(35), N("TIMESTAMP"), Value.Null, I(1)],
            [N("RDB$FIELD_TYPE"), I(14), N("TEXT"), Value.Null, I(1)], [N("RDB$FIELD_TYPE"), I(37), N("VARYING"), Value.Null, I(1)],
            [N("RDB$FIELD_TYPE"), I(261), N("BLOB"), Value.Null, I(1)], [N("RDB$FIELD_TYPE"), I(23), N("BOOLEAN"), Value.Null, I(1)],
        ]);
    }

    static string FieldSource(string table, Column c, int i) => c.Domain ?? $"RDB${table}_{i}";

    static string Rule(Sql.RefAction a) => a switch
    {
        Sql.RefAction.Cascade => "CASCADE",
        Sql.RefAction.SetNull => "SET NULL",
        Sql.RefAction.SetDefault => "SET DEFAULT",
        _ => "RESTRICT",
    };

    public static int BlrType(SqlType t) => t.Kind switch
    {
        TypeKind.SmallInt => 7,
        TypeKind.Integer => 8,
        TypeKind.BigInt => 16,
        TypeKind.Float => 10,
        TypeKind.Double => 27,
        TypeKind.Date => 12,
        TypeKind.Time => 13,
        TypeKind.Timestamp => 35,
        TypeKind.Char => 14,
        TypeKind.VarChar => 37,
        TypeKind.Blob => 261,
        TypeKind.Boolean => 23,
        _ => 0,
    };

    static int SubType(SqlType t) => t.Kind switch
    {
        TypeKind.Blob => t.SubType,
        TypeKind.SmallInt or TypeKind.Integer or TypeKind.BigInt => t.Scale > 0 ? t.SubType : 0,
        TypeKind.Char or TypeKind.VarChar => t.Charset == Charset.Octets ? 1 : 0,
        _ => 0,
    };

    static long TriggerType(Trigger t)
    {
        // Firebird encoding: 1-6 single event (before/after insert/update/delete), multi-event uses slots
        long Code(Sql.TriggerEvent e) => e switch { Sql.TriggerEvent.Insert => 1, Sql.TriggerEvent.Update => 2, _ => 3 };
        var events = new[] { Sql.TriggerEvent.Insert, Sql.TriggerEvent.Update, Sql.TriggerEvent.Delete }.Where(e => (t.Events & e) != 0).ToList();
        var before = t.Timing == Sql.TriggerTiming.Before;
        if (events.Count == 1) return (Code(events[0]) - 1) * 2 + (before ? 1 : 2);
        long type = before ? 1 : 2;
        var shift = 1;
        foreach (var e in events)
        {
            type += Code(e) << shift;
            shift += 2;
        }
        return type - 1 + (before ? 0 : 0);
    }
}
