using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Fakebird.Engine;
using Fakebird.Exec;
using Fakebird.Types;

namespace Fakebird.Wire;

/// <summary>One client connection speaking the Firebird wire protocol (versions 10-12).</summary>
internal sealed class Connection
{
    const int InvalidObject = 0xFFFF;

    readonly FakebirdServer _server;
    readonly Socket _socket;
    readonly Xdr _xdr;
    readonly Encoding _enc = new UTF8Encoding(false);
    int _protocol;

    DbEntry? _entry;
    int _generation;
    Attachment? _att;
    Database? _db;

    int _nextHandle = 1;
    int _lastStatement = InvalidObject;
    readonly Dictionary<int, Transaction> _txs = new();
    readonly Dictionary<int, StmtHandle> _stmts = new();
    readonly Dictionary<int, BlobHandle> _blobs = new();
    /// <summary>Blob contents by blob id (written by the client or materialized for fetched rows).</summary>
    readonly Dictionary<long, (byte[] Data, Transaction? Tx)> _blobStore = new();
    long _nextBlobId = 1;

    sealed class StmtHandle
    {
        public int Id;
        public string? Sql;
        public Prepared? Prepared;
        public List<Value[]>? Rows;
        public int Pos;
        public bool Open;
        public ExecResult? Last;
        public Transaction? Tx;
        /// <summary>Error of an open cursor: Firebird reports execution errors of a SELECT on fetch.</summary>
        public FbError? FetchError;
    }

    sealed class BlobHandle
    {
        public int Id;
        public long BlobId;
        public byte[]? Data;
        public MemoryStream? Writer;
        public int Pos;
        public Transaction? Tx;
    }

    public Connection(FakebirdServer server, Socket socket)
    {
        _server = server;
        _socket = socket;
        _xdr = new Xdr(new NetworkStream(socket, ownsSocket: true));
    }

    int NewHandle()
    {
        var h = _nextHandle++;
        if (_nextHandle >= InvalidObject) _nextHandle = 1;
        return h;
    }

    public void Run()
    {
        try
        {
            while (true)
            {
                var op = _xdr.ReadOperation();
                if (!Dispatch(op)) break;
            }
        }
        catch (EndOfStreamException) { }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        finally
        {
            Cleanup();
            try { _xdr.Flush(); } catch { /* ignored */ }
            try { _socket.Dispose(); } catch { /* ignored */ }
        }
    }

    void Cleanup()
    {
        if (_att == null) return;
        lock (_att.Db.Sync) _att.Detach();
        _att = null;
    }

    // ======================================================================== responses

    void WriteStatus(FbError? e)
    {
        if (e == null)
        {
            _xdr.WriteInt32(Isc.arg_gds);
            _xdr.WriteInt32(0);
            _xdr.WriteInt32(Isc.arg_end);
            return;
        }
        foreach (var a in e.Status)
        {
            _xdr.WriteInt32(a.Type);
            if (a.Type is Isc.arg_string or Isc.arg_interpreted) _xdr.WriteString(a.Text ?? "", _enc);
            else _xdr.WriteInt32(a.Number);
        }
        // Firebird does not send isc_arg_sql_state to remote clients: they map the primary code to a SQLSTATE
        _xdr.WriteInt32(Isc.arg_end);
    }

    void Response(int handle = 0, long blobId = 0, ReadOnlySpan<byte> data = default, FbError? error = null)
    {
        _xdr.WriteInt32(Op.Response);
        _xdr.WriteInt32(handle);
        _xdr.WriteInt64(blobId);
        _xdr.WriteBuffer(data);
        WriteStatus(error);
    }

    void Error(FbError e) => Response(error: e);

    static FbError Wrap(Exception ex) => ex as FbError ?? Err.Random("Fakebird internal error: " + ex.GetType().Name + ": " + ex.Message);

    // ======================================================================== dispatch

