using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Fakebird.Engine;
using Fakebird.Types;
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
    /// <summary>
    /// Session time zone of new attachments, as a region ("America/Sao_Paulo") or offset ("-03:00"), like
    /// Firebird's DefaultTimeZone setting; null uses the operating system's zone.
    /// </summary>
    public string? TimeZone { get; set; }
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

    ushort? DefaultZone => _options.TimeZone == null ? null : TimeZones.Parse(_options.TimeZone);
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
            var e = new DbEntry { Name = name, Database = new Database(name) { DefaultTimeZone = DefaultZone } };
            _dbs[name] = e;
            return e;
        }
    }

    /// <summary>
    /// Creates (or replaces) a database, optionally from a snapshot (copy-on-write, O(tables)). The database's
    /// default character set (for text declared without one) is <paramref name="charset"/>, else the snapshot's, else
    /// UTF8; connections still talk UTF8.
    /// </summary>
    public FakeDatabase CreateDatabase(string name, DatabaseSnapshot? from = null, string? charset = null)
    {
        var db = from == null ? new Database(name) : Database.FromSnapshot(from.Frozen, name);
        db.DefaultTimeZone = DefaultZone;
        if (charset != null) db.DefaultCharset = Compile.Compiler.CharsetOf(charset);
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
        // the clone is built outside the server lock: it only reads the frozen snapshot, and attaches to every
        // other database would wait on it
        var fresh = Database.FromSnapshot(snapshot.Frozen, name);
        lock (_dbs)
        {
            if (!_dbs.TryGetValue(name, out var e))
            {
                fresh.DefaultTimeZone = DefaultZone;
                _dbs[name] = new DbEntry { Name = name, Database = fresh };
                return;
            }
            fresh.DefaultTimeZone = e.Database.DefaultTimeZone;
            e.Database = fresh;
            e.Generation++;
        }
    }

    /// <summary>
    /// Writes the committed state of every database (definitions, rows, sequence values) as JSON. Each database is
    /// frozen on its own, as a <see cref="FakeDatabase.Snapshot"/> is; open transactions are left out. The
    /// snapshots the databases were cloned from (or last snapshotted to), and <paramref name="snapshots"/>, are
    /// written once, and each database only as far as it differs from its snapshot (definitions and rows), so a
    /// restore shares the snapshot's storage and parsed definitions between the databases again.
    /// </summary>
    public void Backup(Stream stream, params IEnumerable<DatabaseSnapshot> snapshots)
    {
        DbEntry[] entries;
        lock (_dbs) entries = [.. _dbs.Values];
        var frozen = entries.Select(e => (e.Name, Live: e.Database, Frozen: e.Database.Freeze())).ToList();

        // every snapshot after the one it is based on
        var ids = new Dictionary<Database, int>(ReferenceEqualityComparer.Instance);
        var order = new List<Database>();
        void Add(Database? snap)
        {
            if (snap == null || ids.ContainsKey(snap)) return;
            Add(snap.Origin);
            ids[snap] = ids.Count;
            order.Add(snap);
        }
        foreach (var s in snapshots) Add(s.Frozen);
        foreach (var (_, live, _) in frozen) Add(live.Origin ?? live.LastSnapshot);
        var baseDefs = new Dictionary<object, byte[]>(ReferenceEqualityComparer.Instance);
        Fakebird.Engine.Backup.BaseRef? Base(Database? snap, bool clone)
            => snap == null ? null : new(ids[snap], snap, clone);

        using var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        w.WriteStartObject();
        w.WriteString("format", Fakebird.Engine.Backup.Format);
        w.WriteNumber("version", Fakebird.Engine.Backup.Version);
        w.WriteString("server", ServerVersion);
        w.WriteString("created", DateTimeOffset.UtcNow);
        w.WriteStartArray("snapshots");
        foreach (var snap in order)
            Fakebird.Engine.Backup.Write(w, snap, snap.Name, snap.DefaultTimeZone, snap.NextTransactionId,
                Base(snap.Origin, clone: false), baseDefs, ids[snap]);
        w.WriteEndArray();
        w.WriteStartArray("databases");
        foreach (var (name, live, snap) in frozen)
            Fakebird.Engine.Backup.Write(w, snap, name, live.DefaultTimeZone, live.NextTransactionId,
                live.Origin != null ? Base(live.Origin, clone: true) : Base(live.LastSnapshot, clone: false), baseDefs);
        w.WriteEndArray();
        w.WriteEndObject();
    }

    /// <summary>Writes a backup to a file, replacing it only once the backup is complete.</summary>
    public void Backup(string path, params IEnumerable<DatabaseSnapshot> snapshots)
    {
        var tmp = path + ".tmp";
        using (var f = File.Create(tmp)) Backup(f, snapshots);
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>
    /// Loads a backup written by <see cref="Backup(Stream, IEnumerable{DatabaseSnapshot})"/>: each database in it
    /// is created, or replaced like <see cref="Restore"/> does (pooled connections without open transactions are
    /// re-attached). Databases not in the backup are left alone. Nothing changes if the backup cannot be read.
    /// Returns the backup's snapshots, which the restored databases share storage with: create further databases
    /// from them rather than from new snapshots of the same data.
    /// </summary>
    public IReadOnlyList<DatabaseSnapshot> RestoreBackup(Stream stream)
    {
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;
        if (!root.TryGetProperty("format", out var format) || format.GetString() != Fakebird.Engine.Backup.Format)
            throw new FormatException("Not a Fakebird backup");
        var version = root.GetProperty("version").GetInt32();
        if (version > Fakebird.Engine.Backup.Version)
            throw new FormatException($"Fakebird backup version {version} is newer than this Fakebird ({Fakebird.Engine.Backup.Version})");
        var snapshots = new Dictionary<int, Database>();
        if (root.TryGetProperty("snapshots", out var snaps))
            foreach (var s in snaps.EnumerateArray())
                snapshots[s.GetProperty("id").GetInt32()] = Fakebird.Engine.Backup.Read(s, snapshots, snapshot: true);
        var restored = root.GetProperty("databases").EnumerateArray()
            .Select(d => Fakebird.Engine.Backup.Read(d, snapshots, snapshot: false)).ToList();
        lock (_dbs)
            foreach (var db in restored)
            {
                db.DefaultTimeZone ??= DefaultZone;
                if (_dbs.TryGetValue(db.Name, out var e))
                {
                    e.Database = db;
                    e.Generation++;
                }
                else _dbs[db.Name] = new DbEntry { Name = db.Name, Database = db };
            }
        return snapshots.Values.Select(s => new DatabaseSnapshot(s)).ToList();
    }

    public IReadOnlyList<DatabaseSnapshot> RestoreBackup(string path)
    {
        using var f = File.OpenRead(path);
        return RestoreBackup(f);
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

    /// <summary>Name of the database the snapshot was taken of.</summary>
    public string Name => Frozen.Name.EndsWith("$snapshot", StringComparison.Ordinal) ? Frozen.Name[..^"$snapshot".Length] : Frozen.Name;
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
    /// Session time zone of new attachments to this database (region or "+hh:mm" offset); null is the server's
    /// <see cref="FakebirdOptions.TimeZone"/> or the operating system's zone.
    /// </summary>
    public string? DefaultTimeZone
    {
        get => Db.DefaultTimeZone is { } z ? TimeZones.Name(z) : null;
        set => Db.DefaultTimeZone = value == null ? null : TimeZones.Parse(value);
    }

    /// <summary>
    /// Default character set of the database (ALTER DATABASE SET DEFAULT CHARACTER SET): the character set of the
    /// text columns, domains and routine variables declared without one from then on.
    /// </summary>
    public string DefaultCharset
    {
        get => Charsets.Name(Db.DefaultCharset);
        set => Db.DefaultCharset = Compile.Compiler.CharsetOf(value);
    }

    /// <summary>
    /// Runs an isql-style script (statements separated by ';', SET TERM supported). Each statement runs and
    /// is committed on its own; the first error stops the script with an exception naming the statement.
    /// </summary>
    public void RunScript(string script) => ScriptRunner.Run(Db, script);

    /// <summary>Executes one statement in its own transaction and returns its rows (if any).</summary>
    public List<object?[]> Execute(string sql, params object?[] args) => ScriptRunner.Execute(Db, sql, args);
}
