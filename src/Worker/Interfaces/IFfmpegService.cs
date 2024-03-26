using System.IO.Pipelines;
using Bounan.Downloader.Worker.Models;

namespace Bounan.Downloader.Worker.Interfaces;

public interface IFfmpegService : IDisposable
{
	Task<VideoInfo> GetResultAsync(CancellationToken cancellationToken);

	PipeWriter PipeWriter { get; }
}