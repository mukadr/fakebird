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
}
