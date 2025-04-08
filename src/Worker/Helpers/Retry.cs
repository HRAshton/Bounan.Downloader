using System.Diagnostics.CodeAnalysis;

namespace Bounan.Downloader.Worker.Helpers;

/// <summary>
/// https://stackoverflow.com/questions/1563191/cleanest-way-to-write-retry-logic
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Retry logic")]
public static partial class Retry
{
    public static Task DoAsync(
        Func<CancellationToken, Task> action,
        ILogger logger,
        int maxRetries = 3,
        int delayInMs = 1000,
        CancellationToken cancellationToken = default)
    {
        return DoAsync<object?>(
            async ct =>
            {
                await action(ct);
                return null;
            },
            logger,
            maxRetries,
            delayInMs,
            cancellationToken);
    }

    public static async Task<T> DoAsync<T>(
        Func<CancellationToken, Task<T>> action,
        ILogger logger,
        int maxRetries = 3,
        int delayInMs = 1000,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        var attempts = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await action(cancellationToken);
            }
            catch (Exception ex) when (attempts < maxRetries)
            {
                Log.RetryAttempt(logger, attempts, maxRetries, delayInMs, ex.Message, ex.StackTrace);
                attempts++;
                await Task.Delay(delayInMs, cancellationToken);
            }
        }
    }
}
