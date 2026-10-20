using System.Net;
using System.Net.Sockets;
using Fakebird.Engine;
using Fakebird.Wire;

namespace Fakebird;

public sealed class FakebirdOptions
{
    /// <summary>TCP port; 0 picks a free ephemeral port.</summary>
    public int Port { get; set; }
    public IPAddress Address { get; set; } = IPAddress.Loopback;
    /// <summary>When set, clients must use this password; otherwise any credentials are accepted.</summary>
    public string? Password { get; set; }
    /// <summary>Allow FbConnection.CreateDatabase to create empty databases.</summary>
    public bool AllowCreateDatabase { get; set; } = true;
    /// <summary>Default user put in generated connection strings.</summary>
    public string User { get; set; } = "SYSDBA";
    /// <summary>Receives diagnostics (failed statements with their SQL text). Defaults to stderr when the
    /// FAKEBIRD_LOG environment variable is set.</summary>
    public Action<string>? Log { get; set; } = DefaultLog();

    static Action<string>? DefaultLog()
    {
        var v = Environment.GetEnvironmentVariable("FAKEBIRD_LOG");
        if (string.IsNullOrEmpty(v)) return null;
        if (v == "1") return Console.Error.WriteLine;
        var gate = new object();
        return msg =>
        {
            lock (gate) File.AppendAllText(v, msg + Environment.NewLine);
        };
    }
}

internal sealed class DbEntry
{
    public string Name = null!;
    public volatile Database Database = null!;
    public volatile int Generation;
}

/// <summary>
/// In-memory fake Firebird 5 server. Speaks the Firebird wire protocol on localhost so the unchanged
/// FirebirdSql.Data.FirebirdClient (and Dapper / EF Core on top of it) can connect to it.
/// </summary>
public sealed class FakebirdServer : IDisposable, IAsyncDisposable
{
    internal const string ServerVersion = "LI-V5.0.4.1812 Fakebird 5.0";

    readonly FakebirdOptions _options;
    readonly TcpListener _listener;
    readonly Dictionary<string, DbEntry> _dbs = new(StringComparer.OrdinalIgnoreCase);
    readonly CancellationTokenSource _cts = new();
    readonly Thread _acceptThread;
    readonly List<Socket> _clients = new();
    volatile bool _disposed;

    public int Port { get; }
    public string Host => _options.Address.ToString();

