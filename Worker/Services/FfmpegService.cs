using System.Diagnostics;
using System.IO.Pipelines;
using System.Text;
using System.Text.RegularExpressions;
using Bounan.Downloader.Worker.Configuration;
using Bounan.Downloader.Worker.Extensions;
using Bounan.Downloader.Worker.Interfaces;
using Bounan.Downloader.Worker.Models;
using Microsoft.Extensions.Options;

namespace Bounan.Downloader.Worker.Services;

public partial class FfmpegService : IFfmpegService
{
	private readonly Regex _resolutionRegex = ResolutionRegex();
	private readonly Regex _durationRegex = DurationRegex();

	private readonly VideoServiceConfig _videoServiceConfig;
	private string _filePath;
	private readonly Pipe _pipe = new ();
	private readonly Stream _pipeReaderStream;
	private readonly StringBuilder _ffmpegStderrOutputBuilder = new ();
	private Process? _ffmpegProcess;
	private Task _stdinStreamTask;
	private Task _errorsTask;

	public FfmpegService(
		ILogger<FfmpegService> logger,
		IOptions<VideoServiceConfig> videoServiceConfig)
	{
		_videoServiceConfig = videoServiceConfig.Value;
		Logger = logger;

		_pipeReaderStream = _pipe.Reader.AsStream();
	}

	private ILogger<FfmpegService> Logger { get; }

	public PipeWriter PipeWriter => _pipe.Writer;

	public void Dispose()
	{
		Logger.LogDebug("Disposing ffmpeg service");
		_pipe.Writer.Complete();
		_stdinStreamTask.Wait();
		_errorsTask.Wait();
		_ffmpegProcess?.Dispose();
		_pipeReaderStream.Dispose();
		File.Delete(_filePath);
		Logger.LogDebug("Ffmpeg service disposed");

		GC.SuppressFinalize(this);
	}

	public void Run(string fileId, CancellationToken cancellationToken)
	{
		_filePath = string.Format(_videoServiceConfig.TempVideoFilePattern, fileId);
		RunInternal(cancellationToken);
	}

	public async Task<VideoInfo> GetResultAsync(CancellationToken cancellationToken)
	{
		if (_filePath is null)
		{
			throw new InvalidOperationException("File path is not set");
		}

		await _stdinStreamTask;
		await _ffmpegProcess!.WaitForExitAsync(cancellationToken);

		var ffmpegStderrOutput = _ffmpegStderrOutputBuilder.ToString();
		var resolution = _resolutionRegex.Matches(ffmpegStderrOutput).Last().Groups;
		var durationParts = _durationRegex.Matches(ffmpegStderrOutput).Last().Groups;
		var duration = int.Parse(durationParts[1].Value) * 3600
		               + int.Parse(durationParts[2].Value) * 60
		               + int.Parse(durationParts[3].Value)
		               + (int)Math.Ceiling(int.Parse(durationParts[4].Value) / 100.0);

		return new VideoInfo(
			_filePath,
			int.Parse(resolution[1].Value),
			int.Parse(resolution[2].Value),
			duration);
	}

	private void RunInternal(CancellationToken cancellationToken)
	{
		Logger.LogDebug("Starting ffmpeg instance");

		_ffmpegProcess = RunFfmpeg(_filePath);
		var killRegistration = cancellationToken.Register(() => _ffmpegProcess.Kill());
		_ffmpegProcess.Exited += (_, _) =>
		{
			Logger.LogDebug("Ffmpeg process exited with code {ExitCode}", _ffmpegProcess.ExitCode);
			killRegistration.Unregister();
		};

		_errorsTask = Task.Run(
			() =>
			{
				while (!_ffmpegProcess.StandardError.EndOfStream)
				{
					var line = _ffmpegProcess.StandardError.ReadLine();
					_ffmpegStderrOutputBuilder.AppendLine(line);
					Logger.LogTrace("{Line}", line);
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
			CreateNoWindow = true,
		};

		var process = Process.Start(processStartInfo);
		if (process == null)
		{
			throw new Exception("Failed to start ffmpeg process");
		}

		return process;
	}

	[GeneratedRegex(@"Video: .*? (\d{3,4})x(\d{3,4})", RegexOptions.Compiled)]
	private static partial Regex ResolutionRegex();

	[GeneratedRegex(@"time=(\d{2}):(\d{2}):(\d{2}).(\d{2})", RegexOptions.Compiled)]
	private static partial Regex DurationRegex();
}