    bool Dispatch(int op)
    {
        switch (op)
        {
            case Op.Connect: OpConnect(); return true;
            case Op.Attach: OpAttach(create: false); return true;
            case Op.Create: OpAttach(create: true); return true;
            case Op.Detach:
                _xdr.ReadInt32();
                Guard(() =>
                {
                    Cleanup();
                    Response();
                });
                return true;
            case Op.Disconnect:
            case Op.Exit:
                return false;
            case Op.DropDatabase:
                _xdr.ReadInt32();
                Guard(() =>
                {
                    var name = _entry?.Name;
                    Cleanup();
                    if (name != null) _server.DropDatabase(name);
                    Response();
                });
                return true;
            case Op.InfoDatabase: OpInfoDatabase(); return true;
            case Op.Transaction: OpTransaction(); return true;
            case Op.Commit: OpEndTx(commit: true, retain: false); return true;
            case Op.Rollback: OpEndTx(commit: false, retain: false); return true;
            case Op.CommitRetaining: OpEndTx(commit: true, retain: true); return true;
            case Op.RollbackRetaining: OpEndTx(commit: false, retain: true); return true;
            case Op.Prepare:
                _xdr.ReadInt32();
                Response();
                return true;
            case Op.Prepare2:
                _xdr.ReadInt32();
                _xdr.ReadBuffer();
                Response();
                return true;
            case Op.InfoTransaction: OpInfoTransaction(); return true;
            case Op.AllocateStatement:
                _xdr.ReadInt32();
                Guard(() =>
                {
                    RequireAttached();
                    var h = NewHandle();
                    _stmts[h] = new StmtHandle { Id = h };
                    _lastStatement = h;
                    Response(h);
                });
                return true;
            case Op.PrepareStatement: OpPrepare(); return true;
            case Op.InfoSql: OpInfoSql(); return true;
            case Op.Execute: OpExecute(execute2: false); return true;
            case Op.Execute2: OpExecute(execute2: true); return true;
            case Op.ExecImmediate: OpExecImmediate(); return true;
            case Op.Fetch: OpFetch(); return true;
            case Op.FreeStatement: OpFree(); return true;
            case Op.SetCursor:
                _xdr.ReadInt32();
                _xdr.ReadBuffer();
                _xdr.ReadInt32();
                Response();
                return true;
            case Op.CreateBlob: OpCreateBlob(withBpb: false); return true;
            case Op.CreateBlob2: OpCreateBlob(withBpb: true); return true;
            case Op.OpenBlob: OpOpenBlob(withBpb: false); return true;
            case Op.OpenBlob2: OpOpenBlob(withBpb: true); return true;
            case Op.GetSegment: OpGetSegment(); return true;
            case Op.PutSegment: OpPutSegment(batch: false); return true;
            case Op.BatchSegments: OpPutSegment(batch: true); return true;
            case Op.CloseBlob: OpCloseBlob(cancel: false); return true;
            case Op.CancelBlob: OpCloseBlob(cancel: true); return true;
            case Op.InfoBlob: OpInfoBlob(); return true;
            case Op.SeekBlob: OpSeekBlob(); return true;
            case Op.Ping:
                Response();
                return true;
            case Op.Cancel:
                _xdr.ReadInt32();
                return true; // no response
            default:
                // Unknown operation: the stream position is unknown, so fail loudly and drop the connection.
                Response(error: Err.Unsupported($"wire operation {op}"));
                _xdr.Flush();
                return false;
        }
    }

    /// <summary>Runs an op body; FbErrors become error responses.</summary>
    void Guard(Action body)
    {
        try
        {
            body();
        }
        catch (Exception ex) when (ex is not IOException and not EndOfStreamException)
        {
            Error(Wrap(ex));
        }
    }

    void RequireAttached()
    {
        if (_att == null) throw Err.Random("database is not attached");
    }

    /// <summary>
    /// Handles databases restored/replaced under the same name: a connection without open transactions is
    /// transparently re-attached to the current instance (pooled connections keep working after Restore).
    /// </summary>
    Database Db()
    {
        RequireAttached();
        if (_entry!.Generation != _generation)
        {
            if (_txs.Values.Any(t => t.Active))
                throw Err.Random($"database {_entry.Name} was restored while this connection had active transactions");
            lock (_att!.Db.Sync) _att.Detach();
            var db = _entry.Database;
            lock (db.Sync) _att = new Attachment(db) { User = _att.User, Role = _att.Role };
            _db = db;
            _generation = _entry.Generation;
            _txs.Clear();
            foreach (var s in _stmts.Values)
            {
                s.Prepared = null;
                s.Rows = null;
                s.Open = false;
            }
            _blobStore.Clear();
        }
        return _db!;
    }

    // ======================================================================== connect / attach

    void OpConnect()
    {
        _xdr.ReadInt32(); // op_attach
        _xdr.ReadInt32(); // CONNECT_VERSION
        _xdr.ReadInt32(); // architecture
        _xdr.ReadString(_enc); // database path
        var count = _xdr.ReadInt32();
        _xdr.ReadBuffer(); // user identification
        var best = -1;
        var bestType = 0;
        for (var i = 0; i < count; i++)
        {
            var version = _xdr.ReadInt32();
            _xdr.ReadInt32(); // arch
            _xdr.ReadInt32(); // min type
            var maxType = _xdr.ReadInt32();
            _xdr.ReadInt32(); // weight
            var v = version & 0x7FFF;
            if (v is 10 or 11 or 12 && (best < 0 || v > (best & 0x7FFF)))
            {
                best = version;
                bestType = maxType & 0xFF;
            }
        }
        if (best < 0)
        {
            _xdr.WriteInt32(Op.Reject);
            _xdr.Flush();
            throw new IOException("no supported protocol");
        }
        _protocol = best;
        _xdr.WriteInt32(Op.Accept);
        _xdr.WriteInt32(best);
        _xdr.WriteInt32(Isc.ArchGeneric);
        _xdr.WriteInt32(Math.Min(bestType, Isc.PtypeLazySend));
    }

    void OpAttach(bool create)
    {
        _xdr.ReadInt32(); // database object id
        var path = _enc.GetString(_xdr.ReadBuffer());
        var dpb = _xdr.ReadBuffer();
        Guard(() =>
        {
            string? user = null, password = null, role = null;
            var i = 1; // version byte
            while (i + 1 < dpb.Length)
            {
                int type = dpb[i];
                int len = dpb[i + 1];
                var data = dpb.AsSpan(Math.Min(i + 2, dpb.Length), Math.Min(len, Math.Max(0, dpb.Length - i - 2)));
                switch (type)
                {
                    case Isc.dpb_user_name: user = _enc.GetString(data); break;
                    case Isc.dpb_password: password = _enc.GetString(data); break;
                    case Isc.dpb_sql_role_name: role = _enc.GetString(data); break;
                }
                i += 2 + len;
            }
            _server.Authenticate(user, password);
            if (_att != null) Cleanup();
            var entry = create ? _server.CreateForClient(path) : _server.Find(path)
                ?? throw new StatusBuilder().Gds(335544344).Str("open").Str(path)
                    .Gds(Err.isc_random).Str("Fakebird: database '" + path + "' does not exist on this server").Build("08001");
            _entry = entry;
            _generation = entry.Generation;
            _db = entry.Database;
            lock (_db.Sync)
                _att = new Attachment(_db) { User = (user ?? "SYSDBA").ToUpperInvariant(), Role = role?.ToUpperInvariant() ?? "NONE" };
            Response(0);
        });
    }

