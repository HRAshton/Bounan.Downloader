using Bounan.Common.Models;
using Bounan.Downloader.Worker.Models;

namespace Bounan.Downloader.Worker.Interfaces;

public interface IAniManClient
{
	Task<DwnQueueResponse?> GetNextVideo(CancellationToken cancellationToken);

	Task SendResult(IDwnResultNotification result, CancellationToken cancellationToken);
}