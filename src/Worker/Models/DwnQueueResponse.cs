using Bounan.Common.Models;
using Bounan.Downloader.Worker.Extensions;

namespace Bounan.Downloader.Worker.Models;

public record DwnQueueResponse(VideoKey? VideoKey) : IDwnQueueResponse<VideoKey>
{
	public string Hash => VideoKey is not null
		? $"{VideoKey.MyAnimeListId}{VideoKey.Dub}{VideoKey.Episode}".CalculateHash()
		: string.Empty;
}