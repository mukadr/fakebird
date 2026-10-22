using FirebirdSql.Data.FirebirdClient;

namespace Fakebird.Tests;

/// <summary>Databases whose default character set is not UTF8, used over UTF8 connections.</summary>
[Collection("server")]
public class CharsetTests(ServerFixture fx)
{
    static object? Scalar(FbConnection cn, string sql)
    {
        using var cmd = new FbCommand(sql, cn);
        return cmd.ExecuteScalar();
    }

    static FbConnection Open(FakeDatabase db)
    {
        var cn = new FbConnection(db.ConnectionString());
        cn.Open();
        return cn;
    }

    [Fact]
    public void CreateDatabaseWithCharset()
    {
        var db = fx.Server.CreateDatabase("cs_" + Guid.NewGuid().ToString("N"), charset: "win1252");
        Assert.Equal("WIN1252", db.DefaultCharset);
        db.RunScript("create table t (s varchar(5));");
        using var cn = Open(db);
        Assert.Equal("WIN1252", ((string)Scalar(cn, "select rdb$character_set_name from rdb$database")!).Trim());
        Assert.Equal((short)53, Scalar(cn, "select f.rdb$character_set_id from rdb$relation_fields r join rdb$fields f on f.rdb$field_name = r.rdb$field_source where r.rdb$relation_name = 'T'"));
        using (var cmd = new FbCommand("insert into t values (@s)", cn))
        {
            cmd.Parameters.AddWithValue("s", "ação€");
            cmd.ExecuteNonQuery();
            cmd.Parameters[0].Value = "ł";
            var e = Assert.Throws<FbException>(() => cmd.ExecuteNonQuery());
            Assert.Contains("Cannot transliterate character between character sets", e.Message);
        }
        Assert.Equal(5, Scalar(cn, "select octet_length(s) from t"));
        Assert.Equal("ação€", Scalar(cn, "select s from t"));
        fx.Server.DropDatabase(db.Name);
    }

    [Fact]
    public void SnapshotsKeepTheCharset()
    {
        var template = fx.Server.CreateDatabase("cs_" + Guid.NewGuid().ToString("N"));
        Assert.Equal("UTF8", template.DefaultCharset);
        template.RunScript("alter database set default character set win1252;");
        var snapshot = template.Snapshot();
        var copy = fx.Server.CreateDatabase("cs_" + Guid.NewGuid().ToString("N"), snapshot);
        Assert.Equal("WIN1252", copy.DefaultCharset);
        copy.DefaultCharset = "UTF8";
        fx.Server.Restore(copy.Name, snapshot);
        Assert.Equal("WIN1252", copy.DefaultCharset);
        Assert.Equal("UTF8", fx.Server.CreateDatabase("cs_" + Guid.NewGuid().ToString("N"), snapshot, "utf8").DefaultCharset);
    }

    [Fact]
    public void UnknownCharset()
    {
        var e = Assert.Throws<FbError>(() => fx.Server.CreateDatabase("cs_" + Guid.NewGuid().ToString("N"), charset: "nosuch"));
        Assert.Contains("CHARACTER SET NOSUCH is not defined", e.Message);
    }

    [Fact]
    public void ConnectionsMustUseUtf8()
    {
        var db = fx.Server.CreateDatabase("cs_" + Guid.NewGuid().ToString("N"), charset: "WIN1252");
        using var cn = new FbConnection(db.ConnectionString().Replace("Charset=UTF8", "Charset=ISO8859_1"));
        var e = Assert.Throws<FbException>(cn.Open);
        Assert.Contains("connection character set ISO8859_1", e.Message);
        fx.Server.DropDatabase(db.Name);
    }

    /// <summary>
    /// As Firebird 5 answers: text is stored in WIN1252 (a binary blob moved into a text blob keeps its bytes, read as
    /// WIN1252); a UTF8 attachment gets it transliterated, a NONE one gets its bytes.
    /// </summary>
    [Fact]
    public void NoneConnectionsGetTheBytes()
    {
        var db = fx.Server.CreateDatabase("cs_" + Guid.NewGuid().ToString("N"), charset: "WIN1252");
        db.RunScript("create table t (id int, v varchar(5), c char(3), b blob sub_type 1);");
        var utf8 = System.Text.Encoding.UTF8.GetBytes("ÓX");
        using (var cn = Open(db))
        {
            using (var cmd = new FbCommand("insert into t values (1, 'ÓX', 'ÓX', 'ÓX')", cn)) cmd.ExecuteNonQuery();
            using (var cmd = new FbCommand("execute block (p blob sub_type binary = @p) as begin insert into t (id, b) values (2, :p); end", cn))
            {
                cmd.Parameters.Add("p", FbDbType.Binary).Value = utf8;
                cmd.ExecuteNonQuery();
            }
            using (var cmd = new FbCommand("insert into t (id, b) values (3, @p)", cn))
            {
                cmd.Parameters.Add("p", FbDbType.Binary).Value = utf8;
                cmd.ExecuteNonQuery();
            }
        }
        string Read(string charset)
        {
            using var cn = new FbConnection(db.ConnectionString().Replace("Charset=UTF8", "Charset=" + charset));
            cn.Open();
            using var cmd = new FbCommand("select id, v, c, b, octet_length(b) from t order by id", cn);
            using var r = cmd.ExecuteReader();
            var rows = new List<string>();
            while (r.Read())
                rows.Add($"{r[0]}: [{r[1]}] [{r[2]}] {Convert.ToHexString(r.GetFieldValue<byte[]>(3))} {r[4]}");
            return string.Join("\n", rows);
        }
        Assert.Equal("1: [ÓX] [ÓX ] C39358 2\n2: [] [] C383E2809C58 3\n3: [] [] C39358 2", Read("UTF8"));
        // the client decodes the WIN1252 byte of Ó as UTF-8
        Assert.Equal("1: [\uFFFDX] [\uFFFDX ] D358 2\n2: [] [] C39358 3\n3: [] [] D358 2", Read("NONE"));
        fx.Server.DropDatabase(db.Name);
    }
}
