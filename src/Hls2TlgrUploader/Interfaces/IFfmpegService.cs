using System.IO.Pipelines;
using Bounan.Downloader.Hls2TlgrUploader.Models;

namespace Bounan.Downloader.Hls2TlgrUploader.Interfaces;

internal interface IFfmpegService : IDisposable
{
    Task<VideoInfo> GetResultAsync(CancellationToken cancellationToken);

    PipeWriter PipeWriter { get; }
}