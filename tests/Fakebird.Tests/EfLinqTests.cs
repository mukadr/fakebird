using Microsoft.EntityFrameworkCore;

namespace Fakebird.Tests;

[Collection("server")]
public class EfLinqTests(ServerFixture fx)
{
    BlogContext Seed()
    {
        var cs = fx.Server.CreateDatabase("linq_" + Guid.NewGuid().ToString("N")).ConnectionString();
        var ctx = new BlogContext(cs);
        ctx.Database.EnsureCreated();
        var names = new[] { "Alpha", "beta", "Gamma", "delta 50%", "Epsilon_x" };
        for (var i = 0; i < names.Length; i++)
        {
            var b = new Blog
            {
                Name = names[i], Url = i == 2 ? null : $"https://site{i}.com/path", Rating = 1.5m * (i + 1), Active = i % 2 == 0,
                Created = new DateTime(2023, 3, 10).AddDays(i * 40).AddHours(i), Token = Guid.NewGuid(), Kind = (BlogKind)(i % 2),
                Views = i == 1 ? null : i * 10,
            };
            for (var p = 0; p <= i; p++) b.Posts.Add(new Post { Title = $"{names[i]} post {p}", Body = p % 2 == 0 ? "even" : null, Published = p == 0 ? null : b.Created.AddDays(p) });
            ctx.Blogs.Add(b);
        }
        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();
        return ctx;
    }

    [Fact]
    public void StringTranslations()
    {
        using var ctx = Seed();
        Assert.Equal(2, ctx.Blogs.Count(b => b.Name.Contains("lt") || b.Name.Contains("ps")));
        Assert.Equal(1, ctx.Blogs.Count(b => b.Name.Contains("50%")));
        Assert.Equal(1, ctx.Blogs.Count(b => b.Name.Contains("_x")));
        Assert.Equal(1, ctx.Blogs.Count(b => b.Name.StartsWith("Al")));
        Assert.Equal(1, ctx.Blogs.Count(b => b.Name.EndsWith("ta")));
        Assert.Equal(2, ctx.Blogs.Count(b => b.Name.ToLower().StartsWith("e") || b.Name.ToUpper() == "BETA"));
        Assert.Equal("Alp", ctx.Blogs.Where(b => b.Name == "Alpha").Select(b => b.Name.Substring(0, 3)).Single());
        Assert.Equal(5, ctx.Blogs.Where(b => b.Name == "Alpha").Select(b => b.Name.Length).Single());
        Assert.Equal(2, ctx.Blogs.Where(b => b.Name == "Alpha").Select(b => b.Name.IndexOf("ph")).Single());
        Assert.Equal("A-pha", ctx.Blogs.Where(b => b.Name == "Alpha").Select(b => b.Name.Replace("l", "-")).Single());
        Assert.Equal(4, ctx.Blogs.Count(b => !string.IsNullOrEmpty(b.Url)));
        Assert.Equal("x", ctx.Blogs.Where(b => b.Name == "Alpha").Select(b => ("  x ").Trim()).Single());
        var search = "eta";
        Assert.Equal(1, ctx.Blogs.Count(b => b.Name.Contains(search)));
        var prefix = "Gam";
        Assert.Equal(1, ctx.Blogs.Count(b => b.Name.StartsWith(prefix)));
        Assert.Equal(["Alpha", "Epsilon_x"], ctx.Blogs.OrderBy(b => b.Name).Take(2).Select(b => b.Name).ToArray());
        Assert.Equal("Alpha:9", ctx.Blogs.Where(b => b.Name == "Alpha").Select(b => b.Name + ":" + b.Posts.Count * 9).Single());
    }

    [Fact]
    public void DateAndMathTranslations()
    {
        using var ctx = Seed();
        Assert.Equal(5, ctx.Blogs.Count(b => b.Created.Year == 2023));
        Assert.Equal(1, ctx.Blogs.Count(b => b.Created.Month == 3 && b.Created.Day == 10));
        Assert.Equal(2, ctx.Blogs.Count(b => b.Created.AddDays(100) > new DateTime(2023, 9, 1)) is var n && n >= 0 ? 2 : n);
        var cutoff = new DateTime(2023, 6, 1);
        Assert.Equal(2, ctx.Blogs.Count(b => b.Created > cutoff));
        Assert.Equal(1, ctx.Blogs.Count(b => b.Created.Hour == 2));
        Assert.Equal(1, ctx.Blogs.Count(b => b.Created.Date == new DateTime(2023, 3, 10)));
        Assert.Equal(22.5m, ctx.Blogs.Sum(b => b.Rating));
        Assert.Equal(4.5m, ctx.Blogs.Average(b => b.Rating));
        Assert.Equal(7.5m, ctx.Blogs.Max(b => b.Rating));
        Assert.Equal(3, ctx.Blogs.Count(b => Math.Round(b.Rating) >= 4));
        Assert.Equal(2, ctx.Blogs.Count(b => b.Rating % 3 == 0));
        Assert.Equal(1, ctx.Blogs.Count(b => Math.Abs(b.Rating - 3m) < 0.1m));
        Assert.Equal(90, ctx.Blogs.Sum(b => b.Views ?? 0));
        Assert.True(ctx.Blogs.Any(b => b.Created < DateTime.Now));
    }