    void OpInfoDatabase()
    {
        _xdr.ReadInt32(); // handle
        _xdr.ReadInt32(); // incarnation
        var items = _xdr.ReadBuffer();
        var bufLen = _xdr.ReadInt32();
        Guard(() =>
        {
            var b = new InfoBuffer();
            foreach (var item in items)
            {
                switch (item)
                {
                    case Isc.info_end: break;
                    case Isc.info_firebird_version:
                    case Isc.info_isc_version:
                    {
                        var v = _enc.GetBytes(FakebirdServer.ServerVersion);
                        var data = new byte[2 + v.Length];
                        data[0] = 1;
                        data[1] = (byte)v.Length;
                        v.CopyTo(data, 2);
                        b.Bytes(item, data);
                        break;
                    }
                    case Isc.info_ods_version: b.Int(item, 12); break;
                    case Isc.info_ods_minor_version: b.Int(item, 0); break;
                    case Isc.info_page_size: b.Int(item, 8192); break;
                    case Isc.info_db_sql_dialect: b.Bytes(item, [3]); break;
                    case Isc.info_db_read_only: b.Bytes(item, [0]); break;
                    case Isc.info_attachment_id: b.Int(item, _att?.Id ?? 0); break;
                    case Isc.info_implementation: b.Bytes(item, [1, 60, 1]); break;
                    case Isc.info_db_class: b.Bytes(item, [Isc.info_db_class]); break;
                    case Isc.info_db_id:
                    {
                        var name = _enc.GetBytes(_entry?.Name ?? "");
                        var host = _enc.GetBytes("localhost");
                        var data = new byte[3 + name.Length + host.Length];
                        data[0] = 2;
                        data[1] = (byte)name.Length;
                        name.CopyTo(data, 2);
                        data[2 + name.Length] = (byte)host.Length;
                        host.CopyTo(data, 3 + name.Length);
                        b.Bytes(item, data);
                        break;
                    }
                    default:
                        b.Byte(Isc.info_error);
                        b.Byte(2).Byte(0).Byte(item).Byte(0);
                        break;
                }
            }
            b.Byte(Isc.info_end);
            Response(data: b.ToArray());
        });
    }

    // ======================================================================== transactions

    void OpTransaction()
    {
        _xdr.ReadInt32(); // db handle
        var tpb = _xdr.ReadBuffer();
        Guard(() =>
        {
            var db = Db();
            bool snapshot = true, readOnly = false, wait = true;
            var lockTimeout = 0;
            var i = tpb.Length > 0 ? 1 : 0;
            while (i < tpb.Length)
            {
                int item = tpb[i++];
                switch (item)
                {
                    case Isc.tpb_consistency:
                    case Isc.tpb_concurrency: snapshot = true; break;
                    case Isc.tpb_read_committed: snapshot = false; break;
                    case Isc.tpb_read: readOnly = true; break;
                    case Isc.tpb_write: readOnly = false; break;
                    case Isc.tpb_wait: wait = true; break;
                    case Isc.tpb_nowait: wait = false; break;
                    case Isc.tpb_lock_timeout:
                    {
                        var len = i < tpb.Length ? tpb[i] : 0;
                        for (var k = 0; k < len && k < 4; k++) lockTimeout |= tpb[i + 1 + k] << (8 * k);
                        i += 1 + len;
                        break;
                    }
                    case Isc.tpb_lock_read:
                    case Isc.tpb_lock_write:
                    {
                        var len = i < tpb.Length ? tpb[i] : 0;
                        i += 1 + len;
                        break;
                    }
                }
            }
            Transaction tx;
            lock (db.Sync) tx = _att!.StartTransaction(snapshot, readOnly, wait);
            tx.LockTimeout = lockTimeout;
            var h = NewHandle();
            _txs[h] = tx;
            Response(h);
        });
    }

    Transaction Tx(int handle) => _txs.TryGetValue(handle, out var t) && t.Active
        ? t
        : throw new StatusBuilder().Gds(335544332).Build("08003"); // bad_trans_handle

    void OpEndTx(bool commit, bool retain)
    {
        var h = _xdr.ReadInt32();
        Guard(() =>
        {
            var db = Db();
            var tx = Tx(h);
            lock (db.Sync)
            {
                if (commit) tx.Commit(retain);
                else tx.Rollback(retain);
            }
            if (!retain)
            {
                _txs.Remove(h);
                foreach (var s in _stmts.Values)
                    if (s.Tx == tx)
                    {
                        s.Open = false;
                        s.Rows = null;
                    }
                ReleaseTxBlobs(tx);
            }
            Response();
        });
    }

    void ReleaseTxBlobs(Transaction tx)
    {
        List<long>? remove = null;
        foreach (var (id, b) in _blobStore)
            if (b.Tx == tx) (remove ??= new()).Add(id);
        if (remove != null) foreach (var id in remove) _blobStore.Remove(id);
    }

