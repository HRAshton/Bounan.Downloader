using System.Globalization;
using Bounan.Common;

namespace Bounan.Downloader.Worker.Extensions;

public static class VideoKeyExtensions
{
    public static string CalculateHash(this IVideoKey? videoKey)
    {
        return videoKey is null
            ? string.Empty
            : CalculateHash($"{videoKey.MyAnimeListId}{videoKey.Dub}{videoKey.Episode}");
    }

    private static string CalculateHash(string str)
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