using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using WindowsAiVsrCpuDemo.Infrastructure;
using WindowsAiVsrCpuDemo.Services;
using WinRT.Interop;

namespace WindowsAiVsrCpuDemo;

public sealed partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly VideoProcessingService _videoProcessingService = new();
    private readonly DispatcherTimer _realtimeLoopTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(100)
    };
    private MediaPlayer? _vsrRealtimeMediaPlayer;
    private MediaTimelineController? _realtimeTimeline;
    private RealtimeVsrFrameProcessor? _realtimeFrameProcessor;
    private StorageFile? _inputFile;
    private StorageFile? _outputFile;
    private VideoJobInfo? _videoJob;
    private CancellationTokenSource? _processingCancellation;
    private bool _isBusy;
    private bool _isInitialized;
    private bool _isClosing;
    private bool _isRealtimeReady;
    private bool _isRealtimeStarted;
    private bool _isRestartingRealtimeLoop;
    private bool _isSyncingOutputScaleSelection;
    private int _isRealtimeFrameProcessing;
    private int _realtimePreparationGeneration;
    private long _realtimeDroppedFrameCount;
    private double _realtimeZoomFactor = 1;
    private double _realtimeFocusX = 0.5;
    private double _realtimeFocusY = 0.5;
    private uint? _realtimeDragPointerId;
    private Windows.Foundation.Point _realtimeDragStartPosition;
    private double _realtimeDragStartFocusX;
    private double _realtimeDragStartFocusY;
    private double _requestedScaleFactor = 2;
    private string _inputFileName = "未選択";
    private string _inputDetails = "対応入力: 240p～1440p、15～60 fps";
    private string _outputFileName = "未選択";
    private string _outputDetails = "2 倍を目標に、最大 4K まで拡大します。";
    private string _statusText = "動画を選択してください。";
    private string _hardwareMessage = "VSR の利用可否を確認しています…";
    private string _realtimePlayPauseLabel = "比較を再生";
    private string _realtimeZoomText = "1.00x";
    private string _realtimeStatusText = "動画を選択すると比較を準備します。";
    private string _realtimeDiagnosticsText = "動画を選択すると VSR 推論状態を確認できます。";
    private SoftwareBitmapSource? _originalRealtimeFrameSource;
    private SoftwareBitmapSource? _vsrRealtimeFrameSource;
    private double _progressValue;
    private InfoBarSeverity _hardwareSeverity = InfoBarSeverity.Informational;

    public MainWindow()
    {
        PickInputCommand = new AsyncCommand(PickInputAsync, () => !IsBusy);
        PickOutputCommand = new AsyncCommand(PickOutputAsync, () => !IsBusy && _inputFile is not null);
        StartCommand = new AsyncCommand(StartProcessingAsync, () => !IsBusy && _inputFile is not null);
        CancelCommand = new RelayCommand(CancelProcessing, () => IsBusy);
        OpenOutputFolderCommand = new AsyncCommand(OpenOutputFolderAsync, () => !IsBusy && _outputFile is not null);
        PlayPauseRealtimeCommand = new RelayCommand(
            PlayPauseRealtime,
            () => !IsBusy && _isRealtimeReady);
        RestartRealtimeCommand = new RelayCommand(
            RestartRealtime,
            () => !IsBusy && _isRealtimeReady);
        ResetRealtimeZoomCommand = new RelayCommand(
            ResetRealtimeZoom,
            () => _isRealtimeReady);

        InitializeComponent();

        RealtimeZoomSlider.Minimum = 1;
        RealtimeZoomSlider.Maximum = 4;
        RealtimeZoomSlider.StepFrequency = 0.25;
        RealtimeZoomSlider.Value = 1;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        SystemBackdrop = new MicaBackdrop();
        Title = "Windows AI Video Super Resolution デモ";
        ResizeWindow(1500, 900);

        Activated += OnActivated;
        Closed += OnClosed;
        _realtimeLoopTimer.Tick += OnRealtimeLoopTimerTick;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AsyncCommand PickInputCommand { get; }

    public AsyncCommand PickOutputCommand { get; }

    public AsyncCommand StartCommand { get; }

    public RelayCommand CancelCommand { get; }

    public AsyncCommand OpenOutputFolderCommand { get; }

    public RelayCommand PlayPauseRealtimeCommand { get; }

    public RelayCommand RestartRealtimeCommand { get; }

    public RelayCommand ResetRealtimeZoomCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanSelectOutputScale));
                NotifyCommandStates();
            }
        }
    }

    public bool CanSelectOutputScale => !IsBusy;

    public string InputFileName
    {
        get => _inputFileName;
        private set => SetProperty(ref _inputFileName, value);
    }

    public string InputDetails
    {
        get => _inputDetails;
        private set => SetProperty(ref _inputDetails, value);
    }

    public string OutputFileName
    {
        get => _outputFileName;
        private set => SetProperty(ref _outputFileName, value);
    }

    public string OutputDetails
    {
        get => _outputDetails;
        private set => SetProperty(ref _outputDetails, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string HardwareMessage
    {
        get => _hardwareMessage;
        private set => SetProperty(ref _hardwareMessage, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetProperty(ref _progressValue, value);
    }

    public InfoBarSeverity HardwareSeverity
    {
        get => _hardwareSeverity;
        private set => SetProperty(ref _hardwareSeverity, value);
    }

    public string RealtimePlayPauseLabel
    {
        get => _realtimePlayPauseLabel;
        private set => SetProperty(ref _realtimePlayPauseLabel, value);
    }

    public string RealtimeStatusText
    {
        get => _realtimeStatusText;
        private set => SetProperty(ref _realtimeStatusText, value);
    }

    public string RealtimeZoomText
    {
        get => _realtimeZoomText;
        private set => SetProperty(ref _realtimeZoomText, value);
    }

    public string RealtimeDiagnosticsText
    {
        get => _realtimeDiagnosticsText;
        private set => SetProperty(ref _realtimeDiagnosticsText, value);
    }

    public SoftwareBitmapSource? VsrRealtimeFrameSource
    {
        get => _vsrRealtimeFrameSource;
        private set => SetProperty(ref _vsrRealtimeFrameSource, value);
    }

    public SoftwareBitmapSource? OriginalRealtimeFrameSource
    {
        get => _originalRealtimeFrameSource;
        private set => SetProperty(ref _originalRealtimeFrameSource, value);
    }

    private async void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        await InitializeVsrAsync();
    }

    private async Task InitializeVsrAsync()
    {
        try
        {
            var availability = await _videoProcessingService.CheckAvailabilityAsync();
            HardwareSeverity = availability.IsReady
                ? InfoBarSeverity.Success
                : InfoBarSeverity.Warning;
            HardwareMessage = availability.Message;
        }
        catch (Exception ex)
        {
            HardwareSeverity = InfoBarSeverity.Error;
            HardwareMessage =
                $"VSR の初期化確認に失敗しました: {ex.GetType().Name}, HRESULT 0x{ex.HResult:X8}, {ex.Message}";
        }
    }

    private async Task PickInputAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.VideosLibrary,
            ViewMode = PickerViewMode.Thumbnail
        };
        picker.FileTypeFilter.Add(".mp4");
        picker.FileTypeFilter.Add(".mov");
        picker.FileTypeFilter.Add(".m4v");
        picker.FileTypeFilter.Add(".wmv");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        ReleaseRealtimeComparison();

        try
        {
            _videoJob = await _videoProcessingService.InspectVideoAsync(
                file,
                _requestedScaleFactor);
            SyncOutputScaleSelection(_videoJob.RequestedScaleFactor);
            _inputFile = file;
            _outputFile = null;
            InputFileName = file.Name;
            InputDetails =
                $"{_videoJob.InputWidth} × {_videoJob.InputHeight} / {_videoJob.FramesPerSecond:F2} fps / {_videoJob.Duration:hh\\:mm\\:ss}";
            OutputFileName = "未選択";
            OutputDetails =
                $"{_videoJob.OutputWidth} × {_videoJob.OutputHeight} / {_videoJob.ScaleFactor:F2} 倍 / MP4 (H.264 + AAC)";
            StatusText = "保存先を選択してから VSR 処理を開始してください。";
            ProgressValue = 0;
            PreviewPlayer.Source = MediaSource.CreateFromStorageFile(file);
            await PrepareRealtimeComparisonAsync(file, _videoJob);
            NotifyCommandStates();
        }
        catch (Exception ex)
        {
            PreviewPlayer.Source = null;
            _inputFile = null;
            _videoJob = null;
            InputFileName = "読み込み失敗";
            InputDetails = ex.Message;
            StatusText = "別の動画を選択してください。";
            NotifyCommandStates();
        }
    }

    private async void OnOutputScaleSelectionChanged(
        object sender,
        SelectionChangedEventArgs args)
    {
        if (_isSyncingOutputScaleSelection ||
            IsBusy ||
            sender is not ComboBox { SelectedItem: ComboBoxItem selectedItem } ||
            selectedItem.Tag is not string scaleText ||
            !double.TryParse(
                scaleText,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var requestedScaleFactor))
        {
            return;
        }

        _requestedScaleFactor = requestedScaleFactor;
        if (_videoJob is null || _inputFile is null)
        {
            OutputDetails = $"{requestedScaleFactor:0.##} 倍を目標に、最大 4K まで拡大します。";
            return;
        }

        ReleaseRealtimeComparison();
        _videoJob = _videoProcessingService.ChangeScaleFactor(
            _videoJob,
            requestedScaleFactor);
        SyncOutputScaleSelection(_videoJob.RequestedScaleFactor);
        OutputDetails =
            $"{_videoJob.OutputWidth} × {_videoJob.OutputHeight} / {_videoJob.ScaleFactor:F2} 倍 / MP4 (H.264 + AAC)";
        StatusText = _outputFile is null
            ? "保存先を選択してから VSR 処理を開始してください。"
            : "選択した倍率で VSR 処理を開始できます。";
        await PrepareRealtimeComparisonAsync(_inputFile, _videoJob);
    }

    private void SyncOutputScaleSelection(double scaleFactor)
    {
        var selectedIndex = (int)Math.Round((scaleFactor - 1.25D) * 4D);
        _isSyncingOutputScaleSelection = true;
        try
        {
            OutputScaleComboBox.SelectedIndex = Math.Clamp(selectedIndex, 0, 11);
            _requestedScaleFactor = scaleFactor;
        }
        finally
        {
            _isSyncingOutputScaleSelection = false;
        }
    }

    private async Task PickOutputAsync()
    {
        if (_inputFile is null)
        {
            return;
        }

        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.VideosLibrary,
            SuggestedFileName = $"{Path.GetFileNameWithoutExtension(_inputFile.Name)}_vsr"
        };
        picker.FileTypeChoices.Add("MP4 動画", [".mp4"]);
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }

        _outputFile = file;
        OutputFileName = file.Name;
        StatusText = "VSR 処理を開始できます。";
        NotifyCommandStates();
    }

    private async Task StartProcessingAsync()
    {
        if (_inputFile is null || _videoJob is null)
        {
            return;
        }

        if (_outputFile is null)
        {
            await PickOutputAsync();
            if (_outputFile is null)
            {
                return;
            }
        }

        _processingCancellation = new CancellationTokenSource();
        IsBusy = true;
        ProgressValue = 0;
        StatusText = "VSR セッションを確認しています…";

        try
        {
            var progress = new Progress<double>(value =>
            {
                ProgressValue = value;
                StatusText = $"AI 超解像度処理中… {value:F0}%";
            });

            await _videoProcessingService.RenderAsync(
                _inputFile,
                _outputFile,
                _videoJob,
                progress,
                _processingCancellation.Token);

            if (_isClosing)
            {
                return;
            }

            ProgressValue = 100;
            StatusText = $"完了: {_outputFile.Path}";
            PreviewPlayer.Source = MediaSource.CreateFromStorageFile(_outputFile);
        }
        catch (OperationCanceledException)
        {
            if (_isClosing)
            {
                return;
            }

            StatusText = "処理をキャンセルしました。部分的な出力ファイルは削除されます。";
            ProgressValue = 0;
            ResetDeletedOutput();
        }
        catch (Exception ex)
        {
            if (_isClosing)
            {
                return;
            }

            StatusText = $"処理に失敗しました: {ex.Message}";
            ProgressValue = 0;
            ResetDeletedOutput();
        }
        finally
        {
            _processingCancellation.Dispose();
            _processingCancellation = null;
            if (!_isClosing)
            {
                IsBusy = false;
            }
        }
    }

    private void CancelProcessing()
    {
        StatusText = "キャンセルしています…";
        _processingCancellation?.Cancel();
    }

    private async Task PrepareRealtimeComparisonAsync(
        StorageFile file,
        VideoJobInfo job)
    {
        var preparationGeneration = ++_realtimePreparationGeneration;
        RealtimeStatusText = "リアルタイム比較を準備しています…";

        try
        {
            var (previewWidth, previewHeight) = CalculateRealtimePreviewSize(job);
            var frameProcessor = await RealtimeVsrFrameProcessor.CreateAsync(
                job.InputWidth,
                job.InputHeight,
                previewWidth,
                previewHeight);
            if (_isClosing || preparationGeneration != _realtimePreparationGeneration)
            {
                frameProcessor.Dispose();
                return;
            }

            _realtimeFrameProcessor = frameProcessor;
            var vsrPlayer = new MediaPlayer
            {
                AutoPlay = false,
                IsVideoFrameServerEnabled = true,
                RealTimePlayback = true
            };

            var timeline = new MediaTimelineController
            {
                Duration = job.Duration,
                IsLoopingEnabled = false
            };
            vsrPlayer.CommandManager.IsEnabled = false;
            vsrPlayer.TimelineController = timeline;

            _vsrRealtimeMediaPlayer = vsrPlayer;
            _realtimeTimeline = timeline;
            _realtimeDroppedFrameCount = 0;
            Interlocked.Exchange(ref _isRealtimeFrameProcessing, 0);
            OriginalRealtimeFrameSource = new SoftwareBitmapSource();
            VsrRealtimeFrameSource = new SoftwareBitmapSource();
            var vsrOpened = WaitForMediaOpenedAsync(vsrPlayer);
            vsrPlayer.MediaEnded += OnRealtimeVsrMediaEnded;
            vsrPlayer.Source = MediaSource.CreateFromStorageFile(file);

            await vsrOpened.WaitAsync(TimeSpan.FromSeconds(30));
            if (_isClosing || preparationGeneration != _realtimePreparationGeneration)
            {
                return;
            }

            vsrPlayer.VideoFrameAvailable += OnRealtimeVideoFrameAvailable;
            _isRealtimeReady = true;
            _realtimeZoomFactor = 1;
            _realtimeFocusX = 0.5;
            _realtimeFocusY = 0.5;
            RealtimeZoomSlider.Value = 1;
            ApplyRealtimeZoom();
            RealtimePlayPauseLabel = "比較を再生";
            RealtimeZoomText = "1.00x";
            RealtimeStatusText =
                $"{job.InputWidth}×{job.InputHeight} → {previewWidth}×{previewHeight} をリアルタイム同期表示";
            RealtimeDiagnosticsText =
                $"推論待機中: 入力 NV12 {job.InputWidth}×{job.InputHeight} → " +
                $"AI 出力 BGRA {previewWidth}×{previewHeight}" +
                (previewWidth > 2560 || previewHeight > 1440
                    ? "。1440p 超のリアルタイム処理ではフレーム落ちが増える場合があります。"
                    : string.Empty);
        }
        catch (Exception ex)
        {
            ReleaseRealtimeComparison();
            if (!_isClosing)
            {
                RealtimeStatusText = $"リアルタイム比較を準備できませんでした: {ex.Message}";
            }
        }
    }

    private void PlayPauseRealtime()
    {
        if (_realtimeTimeline is null || !_isRealtimeReady)
        {
            return;
        }

        if (_realtimeTimeline.State == MediaTimelineControllerState.Running)
        {
            _realtimeTimeline.Pause();
            _realtimeLoopTimer.Stop();
            RealtimePlayPauseLabel = "比較を再生";
            RealtimeStatusText = "一時停止中";
            return;
        }

        if (_isRealtimeStarted)
        {
            _realtimeTimeline.Resume();
        }
        else
        {
            _realtimeTimeline.Start();
            _isRealtimeStarted = true;
        }

        _realtimeLoopTimer.Start();
        RealtimePlayPauseLabel = "一時停止";
        RealtimeStatusText = "原画と VSR をリアルタイム同期再生中";
    }

    private void RestartRealtime()
    {
        if (_realtimeTimeline is null || !_isRealtimeReady)
        {
            return;
        }

        _realtimeTimeline.Position = TimeSpan.Zero;
        if (!_isRealtimeStarted)
        {
            _realtimeTimeline.Start();
            _isRealtimeStarted = true;
        }
        else if (_realtimeTimeline.State != MediaTimelineControllerState.Running)
        {
            _realtimeTimeline.Resume();
        }

        _realtimeLoopTimer.Start();
        RealtimePlayPauseLabel = "一時停止";
        RealtimeStatusText = "先頭からリアルタイム同期再生中";
    }

    private void OnRealtimeVsrMediaEnded(MediaPlayer sender, object args)
    {
        TryRestartRealtimeLoop();
    }

    private async void OnRealtimeVideoFrameAvailable(MediaPlayer sender, object args)
    {
        if (Interlocked.CompareExchange(ref _isRealtimeFrameProcessing, 1, 0) != 0)
        {
            Interlocked.Increment(ref _realtimeDroppedFrameCount);
            return;
        }

        var processor = _realtimeFrameProcessor;
        var preparationGeneration = _realtimePreparationGeneration;

        try
        {
            if (_isClosing || processor is null || sender != _vsrRealtimeMediaPlayer)
            {
                return;
            }

            var frameResult = await processor.ProcessCurrentFrameAsync(sender);
            await PresentRealtimeFrameAsync(
                frameResult,
                processor,
                preparationGeneration);
        }
        catch (Exception ex)
        {
            sender.VideoFrameAvailable -= OnRealtimeVideoFrameAvailable;
            Debug.WriteLine($"[VSR] Realtime processing failed: {ex}");
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_isClosing ||
                    preparationGeneration != _realtimePreparationGeneration)
                {
                    return;
                }

                _realtimeTimeline?.Pause();
                _realtimeLoopTimer.Stop();
                _isRealtimeReady = false;
                RealtimePlayPauseLabel = "比較を再生";
                RealtimeStatusText = "VSR リアルタイム処理を停止しました。";
                RealtimeDiagnosticsText =
                    $"VSR 推論エラー: {ex.GetType().Name}, HRESULT 0x{ex.HResult:X8}, {ex.Message}";
                NotifyCommandStates();
            });
        }
        finally
        {
            Interlocked.Exchange(ref _isRealtimeFrameProcessing, 0);
        }
    }

    private Task PresentRealtimeFrameAsync(
        RealtimeVsrFrameResult frameResult,
        RealtimeVsrFrameProcessor processor,
        int preparationGeneration)
    {
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        if (!DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    if (_isClosing ||
                        preparationGeneration != _realtimePreparationGeneration ||
                        processor != _realtimeFrameProcessor ||
                        OriginalRealtimeFrameSource is not { } originalFrameSource ||
                        VsrRealtimeFrameSource is not { } frameSource)
                    {
                        return;
                    }

                    await originalFrameSource.SetBitmapAsync(frameResult.OriginalBitmap);
                    await frameSource.SetBitmapAsync(frameResult.VsrBitmap);
                    var successfulFrameCount = processor.SuccessfulFrameCount;
                    if (successfulFrameCount == 1 || successfulFrameCount % 30 == 0)
                    {
                        RealtimeDiagnosticsText =
                            $"VSR 推論実行中: 入力 {processor.InputWidth}×{processor.InputHeight} → " +
                            $"AI 出力 {processor.OutputWidth}×{processor.OutputHeight}, " +
                            $"成功 {successfulFrameCount} フレーム, " +
                            $"処理待ちで省略 {Interlocked.Read(ref _realtimeDroppedFrameCount)} フレーム";
                    }
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                    return;
                }
                finally
                {
                    frameResult.Dispose();
                }

                completion.TrySetResult();
            }))
        {
            frameResult.Dispose();
            completion.TrySetException(
                new InvalidOperationException("VSR frame presentation could not be queued."));
        }

        return completion.Task;
    }

    private void OnRealtimeLoopTimerTick(object? sender, object args)
    {
        if (_realtimeTimeline is null ||
            _videoJob is null ||
            _realtimeTimeline.State != MediaTimelineControllerState.Running)
        {
            return;
        }

        var restartPosition = _videoJob.Duration - TimeSpan.FromMilliseconds(150);
        if (_realtimeTimeline.Position >= restartPosition)
        {
            TryRestartRealtimeLoop();
        }
    }

    private void TryRestartRealtimeLoop()
    {
        if (_isClosing ||
            !_isRealtimeStarted ||
            _isRestartingRealtimeLoop ||
            _inputFile is null ||
            _videoJob is null)
        {
            return;
        }

        _isRestartingRealtimeLoop = true;
        _realtimeLoopTimer.Stop();
        if (!DispatcherQueue.TryEnqueue(RestartRealtimeLoop))
        {
            _isRestartingRealtimeLoop = false;
        }
    }

    private void RestartRealtimeLoop()
    {
        var timeline = _realtimeTimeline;

        try
        {
            if (_isClosing || timeline is null)
            {
                return;
            }

            timeline.Pause();
            timeline.Position = TimeSpan.Zero;
            timeline.Start();
            _isRealtimeStarted = true;
            _realtimeLoopTimer.Start();
            RealtimePlayPauseLabel = "一時停止";
            RealtimeStatusText = "原画と VSR を先頭から同期ループ再生中";
        }
        catch (Exception ex)
        {
            if (!_isClosing)
            {
                RealtimeStatusText = $"ループ再開に失敗しました: {ex.Message}";
            }
        }
        finally
        {
            _isRestartingRealtimeLoop = false;
        }
    }

    private void OnRealtimeZoomChanged(
        object sender,
        Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args)
    {
        _realtimeZoomFactor = args.NewValue;
        RealtimeZoomText = $"{_realtimeZoomFactor:F2}x";
        ApplyRealtimeZoom();

        if (_isRealtimeReady && _realtimeZoomFactor > 1)
        {
            RealtimeStatusText =
                $"{_realtimeZoomFactor:F2} 倍で拡大中。映像をクリックまたはドラッグして注目位置を変更できます。";
        }
    }

    private void OnRealtimePlayerPointerPressed(
        object sender,
        PointerRoutedEventArgs args)
    {
        if (!_isRealtimeReady || sender is not FrameworkElement viewport)
        {
            return;
        }

        var point = args.GetCurrentPoint(viewport);
        if (!TryGetImageFocusFromPointer(
                viewport,
                point.Position,
                out var focusX,
                out var focusY))
        {
            return;
        }

        _realtimeFocusX = focusX;
        _realtimeFocusY = focusY;

        if (_realtimeZoomFactor <= 1)
        {
            RealtimeZoomSlider.Value = 2;
        }
        else
        {
            ApplyRealtimeZoom();
        }

        _realtimeDragPointerId = args.Pointer.PointerId;
        _realtimeDragStartPosition = point.Position;
        _realtimeDragStartFocusX = _realtimeFocusX;
        _realtimeDragStartFocusY = _realtimeFocusY;
        viewport.CapturePointer(args.Pointer);
        RealtimeStatusText =
            $"選択した画素を中心に {_realtimeZoomFactor:F2} 倍で左右同時に拡大しています。";
        args.Handled = true;
    }

    private void OnRealtimePlayerPointerMoved(
        object sender,
        PointerRoutedEventArgs args)
    {
        if (!_isRealtimeReady ||
            sender is not FrameworkElement viewport ||
            _realtimeDragPointerId != args.Pointer.PointerId ||
            !TryGetUniformContentRect(viewport, out var contentRect))
        {
            return;
        }

        var position = args.GetCurrentPoint(viewport).Position;
        var scale = Math.Max(1, _realtimeZoomFactor);
        _realtimeFocusX = Math.Clamp(
            _realtimeDragStartFocusX +
                ((position.X - _realtimeDragStartPosition.X) /
                    (contentRect.Width * scale)),
            0,
            1);
        _realtimeFocusY = Math.Clamp(
            _realtimeDragStartFocusY +
                ((position.Y - _realtimeDragStartPosition.Y) /
                    (contentRect.Height * scale)),
            0,
            1);
        ApplyRealtimeZoom();
        RealtimeStatusText =
            $"ドラッグした注目点を中心に {_realtimeZoomFactor:F2} 倍で左右同時に拡大しています。";
        args.Handled = true;
    }

    private void OnRealtimePlayerPointerReleased(
        object sender,
        PointerRoutedEventArgs args)
    {
        EndRealtimeFocusDrag(sender, args);
    }

    private void OnRealtimePlayerPointerCanceled(
        object sender,
        PointerRoutedEventArgs args)
    {
        EndRealtimeFocusDrag(sender, args);
    }

    private void OnRealtimePlayerPointerCaptureLost(
        object sender,
        PointerRoutedEventArgs args)
    {
        if (_realtimeDragPointerId == args.Pointer.PointerId)
        {
            _realtimeDragPointerId = null;
        }
    }

    private void EndRealtimeFocusDrag(
        object sender,
        PointerRoutedEventArgs args)
    {
        if (sender is not FrameworkElement viewport ||
            _realtimeDragPointerId != args.Pointer.PointerId)
        {
            return;
        }

        _realtimeDragPointerId = null;
        viewport.ReleasePointerCapture(args.Pointer);
        args.Handled = true;
    }

    private void OnRealtimeViewportSizeChanged(
        object sender,
        SizeChangedEventArgs args)
    {
        ApplyRealtimeZoom();
    }

    private void ResetRealtimeZoom()
    {
        _realtimeFocusX = 0.5;
        _realtimeFocusY = 0.5;
        RealtimeZoomSlider.Value = 1;
        ApplyRealtimeZoom();
        RealtimeStatusText = "拡大表示をリセットしました。";
    }

    private void ApplyRealtimeZoom()
    {
        ApplyZoomTransform(
            OriginalZoomTransform,
            OriginalRealtimeViewport.ActualWidth,
            OriginalRealtimeViewport.ActualHeight,
            _videoJob?.InputWidth ?? 1,
            _videoJob?.InputHeight ?? 1);
        ApplyZoomTransform(
            VsrZoomTransform,
            VsrRealtimeViewport.ActualWidth,
            VsrRealtimeViewport.ActualHeight,
            _realtimeFrameProcessor?.OutputWidth ?? 1,
            _realtimeFrameProcessor?.OutputHeight ?? 1);
    }

    private void ApplyZoomTransform(
        CompositeTransform transform,
        double width,
        double height,
        double sourceWidth,
        double sourceHeight)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var scale = Math.Max(1, _realtimeZoomFactor);
        if (scale == 1)
        {
            transform.ScaleX = 1;
            transform.ScaleY = 1;
            transform.CenterX = width / 2;
            transform.CenterY = height / 2;
            transform.TranslateX = 0;
            transform.TranslateY = 0;
            return;
        }

        var contentRect = CalculateUniformContentRect(
            width,
            height,
            sourceWidth,
            sourceHeight);
        var focusX = contentRect.X + (contentRect.Width * _realtimeFocusX);
        var focusY = contentRect.Y + (contentRect.Height * _realtimeFocusY);
        transform.ScaleX = scale;
        transform.ScaleY = scale;
        transform.CenterX = focusX;
        transform.CenterY = focusY;
        transform.TranslateX = (width / 2) - focusX;
        transform.TranslateY = (height / 2) - focusY;
    }

    private bool TryGetImageFocusFromPointer(
        FrameworkElement viewport,
        Windows.Foundation.Point pointerPosition,
        out double focusX,
        out double focusY)
    {
        focusX = 0;
        focusY = 0;
        if (!TryGetUniformContentRect(viewport, out var contentRect))
        {
            return false;
        }

        var transform = viewport.Tag as string == "Vsr"
            ? VsrZoomTransform
            : OriginalZoomTransform;
        var scaleX = Math.Max(1, transform.ScaleX);
        var scaleY = Math.Max(1, transform.ScaleY);
        var imageX = transform.CenterX +
            ((pointerPosition.X - transform.TranslateX - transform.CenterX) / scaleX);
        var imageY = transform.CenterY +
            ((pointerPosition.Y - transform.TranslateY - transform.CenterY) / scaleY);
        if (imageX < contentRect.X ||
            imageX > contentRect.X + contentRect.Width ||
            imageY < contentRect.Y ||
            imageY > contentRect.Y + contentRect.Height)
        {
            return false;
        }

        focusX = Math.Clamp((imageX - contentRect.X) / contentRect.Width, 0, 1);
        focusY = Math.Clamp((imageY - contentRect.Y) / contentRect.Height, 0, 1);
        return true;
    }

    private bool TryGetUniformContentRect(
        FrameworkElement viewport,
        out Windows.Foundation.Rect contentRect)
    {
        var isVsr = viewport.Tag as string == "Vsr";
        var sourceWidth = isVsr
            ? _realtimeFrameProcessor?.OutputWidth
            : _videoJob?.InputWidth;
        var sourceHeight = isVsr
            ? _realtimeFrameProcessor?.OutputHeight
            : _videoJob?.InputHeight;
        if (viewport.ActualWidth <= 0 ||
            viewport.ActualHeight <= 0 ||
            sourceWidth is null ||
            sourceHeight is null)
        {
            contentRect = default;
            return false;
        }

        contentRect = CalculateUniformContentRect(
            viewport.ActualWidth,
            viewport.ActualHeight,
            sourceWidth.Value,
            sourceHeight.Value);
        return true;
    }

    private static Windows.Foundation.Rect CalculateUniformContentRect(
        double viewportWidth,
        double viewportHeight,
        double sourceWidth,
        double sourceHeight)
    {
        var scale = Math.Min(
            viewportWidth / sourceWidth,
            viewportHeight / sourceHeight);
        var width = sourceWidth * scale;
        var height = sourceHeight * scale;
        return new Windows.Foundation.Rect(
            (viewportWidth - width) / 2,
            (viewportHeight - height) / 2,
            width,
            height);
    }

    private async Task OpenOutputFolderAsync()
    {
        if (_outputFile is null)
        {
            return;
        }

        var folder = await _outputFile.GetParentAsync();
        if (folder is not null)
        {
            await Launcher.LaunchFolderAsync(folder);
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _isClosing = true;
        _processingCancellation?.Cancel();
        ReleaseRealtimeComparison();
        _realtimeLoopTimer.Stop();
        _realtimeLoopTimer.Tick -= OnRealtimeLoopTimerTick;
        Activated -= OnActivated;
        Closed -= OnClosed;
        PickInputCommand.Dispose();
        PickOutputCommand.Dispose();
        StartCommand.Dispose();
        CancelCommand.Dispose();
        OpenOutputFolderCommand.Dispose();
        PlayPauseRealtimeCommand.Dispose();
        RestartRealtimeCommand.Dispose();
        ResetRealtimeZoomCommand.Dispose();
    }

    private void NotifyCommandStates()
    {
        if (_isClosing)
        {
            return;
        }

        PickInputCommand.NotifyCanExecuteChanged();
        PickOutputCommand.NotifyCanExecuteChanged();
        StartCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        OpenOutputFolderCommand.NotifyCanExecuteChanged();
        PlayPauseRealtimeCommand.NotifyCanExecuteChanged();
        RestartRealtimeCommand.NotifyCanExecuteChanged();
        ResetRealtimeZoomCommand.NotifyCanExecuteChanged();
    }

    private void ReleaseRealtimeComparison()
    {
        _realtimePreparationGeneration++;
        _isRealtimeReady = false;
        _isRealtimeStarted = false;
        _realtimeZoomFactor = 1;
        _realtimeFocusX = 0.5;
        _realtimeFocusY = 0.5;

        _realtimeLoopTimer.Stop();
        _realtimeTimeline?.Pause();
        if (_vsrRealtimeMediaPlayer is not null)
        {
            _vsrRealtimeMediaPlayer.MediaEnded -= OnRealtimeVsrMediaEnded;
            _vsrRealtimeMediaPlayer.VideoFrameAvailable -= OnRealtimeVideoFrameAvailable;
            _vsrRealtimeMediaPlayer.Source = null;
            _vsrRealtimeMediaPlayer.TimelineController = null;
        }

        _vsrRealtimeMediaPlayer?.Dispose();
        _realtimeFrameProcessor?.Dispose();
        _vsrRealtimeMediaPlayer = null;
        _realtimeTimeline = null;
        _realtimeFrameProcessor = null;
        OriginalRealtimeFrameSource = null;
        VsrRealtimeFrameSource = null;
        RealtimeDiagnosticsText = "動画を選択すると VSR 推論状態を確認できます。";
    }

    private static (uint Width, uint Height) CalculateRealtimePreviewSize(
        VideoJobInfo job)
    {
        return (job.OutputWidth, job.OutputHeight);
    }

    private static Task WaitForMediaOpenedAsync(MediaPlayer player)
    {
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        void OnOpened(MediaPlayer sender, object args)
        {
            sender.MediaOpened -= OnOpened;
            sender.MediaFailed -= OnFailed;
            completion.TrySetResult();
        }

        void OnFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            sender.MediaOpened -= OnOpened;
            sender.MediaFailed -= OnFailed;
            completion.TrySetException(new InvalidOperationException(
                $"メディアを開けませんでした: {args.Error}, {args.ErrorMessage}"));
        }

        player.MediaOpened += OnOpened;
        player.MediaFailed += OnFailed;
        return completion.Task;
    }

    private void ResetDeletedOutput()
    {
        _outputFile = null;
        OutputFileName = "再選択が必要";
        NotifyCommandStates();
    }

    private bool SetProperty<T>(
        ref T storage,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        if (!_isClosing)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (!_isClosing)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    private void ResizeWindow(int widthInDips, int heightInDips)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var scale = GetDpiForWindow(hwnd) / 96.0;
        AppWindow.Resize(new SizeInt32(
            (int)(widthInDips * scale),
            (int)(heightInDips * scale)));
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
