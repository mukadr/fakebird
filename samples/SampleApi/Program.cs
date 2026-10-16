using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var cs = builder.Configuration.GetConnectionString("Main")
    ?? "DataSource=localhost;Database=/data/shop.fdb;User=SYSDBA;Password=masterkey";
builder.Services.AddDbContext<ShopDb>(o => o.UseFirebird(cs));
builder.Services.AddScoped(_ => new FbConnection(cs));
var app = builder.Build();

app.MapGet("/products", async (ShopDb db) => await db.Products.OrderBy(p => p.Name).ToListAsync());

app.MapPost("/products", async (ShopDb db, Product p) =>
{
    db.Products.Add(p);
    await db.SaveChangesAsync();
    return Results.Created($"/products/{p.Id}", p);
});

app.MapPost("/orders", async (ShopDb db, OrderRequest req) =>
{
    var product = await db.Products.FindAsync(req.ProductId);
    if (product == null) return Results.NotFound();
    if (product.Stock < req.Quantity) return Results.Conflict("insufficient stock");
    product.Stock -= req.Quantity;
    db.Orders.Add(new Order { ProductId = product.Id, Quantity = req.Quantity, Total = product.Price * req.Quantity });
    await db.SaveChangesAsync();
    return Results.Ok();
});

// Raw SQL through Dapper, using a stored procedure defined in the schema script.
app.MapGet("/report", async (FbConnection cn) =>
    await cn.QueryAsync<ReportRow>("select product_name as ProductName, units as Units, revenue as Revenue from sales_report order by revenue desc"));

app.Run();

public record OrderRequest(int ProductId, int Quantity);
public record ReportRow(string ProductName, long Units, decimal Revenue);

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int Stock { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal Total { get; set; }
}

public class ShopDb(DbContextOptions<ShopDb> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<Product>().ToTable("PRODUCT").Property(p => p.Id).HasColumnName("ID");
        m.Entity<Product>().Property(p => p.Name).HasColumnName("NAME");
        m.Entity<Product>().Property(p => p.Price).HasColumnName("PRICE");
        m.Entity<Product>().Property(p => p.Stock).HasColumnName("STOCK");
        m.Entity<Order>().ToTable("ORDERS").Property(p => p.Id).HasColumnName("ID");
        m.Entity<Order>().Property(p => p.ProductId).HasColumnName("PRODUCT_ID");
        m.Entity<Order>().Property(p => p.Quantity).HasColumnName("QUANTITY");
        m.Entity<Order>().Property(p => p.Total).HasColumnName("TOTAL");
    }
}

public partial class Program;
