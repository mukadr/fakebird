using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.EntityFrameworkCore;

namespace Fakebird.Tests;

public enum BlogKind { Personal, Company }

public class Blog
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Url { get; set; }
    public decimal Rating { get; set; }
    public bool Active { get; set; }
    public DateTime Created { get; set; }
    public Guid Token { get; set; }
    public BlogKind Kind { get; set; }
    public int? Views { get; set; }
    public List<Post> Posts { get; set; } = new();
}

public class Post
{
    public long Id { get; set; }
    public int BlogId { get; set; }
    public Blog? Blog { get; set; }
    public string Title { get; set; } = "";
    public string? Body { get; set; }
    public DateTime? Published { get; set; }
}

public class BlogContext(string cs) : DbContext
{
    public DbSet<Blog> Blogs => Set<Blog>();
    public DbSet<Post> Posts => Set<Post>();

    protected override void OnConfiguring(DbContextOptionsBuilder b) => b.UseFirebird(cs);

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<Blog>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Url).HasMaxLength(500);
            e.Property(x => x.Rating).HasPrecision(5, 2);
            e.HasIndex(x => x.Name).IsUnique();
        });
        m.Entity<Post>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200);
            e.HasOne(x => x.Blog).WithMany(x => x.Posts).HasForeignKey(x => x.BlogId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

[Collection("server")]
public class EfCoreTests(ServerFixture fx)
{
    string NewDb() => fx.Server.CreateDatabase("ef_" + Guid.NewGuid().ToString("N")).ConnectionString();

    [Fact]
    public void EnsureCreatedCrudAndQueries()
    {
        var cs = NewDb();
        using (var ctx = new BlogContext(cs))
        {
            Assert.True(ctx.Database.EnsureCreated());
            for (var i = 1; i <= 5; i++)
            {
                var blog = new Blog
                {
                    Name = "blog" + i, Url = i % 2 == 0 ? null : "http://b" + i, Rating = i * 1.25m, Active = i % 2 == 1,
                    Created = new DateTime(2024, 1, i, 10, 30, 0), Token = Guid.NewGuid(), Kind = (BlogKind)(i % 2), Views = i == 3 ? null : i * 100,
                };
                for (var p = 0; p < i; p++) blog.Posts.Add(new Post { Title = $"post {i}.{p}", Body = new string('b', 50), Published = p == 0 ? null : DateTime.Today });
                ctx.Blogs.Add(blog);
            }
            ctx.SaveChanges();
        }
        using (var ctx = new BlogContext(cs))
        {
            Assert.Equal(5, ctx.Blogs.Count());
            Assert.Equal(15, ctx.Posts.Count());
            var active = ctx.Blogs.Where(b => b.Active && b.Rating > 2m).OrderByDescending(b => b.Rating).ToList();
            Assert.Equal(["blog5", "blog3"], active.Select(b => b.Name).ToArray());
            var page = ctx.Blogs.OrderBy(b => b.Name).Skip(1).Take(2).Select(b => new { b.Name, Count = b.Posts.Count }).ToList();
            Assert.Equal("blog2", page[0].Name);
            Assert.Equal(2, page[0].Count);
            var withPosts = ctx.Blogs.Include(b => b.Posts).Single(b => b.Name == "blog4");
            Assert.Equal(4, withPosts.Posts.Count);
            var groups = ctx.Posts.GroupBy(p => p.BlogId).Select(g => new { g.Key, N = g.Count() }).OrderBy(x => x.Key).ToList();
            Assert.Equal(5, groups.Count);
            Assert.Equal(5, groups[^1].N);
            Assert.True(ctx.Blogs.Any(b => b.Url != null && b.Url.Contains("b3")));
            Assert.Equal(1, ctx.Blogs.Count(b => b.Views == null));
            Assert.Equal(3, ctx.Blogs.Count(b => b.Kind == BlogKind.Company));
            var token = ctx.Blogs.Select(b => b.Token).First();
            Assert.Equal(1, ctx.Blogs.Count(b => b.Token == token));
            var names = ctx.Blogs.Where(b => new[] { "blog1", "blog2", "nope" }.Contains(b.Name)).Select(b => b.Name.ToUpper()).ToList();
            Assert.Equal(2, names.Count);
            Assert.Contains("BLOG1", names);
            var since = ctx.Blogs.Where(b => b.Created >= new DateTime(2024, 1, 3)).Count();
            Assert.Equal(3, since);
        }
        using (var ctx = new BlogContext(cs))
        {
            var b = ctx.Blogs.Single(x => x.Name == "blog1");
            b.Rating = 9.99m;
            b.Url = null;
            ctx.SaveChanges();
            ctx.Blogs.Remove(ctx.Blogs.Single(x => x.Name == "blog2"));
            ctx.SaveChanges();
        }
        using (var ctx = new BlogContext(cs))
        {
            Assert.Equal(9.99m, ctx.Blogs.Single(x => x.Name == "blog1").Rating);
            Assert.Equal(4, ctx.Blogs.Count());
            Assert.Equal(13, ctx.Posts.Count()); // cascade delete removed blog2 posts
            Assert.Throws<DbUpdateException>(() =>
            {
                ctx.Blogs.Add(new Blog { Name = "blog1", Created = DateTime.Now });
                ctx.SaveChanges();
            });
        }
        using (var ctx = new BlogContext(cs))
        {
            using var tx = ctx.Database.BeginTransaction();
            ctx.Blogs.Add(new Blog { Name = "temp", Created = DateTime.Now });
            ctx.SaveChanges();
            tx.Rollback();
        }
        using (var ctx = new BlogContext(cs))
        {
            Assert.False(ctx.Blogs.Any(b => b.Name == "temp"));
            var n = ctx.Database.ExecuteSqlRaw("update \"Posts\" set \"Title\" = 'x' where \"Published\" is null");
            Assert.Equal(4, n);
            Assert.Equal(4, ctx.Posts.FromSqlRaw("select * from \"Posts\" where \"Title\" = 'x'").Count());
        }
    }

    [Fact]
    public void Dapper()
    {
        using var cn = fx.NewDatabase("""
            create table product (id integer not null primary key, name varchar(50), price numeric(10,2), stock integer, created timestamp);
            create sequence product_seq;
            """);
        for (var i = 1; i <= 20; i++)
            cn.Execute("insert into product values (next value for product_seq, @Name, @Price, @Stock, @Created)",
                new { Name = "p" + i, Price = i * 1.5m, Stock = i % 3 == 0 ? (int?)null : i, Created = DateTime.Today.AddDays(-i) });
        var list = cn.Query<ProductRow>("select id, name, price, stock, created from product where price > @min order by id", new { min = 20m }).ToList();
        Assert.Equal(7, list.Count);
        Assert.Equal("p14", list[0].Name);
        Assert.Equal(21.00m, list[0].Price);
        var count = cn.ExecuteScalar<int>("select count(*) from product where stock is null");
        Assert.Equal(6, count);
        var ids = cn.Query<int>("select id from product where id in (1, 2, 3) order by id").ToList();
        Assert.Equal([1, 2, 3], ids);
        var multi = cn.QueryFirst<(string Name, decimal Price)>("select name, price from product where id = @id", new { id = 2 });
        Assert.Equal("p2", multi.Name);
        Assert.Equal(20, cn.Execute("update product set stock = coalesce(stock, 0) + 1"));
    }

    public class ProductRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
        public int? Stock { get; set; }
        public DateTime Created { get; set; }
    }
}