    void OpInfoTransaction()
    {
        var h = _xdr.ReadInt32();
        _xdr.ReadInt32();
        var items = _xdr.ReadBuffer();
        _xdr.ReadInt32();
        Guard(() =>
        {
            var tx = Tx(h);
            var b = new InfoBuffer();
            foreach (var item in items)
            {
                switch (item)
                {
                    case Isc.info_tra_id: b.Int(item, (int)tx.Id); break;
                    case Isc.info_tra_isolation: b.Bytes(item, tx.Snapshot ? [2] : [3, 1]); break;
                    case Isc.info_tra_access: b.Bytes(item, [(byte)(tx.ReadOnly ? 0 : 1)]); break;
                    case Isc.info_end: break;
                    default: b.Byte(Isc.info_error).Byte(2).Byte(0).Byte(item).Byte(0); break;
                }
            }
            b.Byte(Isc.info_end);
            Response(data: b.ToArray());
        });
    }

    // ======================================================================== statements

    StmtHandle Stmt(int h)
    {
        if (h == InvalidObject) h = _lastStatement;
        return _stmts.TryGetValue(h, out var s) ? s : throw new StatusBuilder().Gds(335544485).Build("HY000"); // bad_stmt_handle
    }

    void OpPrepare()
    {
        var txh = _xdr.ReadInt32();
        var sh = _xdr.ReadInt32();
        _xdr.ReadInt32(); // dialect
        var sql = _xdr.ReadString(_enc);
        var items = _xdr.ReadBuffer();
        var bufLen = _xdr.ReadInt32();
        Guard(() =>
        {
            var db = Db();
            var s = Stmt(sh);
            s.Prepared = null;
            s.Rows = null;
            s.Open = false;
            Prepared p;
            try
            {
                lock (db.Sync) p = Executor.Prepare(db, sql);
            }
            catch (FbError e) when (_server.Logging)
            {
                _server.Log($"[fakebird] prepare failed: {e.Message.ReplaceLineEndings(" | ")}\n{sql}");
                throw;
            }
            s.Sql = sql;
            s.Prepared = p;
            Response(0, data: SqlInfo(s, items, bufLen));
        });
    }

    void OpInfoSql()
    {
        var sh = _xdr.ReadInt32();
        _xdr.ReadInt32(); // incarnation
        var items = _xdr.ReadBuffer();
        var bufLen = _xdr.ReadInt32();
        Guard(() =>
        {
            var s = Stmt(sh);
            if (s.Prepared == null) throw Err.Random("statement is not prepared");
            Response(data: SqlInfo(s, items, bufLen));
        });
    }

    byte[] SqlInfo(StmtHandle s, byte[] items, int bufLen)
    {
        var p = s.Prepared!;
        var b = new InfoBuffer();
        var i = 0;
        var startIndex = 0;
        while (i < items.Length)
        {
            var item = items[i++];
            switch (item)
            {
                case Isc.info_end:
                    i = items.Length;
                    break;
                case Isc.info_sql_sqlda_start:
                {
                    var len = items[i++];
                    startIndex = len >= 2 ? items[i] | (items[i + 1] << 8) : 0;
                    i += len;
                    break;
                }
                case Isc.info_sql_select:
                case Isc.info_sql_bind:
                {
                    // collect the per-variable items up to describe_end
                    var wanted = new List<byte>();
                    while (i < items.Length && items[i] != Isc.info_sql_describe_end)
                    {
                        if (items[i] != Isc.info_sql_describe_vars) wanted.Add(items[i]);
                        i++;
                    }
                    i++; // describe_end
                    var cols = item == Isc.info_sql_select
                        ? p.Outputs.Select(o => (o.Type, o.Nullable, o.Field, o.Relation, o.Name)).ToList()
                        : p.ParamTypes.Select(t => (t, true, "", "", "")).ToList();
                    b.Byte(item);
                    b.Int(Isc.info_sql_describe_vars, cols.Count);
                    for (var k = startIndex; k < cols.Count; k++)
                    {
                        if (b.Length > bufLen - 512)
                        {
                            b.Byte(Isc.info_truncated);
                            b.Byte(Isc.info_end);
                            return b.ToArray();
                        }
                        var (type, nullable, field, relation, alias) = cols[k];
                        foreach (var w in wanted)
                        {
                            switch (w)
                            {
                                case Isc.info_sql_sqlda_seq: b.Int(w, k + 1); break;
                                case Isc.info_sql_type: b.Int(w, type.Bound.WireSqlType | (nullable ? 1 : 0)); break;
                                case Isc.info_sql_sub_type: b.Int(w, type.Bound.WireSubType); break;
                                case Isc.info_sql_scale: b.Int(w, -type.Bound.Scale); break;
                                case Isc.info_sql_length: b.Int(w, type.Bound.ByteLength); break;
                                case Isc.info_sql_null_ind: b.Int(w, nullable ? 1 : 0); break;
                                case Isc.info_sql_field: b.Str(w, field); break;
                                case Isc.info_sql_relation: b.Str(w, relation); break;
                                case Isc.info_sql_owner: b.Str(w, relation.Length > 0 ? "SYSDBA" : ""); break;
                                case Isc.info_sql_alias: b.Str(w, alias); break;
                                case Isc.info_sql_relation_alias: b.Str(w, relation); break;
                            }
                        }
                        b.Byte(Isc.info_sql_describe_end);
                    }
                    startIndex = 0;
                    break;
                }
                case Isc.info_sql_stmt_type:
                    b.Int(item, p.StmtType);
                    break;
                case Isc.info_sql_records:
                {
                    var r = s.Last ?? ExecResult.Empty;
                    var inner = new InfoBuffer()
                        .Int(Isc.info_req_update_count, r.Updated)
                        .Int(Isc.info_req_delete_count, r.Deleted)
                        .Int(Isc.info_req_select_count, r.Selected)
                        .Int(Isc.info_req_insert_count, r.Inserted)
                        .Byte(Isc.info_end);
                    b.Bytes(item, inner.ToArray());
                    break;
                }
                case Isc.info_sql_get_plan:
                case Isc.info_sql_explain_plan:
                    b.Str(item, "\nPLAN (FAKEBIRD)");
                    break;
                case Isc.info_sql_stmt_flags:
                    b.Int(item, 0);
                    break;
                case Isc.info_sql_batch_fetch:
                    b.Int(item, 1);
                    break;
                default:
                    b.Byte(Isc.info_error).Byte(2).Byte(0).Byte(item).Byte(0);
                    break;
            }
        }
        b.Byte(Isc.info_end);
        return b.ToArray();
    }

