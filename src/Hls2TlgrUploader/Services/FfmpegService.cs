using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Pipelines;
using System.Text;
using System.Text.RegularExpressions;
using Bounan.Downloader.Hls2TlgrUploader.Configuration;
using Bounan.Downloader.Hls2TlgrUploader.Extensions;
using Bounan.Downloader.Hls2TlgrUploader.Interfaces;
using Bounan.Downloader.Hls2TlgrUploader.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bounan.Downloader.Hls2TlgrUploader.Services;

[SuppressMessage("ReSharper", "ClassWithVirtualMembersNeverInherited.Global", Justification = "Disposable class")]
[SuppressMessage("Performance", "CA1852:Make the partial class sealed", Justification = "Disposable class")]
internal partial class FfmpegService : IFfmpegService
{
    private readonly Regex _resolutionRegex = ResolutionRegex();
    private readonly Regex _durationRegex = DurationRegex();

    private bool _isDisposed;
    private readonly ProcessingConfig _processingConfig;
    private readonly Pipe _pipe = new ();
    private readonly Stream _pipeReaderStream;
    private readonly StringBuilder _ffmpegStderrOutputBuilder = new ();
    private string? _filePath;
    private Process? _ffmpegProcess;
    private Task? _stdinStreamTask;
    private Task? _errorsTask;

    public FfmpegService(
        ILogger<FfmpegService> logger,
        IOptions<ProcessingConfig> videoServiceConfig)
    {
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));
        ArgumentNullException.ThrowIfNull(videoServiceConfig, nameof(videoServiceConfig));

        _processingConfig = videoServiceConfig.Value;
        Logger = logger;

        _pipeReaderStream = _pipe.Reader.AsStream();
    }

    private ILogger<FfmpegService> Logger { get; }

    public PipeWriter PipeWriter => _pipe.Writer;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed) return;

        if (disposing)
        {
            Log.DisposingFfmpegService(Logger);

            _pipe.Writer.Complete();
            _stdinStreamTask?.Wait();
            _errorsTask?.Wait();
            _ffmpegProcess?.Dispose();
            _pipeReaderStream.Dispose();
            if (_filePath is not null && File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }

            Log.FfmpegServiceDisposed(Logger);
        }

        _isDisposed = true;
    }

    public void Run(string fileId, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_processingConfig.TempVideoFilePattern)!);
        _filePath = string.Format(CultureInfo.InvariantCulture, _processingConfig.TempVideoFilePattern, fileId);
        RunInternal(cancellationToken);
    }

    public async Task<VideoInfo> GetResultAsync(CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(_filePath, nameof(_filePath));
        ArgumentNullException.ThrowIfNull(_ffmpegProcess, nameof(_ffmpegProcess));
        ArgumentNullException.ThrowIfNull(_stdinStreamTask, nameof(_stdinStreamTask));

        await _stdinStreamTask;
        await _ffmpegProcess!.WaitForExitAsync(cancellationToken);

        var ffmpegStderrOutput = _ffmpegStderrOutputBuilder.ToString();
        var resolution = _resolutionRegex.Matches(ffmpegStderrOutput).Last().Groups;
        var durationParts = _durationRegex.Matches(ffmpegStderrOutput).Last().Groups;
        var duration = int.Parse(durationParts[1].Value, CultureInfo.InvariantCulture) * 3600
                       + int.Parse(durationParts[2].Value, CultureInfo.InvariantCulture) * 60
                       + int.Parse(durationParts[3].Value, CultureInfo.InvariantCulture)
                       + (int)Math.Ceiling(int.Parse(durationParts[4].Value, CultureInfo.InvariantCulture) / 100.0);

        return new VideoInfo(
            _filePath,
            int.Parse(resolution[1].Value, CultureInfo.InvariantCulture),
            int.Parse(resolution[2].Value, CultureInfo.InvariantCulture),
            duration);
    }

    private void RunInternal(CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(_filePath, nameof(_filePath));

        Log.StartingFfmpegInstance(Logger, _filePath);

        _ffmpegProcess = RunFfmpeg(_filePath);
        var killRegistration = cancellationToken.Register(() => _ffmpegProcess.Kill());
        _ffmpegProcess.Exited += (_, _) =>
        {
            Log.FfmpegProcessExited(Logger, _ffmpegProcess.ExitCode);
            killRegistration.Unregister();
        };

        _errorsTask = Task.Run(
            () =>
            {
                while (!_ffmpegProcess.StandardError.EndOfStream)
                {
                    var line = _ffmpegProcess.StandardError.ReadLine();
                    _ffmpegStderrOutputBuilder.AppendLine(line);
                    Log.FfmpegStderrOutput(Logger, line);
                }
            },
            cancellationToken);

        _stdinStreamTask = _pipe.Reader.PumpToAsync(_ffmpegProcess.StandardInput.BaseStream, cancellationToken);
    }

    private static Process RunFfmpeg(string filePath)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = "-i pipe:0 -f mp4 -c copy " + filePath,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = Process.Start(processStartInfo)
                      ?? throw new InvalidOperationException("Failed to start ffmpeg process");
        return process;
    }

    [GeneratedRegex(@"Video: .*? (\d{3,4})x(\d{3,4})", RegexOptions.Compiled)]
    private static partial Regex ResolutionRegex();

    [GeneratedRegex(@"time=(\d{2}):(\d{2}):(\d{2}).(\d{2})", RegexOptions.Compiled)]
    private static partial Regex DurationRegex();
}