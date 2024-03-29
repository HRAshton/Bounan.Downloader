using Bounan.Common.Models;

namespace Bounan.Downloader.Worker.Models;

public record VideoMetadata(IVideoKey VideoKey, string SignedLink);