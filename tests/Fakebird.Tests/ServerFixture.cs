using FirebirdSql.Data.FirebirdClient;

namespace Fakebird.Tests;

public sealed class ServerFixture : IDisposable
{
    public FakebirdServer Server { get; } = FakebirdServer.Start();

    /// <summary>Creates a fresh, uniquely named database and returns an open connection to it.</summary>
    public FbConnection NewDatabase(string? script = null, string? extra = null)
    {
        var db = Server.CreateDatabase("db_" + Guid.NewGuid().ToString("N"));
        if (script != null) db.RunScript(script);
        var cn = new FbConnection(db.ConnectionString(extra));
        cn.Open();
        return cn;
    }

    public void Dispose()
    {
        FbConnection.ClearAllPools();
        Server.Dispose();
    }
}

[CollectionDefinition("server")]
public sealed class ServerCollection : ICollectionFixture<ServerFixture>;
