using Microsoft.Windows.AI;
using Microsoft.Windows.AI.MachineLearning;
using Microsoft.Windows.AI.Video;
using System.Management;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Editing;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using WindowsAiVsrCpuDemo.Infrastructure;

namespace WindowsAiVsrCpuDemo.Services;

public sealed record VsrAvailability(bool IsReady, string Message);

public sealed record VideoJobInfo(
    uint InputWidth,
    uint InputHeight,
    uint OutputWidth,
    uint OutputHeight,
    double FramesPerSecond,
    uint FrameRateNumerator,
    uint FrameRateDenominator,
    TimeSpan Duration,
    double ScaleFactor,
    double RequestedScaleFactor);

public sealed class VideoProcessingService
{
    private const int RecommendedCores = 4;
    private const int RecommendedClockMhz = 3000;
    private const int RecommendedL3CacheKb = 32 * 1024;

    private static string L(string key, params object?[] arguments) =>
        AppLocalizer.Get(key, arguments);

    public async Task<VsrAvailability> CheckAvailabilityAsync()
    {
        var catalog = ExecutionProviderCatalog.GetDefault();
        await catalog.EnsureAndRegisterCertifiedAsync();

        var readyState = VideoScaler.GetReadyState();
        if (readyState == AIFeatureReadyState.NotReady)
        {
            var readyResult = await VideoScaler.EnsureReadyAsync();
            if (readyResult.Status != AIFeatureReadyResultState.Success)
            {
                return new VsrAvailability(
                    false,
                    L("ScalerPreparationFailed", readyResult.Status));
            }
        }
        else if (readyState != AIFeatureReadyState.Ready)
        {
            return new VsrAvailability(
                false,
                L("ScalerUnavailable", readyState));
        }

        using var scaler = await VideoScaler.CreateAsync();
        var cpu = await Task.Run(GetCpuAssessment);
        return new VsrAvailability(
            true,
            L("ScalerAvailable", cpu));
    }

    public async Task<VideoJobInfo> InspectVideoAsync(
        StorageFile file,
        double requestedScaleFactor = 2D)
    {
        var clip = await MediaClip.CreateFromFileAsync(file);
        var properties = clip.GetVideoEncodingProperties();

        if (properties.Width == 0 || properties.Height == 0)
        {
            throw new InvalidOperationException(L("ResolutionUnavailable"));
        }

        var shortEdge = Math.Min(properties.Width, properties.Height);
        var longEdge = Math.Max(properties.Width, properties.Height);
        if (shortEdge < 240 || longEdge > 2560)
        {
            throw new NotSupportedException(
                L("ResolutionUnsupported", properties.Width, properties.Height));
        }

        var denominator = properties.FrameRate.Denominator == 0
            ? 1U
            : properties.FrameRate.Denominator;
        var numerator = properties.FrameRate.Numerator == 0
            ? 30U
            : properties.FrameRate.Numerator;
        var framesPerSecond = (double)numerator / denominator;

        if (framesPerSecond is < 15 or > 60)
        {
            throw new NotSupportedException(
                L("FrameRateUnsupported", framesPerSecond));
        }

        var (outputWidth, outputHeight, scaleFactor, normalizedScaleFactor) =
            CalculateOutputSize(
                properties.Width,
                properties.Height,
                requestedScaleFactor);

        return new VideoJobInfo(
            properties.Width,
            properties.Height,
            outputWidth,
            outputHeight,
            framesPerSecond,
            numerator,
            denominator,
            clip.OriginalDuration,
            scaleFactor,
            normalizedScaleFactor);
    }

    public VideoJobInfo ChangeScaleFactor(
        VideoJobInfo job,
        double requestedScaleFactor)
    {
        var (outputWidth, outputHeight, scaleFactor, normalizedScaleFactor) =
            CalculateOutputSize(
                job.InputWidth,
                job.InputHeight,
                requestedScaleFactor);
        return job with
        {
            OutputWidth = outputWidth,
            OutputHeight = outputHeight,
            ScaleFactor = scaleFactor,
            RequestedScaleFactor = normalizedScaleFactor
        };
    }

