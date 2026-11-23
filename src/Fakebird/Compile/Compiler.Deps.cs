using Fakebird.Engine;

namespace Fakebird.Compile;

public sealed partial class Compiler
{
    /// <summary>While compiling a stored routine: what it depends on, its RDB$DEPENDENCIES rows.</summary>
    internal HashSet<Dependency>? Deps;
    int _depsOff;

    /// <summary>Records a dependency of the routine (not of a view or a computed column compiled into it).</summary>
    void Dep(string name, string? field, int type)
    {
        if (Deps != null && _depsOff == 0) Deps.Add(new Dependency(name, field, type));
    }

    /// <summary>A column read from a source: of a table (NEW/OLD too), a view or a procedure.</summary>
    void DepOnColumn(SourceItem item, ColInfo ci)
    {
        if (Deps == null) return;
        if (item.Table is { IsSystem: false } t) Dep(t.Name, ci.Name, Dependencies.Relation);
        else if (item.Context == "view") Dep(item.Name, ci.Name, Dependencies.View);
        else if (item.Context == "procedure") Dep(item.Name, ci.Name, Dependencies.Procedure);
    }
}