    FakebirdServer(FakebirdOptions options)
    {
        _options = options;
        Executor.Init();
        _listener = new TcpListener(options.Address, options.Port);
        _listener.Server.NoDelay = true;
        _listener.Start(512);
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "fakebird-accept" };
        _acceptThread.Start();
    }

    public static FakebirdServer Start(FakebirdOptions? options = null) => new(options ?? new FakebirdOptions());

    void AcceptLoop()
    {
        while (!_disposed)
        {
            Socket socket;
            try
            {
                socket = _listener.AcceptSocket();
            }
            catch
            {
                if (_disposed) return;
                continue;
            }
            socket.NoDelay = true;
            lock (_clients) _clients.Add(socket);
            var t = new Thread(() =>
            {
                try { new Connection(this, socket).Run(); }
                finally { lock (_clients) _clients.Remove(socket); }
            }) { IsBackground = true, Name = "fakebird-conn" };
            t.Start();
        }
    }

    internal void Log(string message) => _options.Log?.Invoke(message);
    internal bool Logging => _options.Log != null;

    internal void Authenticate(string? user, string? password)
    {
        if (_options.Password != null && password != _options.Password)
            throw new StatusBuilder().Gds(335544472).Build("28000"); // login
    }

    static string Normalize(string name)
    {
        var n = name.Trim();
        // strip "host:" / "host/port:" prefixes that some clients include
        var colon = n.IndexOf(':');
        if (colon > 1 && !(colon == 1 && char.IsLetter(n[0]))) n = n[(colon + 1)..];
        return n;
    }

    internal DbEntry? Find(string path)
    {
        lock (_dbs) return _dbs.TryGetValue(Normalize(path), out var e) ? e : null;
    }

    internal DbEntry CreateForClient(string path)
    {
        if (!_options.AllowCreateDatabase) throw Err.Random("Fakebird: creating databases from clients is disabled");
        var name = Normalize(path);
        lock (_dbs)
        {
            if (_dbs.ContainsKey(name))
                throw new StatusBuilder().Gds(335544344).Str("create").Str(name)
                    .Gds(Err.isc_random).Str("database already exists").Build("08001");
            var e = new DbEntry { Name = name, Database = new Database(name) };
            _dbs[name] = e;
            return e;
        }
    }

    /// <summary>Creates (or replaces) a database, optionally from a snapshot (copy-on-write, O(tables)).</summary>
    public FakeDatabase CreateDatabase(string name, DatabaseSnapshot? from = null)
    {
        var db = from == null ? new Database(name) : Database.FromSnapshot(from.Frozen, name);
        lock (_dbs)
        {
            if (_dbs.ContainsKey(name)) throw new InvalidOperationException($"Database '{name}' already exists");
            _dbs[name] = new DbEntry { Name = name, Database = db };
        }
        return new FakeDatabase(this, name);
    }

    /// <summary>Replaces the contents of a database with a fresh copy-on-write clone of the snapshot.
    /// Pooled connections without open transactions are re-attached transparently.</summary>
    public void Restore(string name, DatabaseSnapshot snapshot)
    {
        lock (_dbs)
        {
            if (!_dbs.TryGetValue(name, out var e))
            {
                _dbs[name] = new DbEntry { Name = name, Database = Database.FromSnapshot(snapshot.Frozen, name) };
                return;
            }
            e.Database = Database.FromSnapshot(snapshot.Frozen, name);
            e.Generation++;
        }
    }

    public FakeDatabase GetDatabase(string name)
    {
        lock (_dbs)
            return _dbs.ContainsKey(name) ? new FakeDatabase(this, name) : throw new KeyNotFoundException(name);
    }

    public bool DropDatabase(string name)
    {
        lock (_dbs) return _dbs.Remove(name);
    }

    internal Database Engine(string name)
    {
        lock (_dbs) return _dbs.TryGetValue(name, out var e) ? e.Database : throw new KeyNotFoundException(name);
    }

    /// <summary>Connection string for FirebirdSql.Data.FirebirdClient.</summary>
    public string ConnectionString(string database, string? extra = null)
    {
        var cs = $"DataSource={Host};Port={Port};Database={database};User={_options.User};Password={_options.Password ?? "masterkey"};Charset=UTF8;Dialect=3";
        return extra == null ? cs : cs + ";" + extra;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        try { _listener.Stop(); } catch { /* ignored */ }
        lock (_clients)
        {
            foreach (var s in _clients)
                try { s.Dispose(); } catch { /* ignored */ }
            _clients.Clear();
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>Frozen template data used to create databases quickly.</summary>
public sealed class DatabaseSnapshot
{
    internal readonly Database Frozen;
    internal DatabaseSnapshot(Database frozen) => Frozen = frozen;
}

/// <summary>Handle to a database hosted by a <see cref="FakebirdServer"/> for direct (in-process) access.</summary>
public sealed class FakeDatabase
{
    readonly FakebirdServer _server;
    public string Name { get; }

    internal FakeDatabase(FakebirdServer server, string name)
    {
        _server = server;
        Name = name;
    }

    Database Db => _server.Engine(Name);

    public string ConnectionString(string? extra = null) => _server.ConnectionString(Name, extra);

    /// <summary>Freezes the committed state of the database as a snapshot.</summary>
    public DatabaseSnapshot Snapshot() => new(Db.Snapshot());

    /// <summary>
    /// Runs an isql-style script (statements separated by ';', SET TERM supported). Each statement runs and
    /// is committed on its own; the first error stops the script with an exception naming the statement.
    /// </summary>
    public void RunScript(string script) => ScriptRunner.Run(Db, script);

    /// <summary>Executes one statement in its own transaction and returns its rows (if any).</summary>
    public List<object?[]> Execute(string sql, params object?[] args) => ScriptRunner.Execute(Db, sql, args);
}