    /// <summary>Re-prepares when the schema changed since prepare (or after a restore).</summary>
    Prepared Current(StmtHandle s, Database db)
    {
        if (s.Sql == null) throw Err.Random("statement is not prepared");
        if (s.Prepared == null || s.Prepared.Version != db.SchemaVersion)
            s.Prepared = Executor.Prepare(db, s.Sql);
        return s.Prepared;
    }

    void OpExecute(bool execute2)
    {
        var sh = _xdr.ReadInt32();
        var txh = _xdr.ReadInt32();
        var inBlr = _xdr.ReadBuffer();
        _xdr.ReadInt32(); // message number
        var msgCount = _xdr.ReadInt32();
        Value[]? rawParams = null;
        FbError? parseError = null;
        List<ParamDesc>? descs = null;
        if (msgCount > 0)
        {
            descs = ParseBlr(inBlr);
            rawParams = ReadMessage(descs, out parseError);
        }
        byte[]? outBlr = null;
        if (execute2)
        {
            outBlr = _xdr.ReadBuffer();
            _xdr.ReadInt32(); // out message number
        }
        ExecResult? result = null;
        StmtHandle? s = null;
        FbError? error = parseError;
        if (error == null)
        {
            try
            {
                var db = Db();
                s = Stmt(sh);
                var tx = Tx(txh);
                lock (db.Sync)
                {
                    var p = Current(s, db);
                    var pars = BindParams(p, rawParams ?? [], descs, tx);
                    s.Rows = null;
                    s.Open = false;
                    s.FetchError = null;
                    if (!execute2 && p.StmtType is Isc.stmt_select or Isc.stmt_select_for_upd)
                    {
                        try { result = Executor.Run(p, tx, pars); }
                        // a DML RETURNING cursor runs its statement at execute; a query's errors wait for the fetch
                        catch (FbError e) when (!e.AtExecute && !p.ReturningCursor)
                        {
                            if (_server.Logging) _server.Log($"[fakebird] execute failed: {e.Message.ReplaceLineEndings(" | ")}\n{s.Sql}");
                            s.FetchError = e;
                            result = new ExecResult { Rows = [] };
                        }
                    }
                    else result = Executor.Run(p, tx, pars);
                    s.Last = result;
                    s.Tx = tx;
                    if (p.StmtType is Isc.stmt_select or Isc.stmt_select_for_upd)
                    {
                        s.Rows = result.Rows ?? [];
                        s.Pos = 0;
                        s.Open = true;
                    }
                    if (!tx.Active) _txs.Remove(txh);
                }
            }
            catch (Exception ex) when (ex is not IOException and not EndOfStreamException)
            {
                error = Wrap(ex);
                if (_server.Logging) _server.Log($"[fakebird] execute failed: {error.Message.ReplaceLineEndings(" | ")}\n{s?.Sql}");
            }
        }
        if (execute2)
        {
            _xdr.WriteInt32(Op.SqlResponse);
            var row = error == null && result?.Rows is { Count: > 0 } ? result.Rows[0] : null;
            if (row != null && s!.Prepared!.Outputs.Length > 0)
            {
                try { row = CoerceRow(s.Prepared.Outputs, row); }
                catch (FbError e) { error = e; row = null; }
            }
            if (row != null && s!.Prepared!.Outputs.Length > 0)
            {
                _xdr.WriteInt32(1);
                WriteRow(s.Prepared.Outputs, row, s.Tx);
            }
            else _xdr.WriteInt32(0);
        }
        Response(error: error);
    }

    void OpExecImmediate()
    {
        var txh = _xdr.ReadInt32();
        _xdr.ReadInt32(); // db handle
        _xdr.ReadInt32(); // dialect
        var sql = _xdr.ReadString(_enc);
        _xdr.ReadBuffer(); // items
        _xdr.ReadInt32(); // buffer length
        Guard(() =>
        {
            var db = Db();
            lock (db.Sync)
            {
                var p = Executor.Prepare(db, sql);
                if (p.ParamTypes.Length > 0) throw Err.Random("EXECUTE IMMEDIATE does not accept parameters");
                if (txh == 0 || !_txs.ContainsKey(txh))
                {
                    var tx = _att!.StartTransaction();
                    try
                    {
                        Executor.Run(p, tx, []);
                        if (tx.Active) tx.Commit();
                    }
                    catch
                    {
                        if (tx.Active) tx.Rollback();
                        throw;
                    }
                }
                else Executor.Run(p, Tx(txh), []);
            }
            Response();
        });
    }

