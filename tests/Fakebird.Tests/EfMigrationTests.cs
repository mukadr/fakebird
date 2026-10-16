using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Fakebird.Tests;

public class Note
{
    public int Id { get; set; }
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class MigContext(string cs) : DbContext
{
    public DbSet<Note> Notes => Set<Note>();
    protected override void OnConfiguring(DbContextOptionsBuilder b) => b.UseFirebird(cs);
}

[DbContext(typeof(MigContext))]
[Migration("20240101000000_Init")]
public class InitMigration : Migration
{
    protected override void Up(MigrationBuilder mb)
    {
        mb.CreateTable(
            name: "Notes",
            columns: t => new
            {
                Id = t.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Fb:ValueGenerationStrategy", FirebirdSql.EntityFrameworkCore.Firebird.Metadata.FbValueGenerationStrategy.IdentityColumn),
                Text = t.Column<string>(type: "VARCHAR(200)", maxLength: 200, nullable: false),
                CreatedAt = t.Column<DateTime>(type: "TIMESTAMP", nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_Notes", x => x.Id));
        mb.CreateIndex("IX_Notes_Text", "Notes", "Text");
    }

    protected override void Down(MigrationBuilder mb) => mb.DropTable("Notes");
}

[DbContext(typeof(MigContext))]
[Migration("20240102000000_AddColumn")]
public class AddColumnMigration : Migration
{
    protected override void Up(MigrationBuilder mb)
    {
        mb.AddColumn<int>("Priority", "Notes", type: "INTEGER", nullable: false, defaultValue: 0);
        mb.Sql("UPDATE \"Notes\" SET \"Priority\" = 1");
    }

    protected override void Down(MigrationBuilder mb) => mb.DropColumn("Priority", "Notes");
}

[Collection("server")]
public class EfMigrationTests(ServerFixture fx)
{
    [Fact]
    public void MigrateCreatesSchemaAndHistory()
    {
        var cs = fx.Server.CreateDatabase("mig_" + Guid.NewGuid().ToString("N")).ConnectionString();
        using (var ctx = new MigContext(cs))
        {
            ctx.Database.Migrate();
            Assert.Equal(["20240101000000_Init", "20240102000000_AddColumn"], ctx.Database.GetAppliedMigrations().ToArray());
            ctx.Notes.Add(new Note { Text = "hello", CreatedAt = DateTime.Now });
            ctx.SaveChanges();
            Assert.Equal(1, ctx.Notes.Count());
        }
        using (var ctx = new MigContext(cs))
        {
            ctx.Database.Migrate(); // idempotent
            Assert.Empty(ctx.Database.GetPendingMigrations());
        }
    }
}
