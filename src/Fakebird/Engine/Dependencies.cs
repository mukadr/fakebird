using Fakebird.Compile;

namespace Fakebird.Engine;

/// <summary>An object a view or a stored routine depends on: a RDB$DEPENDENCIES row (with a column, or the whole object).</summary>
internal readonly record struct Dependency(string Name, string? Field, int Type);

/// <summary>
/// RDB$DEPENDENCIES: what views, procedures, functions and triggers depend on, and Firebird's check_dependencies,
/// which refuses to drop an object (or a column) that something else still depends on.
/// </summary>
internal static class Dependencies
{
    // Firebird's object types (obj_relation, obj_view, ...)
    public const int Relation = 0, View = 1, Trigger = 2, Procedure = 5, Exception = 7, Domain = 9, Generator = 14, Function = 15;

    /// <summary>Every row: the dependent object, its type, and what it depends on.</summary>
    public static List<(string Dependent, int DependentType, Dependency On)> All(Database db)
    {
        var rows = new List<(string, int, Dependency)>();
        foreach (var v in db.Views.Values)
            foreach (var x in ViewDeps.Of(db, v.Query))
                rows.Add((v.Name, View, new Dependency(x.Table, x.Column, db.Views.ContainsKey(x.Table) ? View : db.Procedures.ContainsKey(x.Table) ? Procedure : Relation)));
        foreach (var p in db.Procedures.Values)
            foreach (var d in p.Deps ??= Capture(p.Inputs.Concat(p.Outputs), deps => Compiler.CompileProcedure(db, p, deps))) rows.Add((p.Name, Procedure, d));
        foreach (var f in db.Functions.Values)
            foreach (var d in f.Deps ??= Capture(f.Inputs, deps => Compiler.CompileFunction(db, f, deps))) rows.Add((f.Name, Function, d));
        foreach (var t in db.Triggers.Values)
        {
            if (t.Table == null || !db.Tables.TryGetValue(t.Table, out var table)) continue;
            foreach (var d in t.Deps ??= Capture([], deps => Compiler.CompileTrigger(db, t, table, deps))) rows.Add((t.Name, Trigger, d));
        }
        return rows;
    }

    /// <summary>
    /// The dependencies of a routine that came without them (a restored backup), found by compiling it; of its
    /// parameters only the domains are known (not TYPE OF COLUMN).
    /// </summary>
    static Dependency[] Capture(IEnumerable<RoutineParam> parameters, Action<HashSet<Dependency>> compile)
    {
        var deps = new HashSet<Dependency>();
        foreach (var p in parameters)
            if (p.Domain != null) deps.Add(new Dependency(p.Domain, null, Domain));
        try { compile(deps); }
        catch (FbError) { /* a body that no longer compiles: what it read before the error */ }
        return deps.ToArray();
    }

    /// <summary>
    /// DROP TABLE / DROP VIEW: each column in turn, then the object itself (Firebird drops the fields first).
    /// <paramref name="droppedToo"/> are the objects that go with it (a table's own triggers).
    /// </summary>
    public static void CheckRelation(Database db, string name, int type, IEnumerable<string> columns, HashSet<(string, int)>? droppedToo = null)
    {
        var all = All(db);
        foreach (var col in columns) Check(all, name, type, col, droppedToo);
        Check(all, name, type, null, droppedToo);
    }

    /// <summary>ALTER TABLE ... DROP column, DROP PROCEDURE / FUNCTION / SEQUENCE / EXCEPTION.</summary>
    public static void Check(Database db, string name, int type, string? field = null) => Check(All(db), name, type, field, null);

    /// <summary>
    /// check_dependencies: "cannot delete COLUMN T.C / TABLE T, there are n dependencies", n the objects (other than
    /// itself and those dropped with it) that depend on the column, or on the object for any reason.
    /// </summary>
    static void Check(List<(string Dependent, int DependentType, Dependency On)> all, string name, int type, string? field, HashSet<(string, int)>? droppedToo)
    {
        var count = all.Where(x => x.On.Name == name && x.On.Type == type && (field == null || x.On.Field == field)
                                   && !(x.Dependent == name && x.DependentType == type)
                                   && droppedToo?.Contains((x.Dependent, x.DependentType)) != true)
            .Select(x => x.Dependent).Distinct().Count();
        if (count == 0) return;
        var b = new StatusBuilder().Gds(Err.isc_no_meta_update).Gds(FbMessages.no_delete);
        b = field != null ? b.Gds(FbMessages.field_name).Str(name + "." + field) : b.Gds(NameCode(type)).Str(name);
        var e = b.Gds(FbMessages.dependency).Num(count).Build("42000");
        e.AtCommit = true;
        throw e;
    }

    static int NameCode(int type) => type switch
    {
        Relation or View => FbMessages.table_name, // a view is named a TABLE too
        Procedure => FbMessages.proc_name,
        Function => FbMessages.udf_name,
        Generator => FbMessages.generator_name,
        Exception => FbMessages.exception_name,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
}