    void OpFetch()
    {
        var sh = _xdr.ReadInt32();
        _xdr.ReadBuffer(); // blr
        _xdr.ReadInt32(); // message number
        var count = _xdr.ReadInt32();
        StmtHandle s;
        try
        {
            s = Stmt(sh);
            if (!s.Open || s.Rows == null) throw new StatusBuilder().Gds(335544577).Build("24000"); // dsql_cursor_not_open
        }
        catch (FbError e)
        {
            Error(e);
            return;
        }
        if (s.FetchError is { } fe)
        {
            s.FetchError = null;
            s.Open = false;
            Error(fe);
            return;
        }
        if (count <= 0) count = 200;
        var outputs = s.Prepared!.Outputs;
        var sent = 0;
        while (sent < count && s.Pos < s.Rows.Count)
        {
            Value[] row;
            try
            {
                row = CoerceRow(outputs, s.Rows[s.Pos]);
            }
            catch (FbError e)
            {
                // like Firebird's server: the rows fetched so far go out, then the error response, which makes the
                // client drop the whole batch
                s.Pos = s.Rows.Count;
                s.Open = false;
                Error(e);
                return;
            }
            s.Pos++;
            _xdr.WriteInt32(Op.FetchResponse);
            _xdr.WriteInt32(0);
            _xdr.WriteInt32(1);
            WriteRow(outputs, row, s.Tx);
            sent++;
        }
        _xdr.WriteInt32(Op.FetchResponse);
        if (s.Pos >= s.Rows.Count && s.FetchError == null)
        {
            _xdr.WriteInt32(100);
            _xdr.WriteInt32(0);
        }
        else
        {
            _xdr.WriteInt32(0);
            _xdr.WriteInt32(0);
        }
    }

    void OpFree()
    {
        var sh = _xdr.ReadInt32();
        var option = _xdr.ReadInt32();
        Guard(() =>
        {
            var h = sh == InvalidObject ? _lastStatement : sh;
            if (_stmts.TryGetValue(h, out var s))
            {
                if (option == Isc.DSQL_drop) _stmts.Remove(h);
                else if (option == Isc.DSQL_unprepare)
                {
                    s.Prepared = null;
                    s.Sql = null;
                }
                s.Open = false;
                s.Rows = null;
            }
            Response(option == Isc.DSQL_drop ? 0 : h);
        });
    }

    // ======================================================================== message encoding

    readonly record struct ParamDesc(int Blr, int Length, int Scale);

    static List<ParamDesc> ParseBlr(byte[] blr)
    {
        var list = new List<ParamDesc>();
        var i = 0;
        if (blr.Length == 0) return list;
        i++; // version
        if (blr[i++] != Isc.blr_begin) throw Err.Random("invalid message BLR");
        if (blr[i++] != Isc.blr_message) throw Err.Random("invalid message BLR");
        i++; // message number
        var count = blr[i] | (blr[i + 1] << 8);
        i += 2;
        for (var k = 0; k < count; k++)
        {
            int type = blr[i++];
            int len = 0, scale = 0;
            switch (type)
            {
                case Isc.blr_text:
                case Isc.blr_varying:
                case Isc.blr_cstring:
                    len = blr[i] | (blr[i + 1] << 8);
                    i += 2;
                    break;
                case Isc.blr_text2:
                case Isc.blr_varying2:
                    i += 2; // charset
                    len = blr[i] | (blr[i + 1] << 8);
                    i += 2;
                    break;
                case Isc.blr_short:
                case Isc.blr_long:
                case Isc.blr_int64:
                case Isc.blr_quad:
                case Isc.blr_int128:
                    scale = (sbyte)blr[i++];
                    break;
                case Isc.blr_blob2:
                    i += 4;
                    break;
            }
            // every value is followed by a blr_short null indicator descriptor
            if (k % 2 == 0) list.Add(new ParamDesc(type, len, scale));
        }
        return list;
    }

    Value[] ReadMessage(List<ParamDesc> descs, out FbError? error)
    {
        error = null;
        var vals = new Value[descs.Count];
        for (var k = 0; k < descs.Count; k++)
        {
            var d = descs[k];
            Value v;
            switch (d.Blr)
            {
                case Isc.blr_text:
                    v = Value.Bytes(_xdr.ReadOpaque(d.Length));
                    break;
                case Isc.blr_varying:
                    v = Value.Bytes(_xdr.ReadBuffer());
                    break;
                case Isc.blr_short:
                case Isc.blr_long:
                    v = Value.Int(_xdr.ReadInt32(), -d.Scale);
                    break;
                case Isc.blr_int64:
                    v = Value.Int(_xdr.ReadInt64(), -d.Scale);
                    break;
                case Isc.blr_quad:
                    v = Value.Int(_xdr.ReadInt64());
                    break;
                case Isc.blr_float:
                    v = Value.Dbl(BitConverter.Int32BitsToSingle(_xdr.ReadInt32()));
                    break;
                case Isc.blr_double:
                case Isc.blr_d_float:
                    v = Value.Dbl(BitConverter.Int64BitsToDouble(_xdr.ReadInt64()));
                    break;
                case Isc.blr_sql_date:
                    v = Value.Date(_xdr.ReadInt32());
                    break;
                case Isc.blr_sql_time:
                    v = Value.Time((uint)_xdr.ReadInt32());
                    break;
                case Isc.blr_timestamp:
                {
                    var day = _xdr.ReadInt32();
                    var time = (uint)_xdr.ReadInt32();
                    v = Value.Timestamp(day * Value.TicksPerDay + time);
                    break;
                }
                case Isc.blr_bool:
                    v = Value.Bool(_xdr.ReadOpaque(1)[0] != 0);
                    break;
                default:
                    error ??= Err.Unsupported("parameter BLR type " + d.Blr);
                    v = Value.Null;
                    break;
            }
            var ind = _xdr.ReadInt32();
            vals[k] = ind == -1 ? Value.Null : v;
        }
        return vals;
    }

