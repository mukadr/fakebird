using System.Collections;
using System.Reflection;
using Fakebird.Sql;

namespace Fakebird.Engine;

/// <summary>What a view's query reads: its FROM contexts and the base columns it references.</summary>
internal static class ViewDeps
{
    /// <summary>The named sources of the query's FROM clauses, in order.</summary>
    public static List<(string Name, string? Alias)> Contexts(SelectQuery q)
    {
        var list = new List<(string, string?)>();
        foreach (var n in Nodes(q).OfType<NamedTableRef>()) list.Add((n.Name, n.Alias));
        return list;
    }

    /// <summary>Per source: a row without a column, then each column the query references (Firebird's rows).</summary>
    public static List<(string Table, string? Column)> Of(Database db, SelectQuery q)
    {
        var sources = Nodes(q).OfType<NamedTableRef>().ToList();
        var columns = new SortedSet<(string, string)>();
        string[] ColumnsOf(string name) => db.Tables.TryGetValue(name, out var t) ? t.Columns.Select(c => c.Name).ToArray()
            : db.Views.TryGetValue(name, out var v) ? v.Columns.Select(c => c.Name).ToArray() : [];
        foreach (var node in Nodes(q))
        {
            switch (node)
            {
                case ColumnRefExpr c:
                {
                    var src = sources.FirstOrDefault(s => c.Qualifier != null ? (s.Alias ?? s.Name) == c.Qualifier : ColumnsOf(s.Name).Contains(c.Name));
                    if (src != null && ColumnsOf(src.Name).Contains(c.Name)) columns.Add((src.Name, c.Name));
                    break;
                }
                case StarExpr star:
                    foreach (var s in sources.Where(s => star.Qualifier == null || (s.Alias ?? s.Name) == star.Qualifier))
                        foreach (var col in ColumnsOf(s.Name)) columns.Add((s.Name, col));
                    break;
            }
        }
        var rows = new List<(string, string?)>();
        foreach (var name in sources.Select(s => s.Name).Distinct())
        {
            rows.Add((name, null));
            rows.AddRange(columns.Where(c => c.Item1 == name).Select(c => (name, (string?)c.Item2)));
        }
        return rows;
    }

    /// <summary>Every node of a syntax tree, depth first.</summary>
    static IEnumerable<object> Nodes(object root)
    {
        var stack = new Stack<object>();
        stack.Push(root);
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        while (stack.Count > 0)
        {
            var o = stack.Pop();
            if (!seen.Add(o)) continue;
            yield return o;
            var children = new List<object>();
            foreach (var f in o.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var v = f.GetValue(o);
                if (v is IList l) { foreach (var x in l) if (IsNode(x)) children.Add(x!); }
                else if (IsNode(v)) children.Add(v!);
            }
            for (var i = children.Count - 1; i >= 0; i--) stack.Push(children[i]);
        }

        static bool IsNode(object? x) => x != null && x.GetType().Namespace == "Fakebird.Sql" && !x.GetType().IsEnum;
    }
}
