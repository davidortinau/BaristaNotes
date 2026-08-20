using BaristaNotes.Core.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BaristaNotes.Tests.TestInfrastructure;

internal static class SqliteTestContextFactory
{
    public static BaristaNotesContext Create()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys = OFF";
            command.ExecuteNonQuery();
        }

        var options = new DbContextOptionsBuilder<BaristaNotesContext>()
            .UseSqlite(connection, contextOwnsConnection: true)
            .Options;

        var context = new BaristaNotesContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
