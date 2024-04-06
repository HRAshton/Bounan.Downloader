using System.Globalization;

namespace Bounan.Downloader.Worker.Extensions;

public static class StringExtensions
{
    public static string CalculateHash(this string str)
    {
        ArgumentNullException.ThrowIfNull(str, nameof(str));

        var hashedValue = 3074457345618258791ul;
        foreach (var t in str)
        {
            hashedValue += t;
            hashedValue *= 3074457345618258799ul;
        }

        return hashedValue.ToString("X", CultureInfo.InvariantCulture);
    }
}