    public async Task RenderAsync(
        StorageFile inputFile,
        StorageFile outputFile,
        VideoJobInfo job,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        await EnsureVsrReadyAsync();
        cancellationToken.ThrowIfCancellationRequested();

        var clip = await MediaClip.CreateFromFileAsync(inputFile);
        var properties = new PropertySet
        {
            ["DiagnosticContext"] = "MP4 export",
            ["InputWidth"] = job.InputWidth,
            ["InputHeight"] = job.InputHeight,
            ["OutputWidth"] = job.OutputWidth,
            ["OutputHeight"] = job.OutputHeight
        };
        var effectDefinition = new VideoEffectDefinition(
            typeof(VsrVideoEffect.VideoSuperResolutionEffect).FullName!,
            properties);
        clip.VideoEffectDefinitions.Add(effectDefinition);

        var composition = new MediaComposition();
        composition.Clips.Add(clip);

        var quality = job.OutputHeight > 1080 || job.OutputWidth > 1920
            ? VideoEncodingQuality.Uhd2160p
            : VideoEncodingQuality.HD1080p;
        var profile = MediaEncodingProfile.CreateMp4(quality);
        profile.Video.Width = job.OutputWidth;
        profile.Video.Height = job.OutputHeight;
        profile.Video.FrameRate.Numerator = job.FrameRateNumerator;
        profile.Video.FrameRate.Denominator = job.FrameRateDenominator;
        profile.Video.PixelAspectRatio.Numerator = 1;
        profile.Video.PixelAspectRatio.Denominator = 1;
        profile.Video.Bitrate = CalculateBitrate(job.OutputWidth, job.OutputHeight);

        try
        {
            var renderOperation = composition.RenderToFileAsync(
                outputFile,
                MediaTrimmingPreference.Precise,
                profile);
            var failureReason = await renderOperation.AsTask(cancellationToken, progress);

            if (failureReason != TranscodeFailureReason.None)
            {
                throw new InvalidOperationException(
                    L("CompositionExportFailed", failureReason));
            }
        }
        catch
        {
            try
            {
                await outputFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }
            catch
            {
                // The transcoder can retain the file briefly while cancellation unwinds.
            }

            throw;
        }
    }

    private static async Task EnsureVsrReadyAsync()
    {
        var catalog = ExecutionProviderCatalog.GetDefault();
        await catalog.EnsureAndRegisterCertifiedAsync();

        var readyState = VideoScaler.GetReadyState();
        if (readyState == AIFeatureReadyState.NotReady)
        {
            var result = await VideoScaler.EnsureReadyAsync();
            if (result.Status != AIFeatureReadyResultState.Success)
            {
                throw new InvalidOperationException(
                    L("ScalerPreparationFailed", result.Status));
            }
        }
        else if (readyState != AIFeatureReadyState.Ready)
        {
            throw new NotSupportedException(
                L("ScalerUnavailable", readyState));
        }

        using var scaler = await VideoScaler.CreateAsync();
    }

    private static (uint Width, uint Height, double ScaleFactor, double RequestedScaleFactor) CalculateOutputSize(
        uint inputWidth,
        uint inputHeight,
        double requestedScaleFactor)
    {
        if (requestedScaleFactor is < 1.25D or > 4D)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedScaleFactor),
                L("ScaleOutOfRange"));
        }

        var isPortrait = inputHeight > inputWidth;
        var maximumWidth = isPortrait ? 2160D : 3840D;
        var maximumHeight = isPortrait ? 3840D : 2160D;
        var minimumOutputScale = 480D / Math.Min(inputWidth, inputHeight);
        var normalizedScaleFactor = Math.Min(
            4D,
            Math.Ceiling(Math.Max(requestedScaleFactor, minimumOutputScale) * 4D) / 4D);
        var scale = Math.Min(
            normalizedScaleFactor,
            Math.Min(maximumWidth / inputWidth, maximumHeight / inputHeight));

        var width = MakeEven((uint)Math.Floor(inputWidth * scale));
        var height = MakeEven((uint)Math.Floor(inputHeight * scale));
        return (
            width,
            height,
            Math.Min((double)width / inputWidth, (double)height / inputHeight),
            normalizedScaleFactor);
    }

    private static uint MakeEven(uint value) => Math.Max(2U, value & ~1U);

    private static uint CalculateBitrate(uint width, uint height)
    {
        var pixels = (ulong)width * height;
        if (pixels >= 3840UL * 2160UL)
        {
            return 35_000_000;
        }

        if (pixels >= 1920UL * 1080UL)
        {
            return 16_000_000;
        }

        return 8_000_000;
    }

    private static string GetCpuAssessment()
    {
        try
        {
            var totalCores = 0;
            var maximumClockMhz = 0;
            var maximumL3CacheKb = 0;

            using var searcher = new ManagementObjectSearcher(
                "SELECT NumberOfCores, MaxClockSpeed, L3CacheSize FROM Win32_Processor");
            foreach (ManagementObject processor in searcher.Get())
            {
                totalCores += Convert.ToInt32(processor["NumberOfCores"] ?? 0);
                maximumClockMhz = Math.Max(
                    maximumClockMhz,
                    Convert.ToInt32(processor["MaxClockSpeed"] ?? 0));
                maximumL3CacheKb = Math.Max(
                    maximumL3CacheKb,
                    Convert.ToInt32(processor["L3CacheSize"] ?? 0));
            }

            var meetsRecommendation =
                totalCores >= RecommendedCores &&
                maximumClockMhz >= RecommendedClockMhz &&
                maximumL3CacheKb >= RecommendedL3CacheKb;
            return meetsRecommendation
                ? L(
                    "CpuRecommended",
                    totalCores,
                    maximumClockMhz,
                    maximumL3CacheKb / 1024)
                : L(
                    "CpuBelowRecommended",
                    totalCores,
                    maximumClockMhz,
                    maximumL3CacheKb / 1024);
        }
        catch (Exception ex)
        {
            return L("CpuCheckSkipped", ex.Message);
        }
    }
}
