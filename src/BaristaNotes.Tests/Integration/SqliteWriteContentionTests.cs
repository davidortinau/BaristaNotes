using System.Collections.Concurrent;
using System.Data.Common;
using BaristaNotes.Core.Data;
using BaristaNotes.Core.Data.CompiledModels;
using BaristaNotes.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BaristaNotes.Tests.Integration;

public sealed class SqliteWriteContentionTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(), $"BaristaNotes-contention-{Guid.NewGuid():N}.db");

    public async Task InitializeAsync()
    {
        await using var context = CreateContext(useCompiledModel: true);
        await context.Database.EnsureCreatedAsync();
        context.Beans.Add(new Bean { Id = 1, Name = "Contention fixture", SyncId = Guid.NewGuid() });
        context.Bags.Add(new Bag
        {
            Id = 1,
            BeanId = 1,
            RoastDate = DateTime.Today,
            Notes = "Original",
            SyncId = Guid.NewGuid()
        });
        await context.SaveChangesAsync();
        await context.Database.OpenConnectionAsync();
        await using var journal = context.Database.GetDbConnection().CreateCommand();
        journal.CommandText = "PRAGMA journal_mode=DELETE;";
        Assert.Equal("delete", await journal.ExecuteScalarAsync());
    }

    public Task DisposeAsync()
    {
        File.Delete(_path);
        File.Delete(_path + "-wal");
        File.Delete(_path + "-shm");
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData(false, "insert")]
    [InlineData(true, "insert")]
    [InlineData(false, "update")]
    [InlineData(true, "update")]
    [InlineData(false, "delete")]
    [InlineData(true, "delete")]
    public async Task SaveChanges_DoesNotReportSuccessBeforeReaderReleasesLock(
        bool useCompiledModel,
        string operation)
    {
        await using var blocker = new SqliteConnection(ConnectionString());
        await blocker.OpenAsync();
        await using var transaction = blocker.BeginTransaction(deferred: true);
        await using var read = blocker.CreateCommand();
        read.Transaction = transaction;
        read.CommandText = "SELECT Id FROM Beans;";
        await using var reader = await read.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        var statements = new CommandCapture();
        var write = Task.Run(async () =>
        {
            await using var context = CreateContext(useCompiledModel, statements);
            await context.Database.OpenConnectionAsync();
            var bag = operation == "insert"
                ? new Bag { BeanId = 1, RoastDate = DateTime.Today, SyncId = Guid.NewGuid() }
                : await context.Bags.SingleAsync(item => item.Id == 1);
            if (operation == "insert")
                context.Bags.Add(bag);
            else if (operation == "delete")
                context.Bags.Remove(bag);
            else
                bag.Notes = "Updated";

            await context.SaveChangesAsync();
            return bag.Id;
        });

        bool completedWhileLocked;
        try
        {
            await statements.WriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            // Keep the lock past the provider's 150 ms reset-retry interval.
            completedWhileLocked = await Task.WhenAny(write, Task.Delay(350)) == write;
        }
        finally
        {
            await reader.DisposeAsync();
            await transaction.CommitAsync();
        }

        var id = await write.WaitAsync(TimeSpan.FromSeconds(10));
        await using var verification = CreateContext(useCompiledModel);
        var stored = await verification.Bags.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id);
        if (operation == "delete")
        {
            Assert.Null(stored);
        }
        else
        {
            Assert.NotNull(stored);
            Assert.Equal(1, stored.BeanId);
            if (operation == "update")
                Assert.Equal("Updated", stored.Notes);
        }

        Assert.False(completedWhileLocked, "A write reported success while another connection still held the read lock.");
        Assert.DoesNotContain(statements.Sql, sql => sql.Contains("RETURNING", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryTable_UsesTheSameSafeWriteStrategy(bool useCompiledModel)
    {
        using var context = CreateContext(useCompiledModel);
        Assert.All(context.Model.GetEntityTypes(), entity => Assert.False(entity.IsSqlReturningClauseUsed()));
    }

    private string ConnectionString() => new SqliteConnectionStringBuilder
    {
        DataSource = _path,
        Pooling = false,
        DefaultTimeout = 5
    }.ToString();

    private BaristaNotesContext CreateContext(bool useCompiledModel, CommandCapture? capture = null)
    {
        var options = new DbContextOptionsBuilder<BaristaNotesContext>().UseSqlite(ConnectionString());
        if (useCompiledModel)
            options.UseModel(BaristaNotesContextModel.Instance);
        else
        {
            // EF auto-discovers the compiled model, so explicitly request OnModelCreating here.
            using var designContext = new BaristaNotesContext(options.Options);
            options.UseModel(designContext.GetService<IDesignTimeModel>().Model);
        }
        if (capture is not null)
            options.AddInterceptors(capture);
        return new BaristaNotesContext(options.Options);
    }

    private sealed class CommandCapture : DbCommandInterceptor
    {
        public ConcurrentQueue<string> Sql { get; } = new();
        public TaskCompletionSource WriteStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Sql.Enqueue(command.CommandText);
            if (command.CommandText.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase)
                || command.CommandText.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)
                || command.CommandText.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase))
            {
                WriteStarted.TrySetResult();
            }
            return ValueTask.FromResult(result);
        }
    }
}
