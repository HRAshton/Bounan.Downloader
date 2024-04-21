using Bounan.Common.Models;

namespace Bounan.Downloader.Worker.Models;

public record DwnResultNotification(int MyAnimeListId, string Dub, int Episode, string? MessageId)
    : IDwnResultNotification;