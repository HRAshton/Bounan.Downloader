namespace Bounan.Downloader.Hls2TlgrUploader.Interfaces;

internal interface IFfmpegFactory
{
    IFfmpegService CreateFfmpegService(CancellationToken cancellationToken);
}