    /// <summary>Converts raw wire values into values of the described parameter types.</summary>
    Value[] BindParams(Prepared p, Value[] raw, List<ParamDesc>? descs, Transaction tx)
    {
        if (raw.Length != p.ParamTypes.Length)
            throw new StatusBuilder().Gds(Err.isc_dsql_error).Gds(Err.isc_sqlerr).Num(-804)
                .Gds(335544590).Build("07001"); // dsql_wrong_param_num
        var vals = new Value[raw.Length];
        for (var i = 0; i < raw.Length; i++)
        {
            var v = raw[i];
            var t = p.ParamTypes[i];
            if (v.IsNull)
            {
                vals[i] = v;
                continue;
            }
            if (t.Kind == TypeKind.Null)
            {
                vals[i] = Value.True; // value of an untyped "? IS NULL" marker is irrelevant
                continue;
            }
            if (descs![i].Blr == Isc.blr_quad && t.Kind == TypeKind.Blob)
            {
                var id = v.L;
                if (!_blobStore.TryGetValue(id, out var data)) throw Err.Random("invalid BLOB ID " + id);
                vals[i] = t.SubType == 1 ? Value.Str(_enc.GetString(data.Data)) : Value.Bytes(data.Data);
                continue;
            }
            if (v.Tag == VTag.Bytes && !(t.IsBinary))
            {
                var s = _enc.GetString(v.AsBytes);
                if (descs![i].Blr == Isc.blr_text) s = s.TrimEnd(' ');
                v = Value.Str(s);
            }
            vals[i] = Conv.Cast(v, t);
        }
        return vals;
    }

    /// <summary>
    /// Converts a row to the described output types before anything is written, so a failing value (e.g. an
    /// INT128 result that does not fit the BIGINT of DataTypeCompatibility 3.0) becomes the fetch's error.
    /// </summary>
    static Value[] CoerceRow(OutCol[] cols, Value[] row)
    {
        var r = new Value[cols.Length];
        for (var i = 0; i < cols.Length; i++)
        {
            var v = row[i];
            if (!v.IsNull)
            {
                var t = cols[i].Type;
                v = Coercion.ToType(v, t);
                if (t.Kind == TypeKind.Int128)
                {
                    if (v.Tag == VTag.Int128 && !v.FitsLong)
                        throw new StatusBuilder().Gds(Err.isc_dsql_error).Gds(Err.isc_sqlerr).Num(-303)
                            .Gds(Err.isc_arith_except).Gds(Err.isc_integer_overflow).Build();
                    v = Value.Int(v.L, v.Scale);
                }
            }
            r[i] = v;
        }
        return r;
    }

    void WriteRow(OutCol[] cols, Value[] row, Transaction? tx)
    {
        for (var i = 0; i < cols.Length; i++)
        {
            var t = cols[i].Type.Bound;
            var v = row[i];
            var isNull = v.IsNull;
            switch (t.Kind)
            {
                case TypeKind.Char:
                    if (isNull) _xdr.WriteOpaqueFixed([], t.ByteLength, (byte)' ');
                    else if (v.Tag == VTag.Bytes) _xdr.WriteOpaqueFixed(v.AsBytes, t.ByteLength, 0);
                    else _xdr.WriteFixedString(v.AsString, t.ByteLength, _enc);
                    break;
                case TypeKind.VarChar:
                    if (isNull) _xdr.WriteInt32(0);
                    else if (v.Tag == VTag.Bytes) _xdr.WriteBuffer(v.AsBytes);
                    else _xdr.WriteString(v.AsString, _enc);
                    break;
                case TypeKind.SmallInt:
                case TypeKind.Integer:
                    _xdr.WriteInt32(isNull ? 0 : (int)v.L);
                    break;
                case TypeKind.BigInt:
                    _xdr.WriteInt64(isNull ? 0 : v.L);
                    break;
                case TypeKind.Float:
                    _xdr.WriteFloat(isNull ? 0 : (float)v.AsDouble);
                    break;
                case TypeKind.Double:
                    _xdr.WriteDouble(isNull ? 0 : v.AsDouble);
                    break;
                case TypeKind.Date:
                    _xdr.WriteInt32(isNull ? 0 : (int)v.L);
                    break;
                case TypeKind.Time:
                    _xdr.WriteInt32(isNull ? 0 : (int)v.L);
                    break;
                case TypeKind.Timestamp:
                {
                    if (isNull)
                    {
                        _xdr.WriteInt32(0);
                        _xdr.WriteInt32(0);
                    }
                    else
                    {
                        var (day, time) = Conv.SplitTimestamp(v.L);
                        _xdr.WriteInt32((int)day);
                        _xdr.WriteInt32((int)time);
                    }
                    break;
                }
                case TypeKind.Boolean:
                    _xdr.WriteOpaqueFixed([(byte)(isNull ? 0 : v.L != 0 ? 1 : 0)], 1, 0);
                    break;
                case TypeKind.Blob:
                {
                    long id = 0;
                    if (!isNull)
                    {
                        var data = v.Tag == VTag.Bytes ? v.AsBytes : _enc.GetBytes(Conv.ToStr(v));
                        id = _nextBlobId++;
                        _blobStore[id] = (data, tx);
                    }
                    _xdr.WriteInt64(id);
                    break;
                }
                case TypeKind.Null:
                    break;
                default:
                    throw Err.Unsupported("output type " + t);
            }
            _xdr.WriteInt32(isNull ? -1 : 0);
        }
    }