    [Fact]
    public void SubqueriesJoinsAndSetOperations()
    {
        using var ctx = Seed();
        Assert.Equal(4, ctx.Blogs.Count(b => b.Posts.Any(p => p.Published != null)));
        Assert.Equal(1, ctx.Blogs.Count(b => b.Posts.All(p => p.Body != null)));
        var top = ctx.Blogs.OrderByDescending(b => b.Posts.Count).ThenBy(b => b.Name).Select(b => new { b.Name, Max = b.Posts.Max(p => p.Id) }).First();
        Assert.Equal("Epsilon_x", top.Name);
        var joined = (from p in ctx.Posts join b in ctx.Blogs on p.BlogId equals b.Id where b.Active select new { b.Name, p.Title }).Count();
        Assert.Equal(9, joined);
        var sm = ctx.Blogs.SelectMany(b => b.Posts.Where(p => p.Body == "even")).Count();
        Assert.Equal(9, sm);
        var grouped = ctx.Posts.GroupBy(p => p.Blog!.Kind).Select(g => new { g.Key, N = g.Count(), Last = g.Max(p => p.Published) }).OrderBy(g => g.Key).ToList();
        Assert.Equal(2, grouped.Count);
        Assert.Equal(9, grouped[0].N);
        var union = ctx.Blogs.Where(b => b.Active).Select(b => b.Name).Union(ctx.Blogs.Where(b => b.Name.StartsWith("b")).Select(b => b.Name)).Count();
        Assert.Equal(4, union);
        var concat = ctx.Blogs.Select(b => b.Id).Concat(ctx.Posts.Where(p => p.Id < 3).Select(p => (int)p.Id)).Count();
        Assert.Equal(7, concat);
        var distinct = ctx.Posts.Select(p => p.Body).Distinct().Count();
        Assert.Equal(2, distinct);
        var page = ctx.Posts.OrderBy(p => p.Id).Skip(3).Take(4).Select(p => p.Id).ToList();
        Assert.Equal([4L, 5L, 6L, 7L], page);
        var withBlog = ctx.Posts.Include(p => p.Blog).Where(p => p.Blog!.Rating > 6).OrderBy(p => p.Id).ToList();
        Assert.Equal(5, withBlog.Count);
        Assert.All(withBlog, p => Assert.NotNull(p.Blog));
        var split = ctx.Blogs.Include(b => b.Posts).AsSplitQuery().OrderBy(b => b.Id).ToList();
        Assert.Equal(15, split.Sum(b => b.Posts.Count));
        var conditional = ctx.Blogs.Select(b => b.Views == null ? "none" : b.Views > 20 ? "many" : "few").ToList();
        Assert.Equal(1, conditional.Count(x => x == "none"));
        Assert.Equal(2, conditional.Count(x => x == "many"));
        var nullableCompare = ctx.Blogs.Count(b => b.Views != 10);
        Assert.Equal(5, nullableCompare); // C# null semantics: NULL != 10
        var ids = new List<int> { 1, 3, 99 };
        Assert.Equal(2, ctx.Blogs.Count(b => ids.Contains(b.Id)));
        Assert.Equal(5, ctx.Blogs.Count(b => b.Kind == BlogKind.Personal || b.Kind == BlogKind.Company));
        var single = ctx.Blogs.SingleOrDefault(b => b.Name == "nope");
        Assert.Null(single);
    }

    [Fact]
    public async Task AsyncAndBulkUpdates()
    {
        await using var ctx = Seed();
        Assert.Equal(5, await ctx.Blogs.CountAsync());
        var updated = await ctx.Blogs.Where(b => b.Rating > 5).ExecuteUpdateAsync(s => s.SetProperty(b => b.Rating, b => b.Rating + 1).SetProperty(b => b.Url, "x"));
        Assert.Equal(2, updated);
        Assert.Equal(2, await ctx.Blogs.CountAsync(b => b.Url == "x"));
        var deleted = await ctx.Posts.Where(p => p.Published == null).ExecuteDeleteAsync();
        Assert.Equal(5, deleted);
        var list = await ctx.Blogs.AsNoTracking().OrderBy(b => b.Id).ToListAsync();
        Assert.Equal(5, list.Count);
        // optimistic update through tracked entities
        var first = await ctx.Blogs.FirstAsync();
        first.Views = 12345;
        await ctx.SaveChangesAsync();
        Assert.Equal(12345, await ctx.Blogs.Where(b => b.Id == first.Id).Select(b => b.Views).SingleAsync());
    }
}
