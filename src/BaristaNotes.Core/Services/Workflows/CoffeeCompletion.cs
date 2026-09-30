using BaristaNotes.Core.Services.DTOs;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.Core.Services.Workflows;

public enum CoffeeCreationPath
{
    ExistingBean,
    TypeForm
}

public sealed class CoffeeCompletion
{
    private int _started;

    public bool Started => Volatile.Read(ref _started) != 0;

    public Task CompleteSavedBagAsync(
        BagSummaryDto bag,
        CoffeeCreationPath path,
        Action haptic,
        Func<Task> exit,
        Func<bool> ownerAlive,
        Action<BagSummaryDto>? created,
        Action<string, bool> feedback,
        Action release,
        ILogger logger)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
            return Task.CompletedTask;

        return RunAsync(haptic, exit, ownerAlive, () => created?.Invoke(bag), release, exception =>
        {
            if (exception is OperationCanceledException)
            {
                logger.LogDebug("Saved coffee completion canceled for bag {BagId}", bag.Id);
                return;
            }

            logger.LogError(exception,
                "Completing saved coffee bag {BagId} from {CreationPath} failed", bag.Id, path);
            if (ownerAlive())
                feedback(path == CoffeeCreationPath.ExistingBean ? "Couldn't create bag" : exception.Message, true);
        });
    }

    public static async Task RunAsync(
        Action haptic,
        Func<Task> exit,
        Func<bool> ownerAlive,
        Action created,
        Action release,
        Action<Exception>? failed = null)
    {
        try
        {
            haptic();
            await exit();
            if (ownerAlive()) created();
        }
        catch (Exception exception) when (failed is not null)
        {
            failed(exception);
        }
        finally
        {
            release();
        }
    }
}