    // ======================================================================== blobs

    void OpCreateBlob(bool withBpb)
    {
        if (withBpb) _xdr.ReadBuffer();
        var txh = _xdr.ReadInt32();
        _xdr.ReadInt64();
        Guard(() =>
        {
            var tx = Tx(txh);
            var h = NewHandle();
            var id = _nextBlobId++;
            _blobs[h] = new BlobHandle { Id = h, BlobId = id, Writer = new MemoryStream(), Tx = tx };
            Response(h, id);
        });
    }

    void OpOpenBlob(bool withBpb)
    {
        if (withBpb) _xdr.ReadBuffer();
        var txh = _xdr.ReadInt32();
        var blobId = _xdr.ReadInt64();
        Guard(() =>
        {
            if (!_blobStore.TryGetValue(blobId, out var data)) throw new StatusBuilder().Gds(335544329).Build("HY000"); // bad_segstr_id
            var h = NewHandle();
            _blobs[h] = new BlobHandle { Id = h, BlobId = blobId, Data = data.Data };
            Response(h, blobId);
        });
    }

    BlobHandle Blob(int h) => _blobs.TryGetValue(h, out var b) ? b : throw new StatusBuilder().Gds(335544328).Build("HY000"); // bad_segstr_handle

    void OpGetSegment()
    {
        var h = _xdr.ReadInt32();
        var requested = _xdr.ReadInt32();
        _xdr.ReadInt32(); // data segment
        Guard(() =>
        {
            var b = Blob(h);
            var data = b.Data ?? throw Err.Random("blob is not open for reading");
            var buf = new MemoryStream();
            var room = Math.Max(requested, 64);
            while (b.Pos < data.Length && room > 2)
            {
                var n = Math.Min(Math.Min(room - 2, data.Length - b.Pos), 32765);
                buf.WriteByte((byte)(n & 0xFF));
                buf.WriteByte((byte)(n >> 8));
                buf.Write(data, b.Pos, n);
                b.Pos += n;
                room -= n + 2;
            }
            var state = b.Pos >= data.Length ? 2 : 0;
            Response(state, data: buf.ToArray());
        });
    }

    void OpPutSegment(bool batch)
    {
        var h = _xdr.ReadInt32();
        _xdr.ReadInt32(); // segment length
        var data = _xdr.ReadBuffer();
        Guard(() =>
        {
            var b = Blob(h);
            var w = b.Writer ?? throw Err.Random("blob is not open for writing");
            if (!batch) w.Write(data);
            else
            {
                var i = 0;
                while (i + 2 <= data.Length)
                {
                    var len = data[i] | (data[i + 1] << 8);
                    i += 2;
                    w.Write(data, i, Math.Min(len, data.Length - i));
                    i += len;
                }
            }
            Response();
        });
    }

    void OpCloseBlob(bool cancel)
    {
        var h = _xdr.ReadInt32();
        Guard(() =>
        {
            if (_blobs.Remove(h, out var b) && !cancel && b.Writer != null)
                _blobStore[b.BlobId] = (b.Writer.ToArray(), b.Tx);
            Response();
        });
    }

    void OpInfoBlob()
    {
        var h = _xdr.ReadInt32();
        _xdr.ReadInt32();
        var items = _xdr.ReadBuffer();
        _xdr.ReadInt32();
        Guard(() =>
        {
            var b = Blob(h);
            var len = b.Data?.Length ?? (int)(b.Writer?.Length ?? 0);
            var ib = new InfoBuffer();
            foreach (var item in items)
            {
                switch (item)
                {
                    case Isc.info_blob_total_length: ib.Int(item, len); break;
                    case Isc.info_blob_num_segments: ib.Int(item, len == 0 ? 0 : (len + 32764) / 32765); break;
                    case Isc.info_blob_max_segment: ib.Int(item, Math.Min(len, 32765)); break;
                    case Isc.info_blob_type: ib.Int(item, 0); break;
                    case Isc.info_end: break;
                    default: ib.Byte(Isc.info_error).Byte(2).Byte(0).Byte(item).Byte(0); break;
                }
            }
            ib.Byte(Isc.info_end);
            Response(data: ib.ToArray());
        });
    }

    void OpSeekBlob()
    {
        var h = _xdr.ReadInt32();
        var mode = _xdr.ReadInt32();
        var offset = _xdr.ReadInt32();
        Guard(() =>
        {
            var b = Blob(h);
            var len = b.Data?.Length ?? 0;
            b.Pos = Math.Clamp(mode switch { 0 => offset, 1 => b.Pos + offset, _ => len + offset }, 0, len);
            Response(b.Pos);
        });
    }
}
