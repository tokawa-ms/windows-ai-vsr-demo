using Microsoft.Windows.AI.Video;
using System.Diagnostics;
using Windows.Graphics.DirectX;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Playback;

namespace WindowsAiVsrCpuDemo.Services;

public sealed class RealtimeVsrFrameProcessor : IDisposable
{
    private readonly SemaphoreSlim _processingGate = new(1, 1);
    private readonly VideoScaler _videoScaler;
    private readonly VideoFrame _inputFrame;
    private readonly VideoFrame _originalOutputFrame;
    private readonly VideoFrame _outputFrame;
    private bool _isDisposed;
    private long _successfulFrameCount;

    private RealtimeVsrFrameProcessor(
        VideoScaler videoScaler,
        VideoFrame inputFrame,
        VideoFrame originalOutputFrame,
        VideoFrame outputFrame)
    {
        _videoScaler = videoScaler;
        _inputFrame = inputFrame;
        _originalOutputFrame = originalOutputFrame;
        _outputFrame = outputFrame;
    }

    public uint InputWidth => (uint)_inputFrame.Direct3DSurface!.Description.Width;

    public uint InputHeight => (uint)_inputFrame.Direct3DSurface!.Description.Height;

    public uint OutputWidth => (uint)_outputFrame.Direct3DSurface!.Description.Width;

    public uint OutputHeight => (uint)_outputFrame.Direct3DSurface!.Description.Height;

    public long SuccessfulFrameCount => Interlocked.Read(ref _successfulFrameCount);

    public static async Task<RealtimeVsrFrameProcessor> CreateAsync(
        uint inputWidth,
        uint inputHeight,
        uint outputWidth,
        uint outputHeight)
    {
        var videoScaler = await VideoScaler.CreateAsync();
        VideoFrame? inputFrame = null;
        VideoFrame? originalOutputFrame = null;
        VideoFrame? outputFrame = null;

        try
        {
            inputFrame = VideoFrame.CreateAsDirect3D11SurfaceBacked(
                DirectXPixelFormat.NV12,
                checked((int)inputWidth),
                checked((int)inputHeight));
            originalOutputFrame = VideoFrame.CreateAsDirect3D11SurfaceBacked(
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                checked((int)inputWidth),
                checked((int)inputHeight));
            outputFrame = VideoFrame.CreateAsDirect3D11SurfaceBacked(
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                checked((int)outputWidth),
                checked((int)outputHeight));
            return new RealtimeVsrFrameProcessor(
                videoScaler,
                inputFrame,
                originalOutputFrame,
                outputFrame);
        }
        catch
        {
            inputFrame?.Dispose();
            originalOutputFrame?.Dispose();
            outputFrame?.Dispose();
            videoScaler.Dispose();
            throw;
        }
    }

    public async Task<RealtimeVsrFrameResult> ProcessCurrentFrameAsync(
        MediaPlayer mediaPlayer)
    {
        await _processingGate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            var inputSurface = _inputFrame.Direct3DSurface
                ?? throw new InvalidOperationException("VSR input surface is unavailable.");
            var outputSurface = _outputFrame.Direct3DSurface
                ?? throw new InvalidOperationException("VSR output surface is unavailable.");

            mediaPlayer.CopyFrameToVideoSurface(inputSurface);
            await _inputFrame.CopyToAsync(_originalOutputFrame);
            var result = _videoScaler.Scale(
                inputSurface,
                outputSurface,
                new VideoScalerOptions());

            if (result.Status != VideoScalerStatus.Success)
            {
                throw new InvalidOperationException(
                    $"VideoScaler failed to process a realtime frame: {result.Status}");
            }

            var successfulFrameCount = Interlocked.Increment(ref _successfulFrameCount);
            if (successfulFrameCount == 1 || successfulFrameCount % 300 == 0)
            {
                Debug.WriteLine(
                    $"[VSR] Realtime frame {successfulFrameCount}: " +
                    $"{inputSurface.Description.Width}x{inputSurface.Description.Height} -> " +
                    $"{outputSurface.Description.Width}x{outputSurface.Description.Height}, " +
                    $"status {result.Status}.");
            }

            var originalBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(
                _originalOutputFrame.Direct3DSurface!,
                BitmapAlphaMode.Premultiplied);
            var vsrBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(
                outputSurface,
                BitmapAlphaMode.Premultiplied);
            return new RealtimeVsrFrameResult(originalBitmap, vsrBitmap);
        }
        finally
        {
            _processingGate.Release();
        }
    }

    public void Dispose()
    {
        _processingGate.Wait();
        try
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _inputFrame.Dispose();
            _originalOutputFrame.Dispose();
            _outputFrame.Dispose();
            _videoScaler.Dispose();
        }
        finally
        {
            _processingGate.Release();
            _processingGate.Dispose();
        }
    }

}

public sealed record RealtimeVsrFrameResult(
    SoftwareBitmap OriginalBitmap,
    SoftwareBitmap VsrBitmap) : IDisposable
{
    public void Dispose()
    {
        OriginalBitmap.Dispose();
        VsrBitmap.Dispose();
    }
}
