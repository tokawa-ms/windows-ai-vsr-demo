using Microsoft.Windows.AI;
using Microsoft.Windows.AI.MachineLearning;
using Microsoft.Windows.AI.Video;
using System.Diagnostics;
using Windows.Foundation.Collections;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;

namespace VsrVideoEffect;

public sealed class VideoSuperResolutionEffect : IBasicVideoEffect
{
    private static int _nextInstanceId;
    private readonly object _syncRoot = new();
    private readonly int _instanceId = Interlocked.Increment(ref _nextInstanceId);
    private VideoScaler? _videoScaler;
    private long _processedFrameCount;
    private string _diagnosticContext = "unspecified";

    public VideoSuperResolutionEffect()
    {
        InitializeVideoScaler();
    }

    public bool IsReadOnly => false;

    public bool TimeIndependent => true;

    public MediaMemoryTypes SupportedMemoryTypes => MediaMemoryTypes.Gpu;

    public IReadOnlyList<VideoEncodingProperties> SupportedEncodingProperties =>
    new VideoEncodingProperties[]
    {
        new VideoEncodingProperties
        {
            Subtype = MediaEncodingSubtypes.Nv12
        }
    };

    public void SetEncodingProperties(
        VideoEncodingProperties encodingProperties,
        Windows.Graphics.DirectX.Direct3D11.IDirect3DDevice device)
    {
    }

    public void SetProperties(IPropertySet configuration)
    {
        if (configuration.TryGetValue("DiagnosticContext", out var value) &&
            value is string diagnosticContext &&
            !string.IsNullOrWhiteSpace(diagnosticContext))
        {
            _diagnosticContext = diagnosticContext;
        }

        Debug.WriteLine(
            $"[VSR] Instance {_instanceId} configured for {_diagnosticContext}.");
    }

    public void ProcessFrame(ProcessVideoFrameContext context)
    {
        var inputSurface = context.InputFrame.Direct3DSurface
            ?? throw new InvalidOperationException("VSR input surface is unavailable.");
        var outputSurface = context.OutputFrame.Direct3DSurface
            ?? throw new InvalidOperationException("VSR output surface is unavailable.");

        lock (_syncRoot)
        {
            var scaler = _videoScaler
                ?? throw new ObjectDisposedException(nameof(VideoSuperResolutionEffect));
            var result = scaler.Scale(
                inputSurface,
                outputSurface,
                new VideoScalerOptions());

            if (result.Status != VideoScalerStatus.Success)
            {
                Debug.WriteLine(
                    $"[VSR] Instance {_instanceId} failed in {_diagnosticContext}: " +
                    $"{inputSurface.Description.Width}x{inputSurface.Description.Height} -> " +
                    $"{outputSurface.Description.Width}x{outputSurface.Description.Height}, " +
                    $"status {result.Status}.");
                throw new InvalidOperationException(
                    $"VideoScaler failed to process a frame: {result.Status}");
            }

            var processedFrameCount = Interlocked.Increment(ref _processedFrameCount);
            if (processedFrameCount == 1 || processedFrameCount % 300 == 0)
            {
                Debug.WriteLine(
                    $"[VSR] Instance {_instanceId} processed frame {processedFrameCount} " +
                    $"for {_diagnosticContext}: " +
                    $"{inputSurface.Description.Width}x{inputSurface.Description.Height} -> " +
                    $"{outputSurface.Description.Width}x{outputSurface.Description.Height}, " +
                    $"status {result.Status}.");
            }
        }
    }

    public void DiscardQueuedFrames()
    {
    }

    public void Close(MediaEffectClosedReason reason)
    {
        lock (_syncRoot)
        {
            Debug.WriteLine(
                $"[VSR] Instance {_instanceId} closed for {_diagnosticContext}: " +
                $"{Interlocked.Read(ref _processedFrameCount)} frames, reason {reason}.");
            _videoScaler?.Dispose();
            _videoScaler = null;
        }
    }

    private void InitializeVideoScaler()
    {
        var catalog = ExecutionProviderCatalog.GetDefault();
        catalog
            .EnsureAndRegisterCertifiedAsync()
            .AsTask()
            .GetAwaiter()
            .GetResult();

        var readyState = VideoScaler.GetReadyState();
        if (readyState == AIFeatureReadyState.NotReady)
        {
            var readyResult = VideoScaler
                .EnsureReadyAsync()
                .AsTask()
                .GetAwaiter()
                .GetResult();

            if (readyResult.Status != AIFeatureReadyResultState.Success)
            {
                throw new InvalidOperationException(
                    $"VideoScaler could not become ready: {readyResult.Status}");
            }
        }
        else if (readyState != AIFeatureReadyState.Ready)
        {
            throw new NotSupportedException(
                $"VideoScaler is not supported on this system: {readyState}");
        }

        _videoScaler = VideoScaler
            .CreateAsync()
            .AsTask()
            .GetAwaiter()
            .GetResult();
    }
}
