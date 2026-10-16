using System.Net;
using System.Net.Http.Json;
using Fakebird;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SampleApi.Tests;

/// <summary>
/// One Fakebird server per test run. The schema script runs once into a template database, which is frozen
/// into a snapshot; every test gets its own copy-on-write clone (microseconds, no disk).
/// </summary>
public sealed class FakebirdFixture : IDisposable
{
    public FakebirdServer Server { get; } = FakebirdServer.Start();
    public DatabaseSnapshot Template { get; }

    public FakebirdFixture()
    {
        var template = Server.CreateDatabase("template");
        template.RunScript(File.ReadAllText("schema.sql"));
        Template = template.Snapshot();
    }

    /// <summary>A fresh database for one test, pre-loaded with the template schema and seed data.</summary>
    public string NewDatabase() => Server.CreateDatabase("test_" + Guid.NewGuid().ToString("N"), Template).ConnectionString();

    public void Dispose() => Server.Dispose();
}

public class ApiTests(FakebirdFixture fx) : IClassFixture<FakebirdFixture>
{
    HttpClient Client()
    {
        var cs = fx.NewDatabase();
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:Main", cs));
        return factory.CreateClient();
    }

    [Fact]
    public async Task ListsSeededProducts()
    {
        var products = await Client().GetFromJsonAsync<List<Product>>("/products");
        Assert.Equal(["Keyboard", "Mouse"], products!.Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task OrderReducesStockAndShowsInReport()
    {
        var http = Client();
        var created = await http.PostAsJsonAsync("/products", new Product { Name = "Monitor", Price = 199.99m, Stock = 5 });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var monitor = await created.Content.ReadFromJsonAsync<Product>();
        Assert.Equal(HttpStatusCode.OK, (await http.PostAsJsonAsync("/orders", new OrderRequest(monitor!.Id, 2))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync("/orders", new OrderRequest(monitor.Id, 10))).StatusCode);
        var report = await http.GetFromJsonAsync<List<ReportRow>>("/report");
        Assert.Equal("Monitor", report!.Single().ProductName);
        Assert.Equal(399.98m, report.Single().Revenue);
    }

    [Fact]
    public async Task EachTestStartsFromTheTemplate()
    {
        // mutations from other tests are invisible here
        var products = await Client().GetFromJsonAsync<List<Product>>("/products");
        Assert.Equal(2, products!.Count);
        Assert.Equal(3, products.Single(p => p.Name == "Mouse").Stock);
    }

    [Fact]
    public async Task TriggerExceptionSurfacesAsServerError()
    {
        var http = Client();
        var response = await http.PostAsJsonAsync("/orders", new OrderRequest(1, 0));
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
