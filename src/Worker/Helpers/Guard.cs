using System.Diagnostics.CodeAnalysis;

namespace Bounan.Downloader.Worker.Helpers;

internal static class Guard
{
    public static void Ensure([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
        {
            throw new ArgumentException(message);
        }
    }

    public static void NotNull<T>([NotNull] T? value, string message)
    {
        if (value is null)
        {
            throw new ArgumentNullException(message);
        }
    }

    public static void NotNullOrEmpty([NotNull] string? value, string message)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException($"Argument {message} cannot be null or empty.", message);
        }
    }
}
