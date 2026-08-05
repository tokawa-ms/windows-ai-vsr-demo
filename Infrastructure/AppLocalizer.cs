using System.Globalization;

namespace WindowsAiVsrCpuDemo.Infrastructure;

public enum AppLanguage
{
    English,
    Japanese
}

public static class AppLocalizer
{
    private static readonly IReadOnlyDictionary<string, (string English, string Japanese)> Strings =
        new Dictionary<string, (string English, string Japanese)>
        {
            ["AppTitle"] = ("Windows AI Video Super Resolution Demo", "Windows AI Video Super Resolution デモ"),
            ["HeroTitle"] = ("Upscale video with AI", "AI で動画を高解像度化"),
            ["HeroDescription"] = ("Compare the results in real time and save an MP4 with audio when needed.", "リアルタイム比較で効果を確認し、必要に応じて音声付き MP4 として保存できます。"),
            ["Language"] = ("Language", "表示言語"),
            ["English"] = ("English", "English"),
            ["Japanese"] = ("日本語", "日本語"),
            ["HardwareTitle"] = ("Processing hardware", "実行ハードウェア"),
            ["RealtimeTab"] = ("Real-time VSR comparison", "リアルタイム VSR 比較"),
            ["ExportTab"] = ("Export MP4", "MP4 書き出し"),
            ["ChooseComparisonVideo"] = ("Choose a video to compare", "比較する動画を選択"),
            ["Restart"] = ("Restart", "先頭から再生"),
            ["Zoom"] = ("Zoom", "拡大率"),
            ["Reset"] = ("Reset", "リセット"),
            ["RealtimeZoomAutomation"] = ("Real-time comparison zoom", "リアルタイム比較の拡大率"),
            ["OriginalFocusAutomation"] = ("Click or drag to select the focus point in the original video", "原画の注目位置をクリックまたはドラッグして選択"),
            ["VsrFocusAutomation"] = ("Click or drag to select the focus point in the VSR video", "VSR 映像の注目位置をクリックまたはドラッグして選択"),
            ["Original"] = ("Original", "原画"),
            ["RealtimeVsr"] = ("VSR real time", "VSR リアルタイム"),
            ["FocusHelp"] = ("Click the video to zoom around that pixel. Drag to move the focus point.", "映像内をクリックするとその画素を中心に拡大します。ドラッグすると注目点を移動できます。"),
            ["InputVideoStep"] = ("1. Input video", "1. 入力動画"),
            ["ChooseVideo"] = ("Choose video", "動画を選択"),
            ["OutputScale"] = ("Output scale", "出力倍率"),
            ["ScaleFormat"] = ("{0:0.##}x", "{0:0.##} 倍"),
            ["OutputStep"] = ("2. Output", "2. 出力先"),
            ["ChooseOutput"] = ("Choose output location", "保存先を選択"),
            ["ProcessingStep"] = ("3. Process", "3. 処理"),
            ["StartProcessing"] = ("Start VSR processing", "VSR 処理を開始"),
            ["Cancel"] = ("Cancel", "キャンセル"),
            ["ProgressAutomation"] = ("Video processing progress", "動画処理の進行状況"),
            ["OpenOutputFolder"] = ("Open output folder", "出力フォルダーを開く"),
            ["PreviewTitle"] = ("Input / saved result preview", "入力 / 保存結果プレビュー"),
            ["HardwareFooter"] = ("Copilot+ PCs automatically use the NPU; other PCs use the CPU. The GPU is not used.", "Copilot+ PC では NPU、非 Copilot+ PC では CPU が自動使用されます。GPU は使用されません。"),
            ["OfficialDocumentation"] = ("Official documentation", "公式ドキュメント"),
            ["NotSelected"] = ("Not selected", "未選択"),
            ["SupportedInput"] = ("Supported input: 240p-1440p, 15-60 fps", "対応入力: 240p～1440p、15～60 fps"),
            ["OutputTarget"] = ("Targets {0:0.##}x scaling, up to 4K.", "{0:0.##} 倍を目標に、最大 4K まで拡大します。"),
            ["ChooseVideoStatus"] = ("Choose a video.", "動画を選択してください。"),
            ["CheckingAvailability"] = ("Checking VSR availability...", "VSR の利用可否を確認しています…"),
            ["PlayComparison"] = ("Play comparison", "比較を再生"),
            ["Pause"] = ("Pause", "一時停止"),
            ["RealtimeChooseVideo"] = ("Choose a video to prepare the comparison.", "動画を選択すると比較を準備します。"),
            ["RealtimeDiagnosticsInitial"] = ("Choose a video to view VSR inference status.", "動画を選択すると VSR 推論状態を確認できます。"),
            ["AvailabilityCheckFailed"] = ("VSR availability check failed: {0}, HRESULT 0x{1:X8}, {2}", "VSR の初期化確認に失敗しました: {0}, HRESULT 0x{1:X8}, {2}"),
            ["OutputDetails"] = ("{0} x {1} / {2:F2}x / MP4 (H.264 + AAC)", "{0} × {1} / {2:F2} 倍 / MP4 (H.264 + AAC)"),
            ["ChooseOutputThenStart"] = ("Choose an output location, then start VSR processing.", "保存先を選択してから VSR 処理を開始してください。"),
            ["LoadFailed"] = ("Load failed", "読み込み失敗"),
            ["ChooseAnotherVideo"] = ("Choose another video.", "別の動画を選択してください。"),
            ["ReadySelectedScale"] = ("VSR processing can start with the selected scale.", "選択した倍率で VSR 処理を開始できます。"),
            ["Mp4Video"] = ("MP4 video", "MP4 動画"),
            ["ReadyToProcess"] = ("VSR processing is ready to start.", "VSR 処理を開始できます。"),
            ["CheckingSession"] = ("Checking the VSR session...", "VSR セッションを確認しています…"),
            ["ProcessingProgress"] = ("AI super resolution in progress... {0:F0}%", "AI 超解像度処理中… {0:F0}%"),
            ["Completed"] = ("Completed: {0}", "完了: {0}"),
            ["ProcessingCanceled"] = ("Processing was canceled. The partial output file will be deleted.", "処理をキャンセルしました。部分的な出力ファイルは削除されます。"),
            ["ProcessingFailed"] = ("Processing failed: {0}", "処理に失敗しました: {0}"),
            ["Canceling"] = ("Canceling...", "キャンセルしています…"),
            ["PreparingRealtime"] = ("Preparing the real-time comparison...", "リアルタイム比較を準備しています…"),
            ["RealtimeReady"] = ("Real-time synchronized view: {0}x{1} -> {2}x{3}", "{0}×{1} → {2}×{3} をリアルタイム同期表示"),
            ["InferenceWaiting"] = ("Waiting for inference: input NV12 {0}x{1} -> AI output BGRA {2}x{3}", "推論待機中: 入力 NV12 {0}×{1} → AI 出力 BGRA {2}×{3}"),
            ["HighResolutionWarning"] = (". Real-time processing above 1440p may drop more frames.", "。1440p 超のリアルタイム処理ではフレーム落ちが増える場合があります。"),
            ["RealtimePrepareFailed"] = ("Could not prepare the real-time comparison: {0}", "リアルタイム比較を準備できませんでした: {0}"),
            ["Paused"] = ("Paused", "一時停止中"),
            ["RealtimePlaying"] = ("Playing the original and VSR video in real-time sync", "原画と VSR をリアルタイム同期再生中"),
            ["RealtimeRestarted"] = ("Playing in real-time sync from the beginning", "先頭からリアルタイム同期再生中"),
            ["RealtimeStopped"] = ("Real-time VSR processing stopped.", "VSR リアルタイム処理を停止しました。"),
            ["InferenceError"] = ("VSR inference error: {0}, HRESULT 0x{1:X8}, {2}", "VSR 推論エラー: {0}, HRESULT 0x{1:X8}, {2}"),
            ["InferenceRunning"] = ("VSR inference: input {0}x{1} -> AI output {2}x{3}, {4} successful frames, {5} frames skipped while busy", "VSR 推論実行中: 入力 {0}×{1} → AI 出力 {2}×{3}, 成功 {4} フレーム, 処理待ちで省略 {5} フレーム"),
            ["RealtimeLooping"] = ("Looping the original and VSR video in sync from the beginning", "原画と VSR を先頭から同期ループ再生中"),
            ["LoopRestartFailed"] = ("Failed to restart the loop: {0}", "ループ再開に失敗しました: {0}"),
            ["Zooming"] = ("Zoomed to {0:F2}x. Click or drag the video to change the focus point.", "{0:F2} 倍で拡大中。映像をクリックまたはドラッグして注目位置を変更できます。"),
            ["PixelFocus"] = ("Both videos are zoomed to {0:F2}x around the selected pixel.", "選択した画素を中心に {0:F2} 倍で左右同時に拡大しています。"),
            ["DraggedFocus"] = ("Both videos are zoomed to {0:F2}x around the dragged focus point.", "ドラッグした注目点を中心に {0:F2} 倍で左右同時に拡大しています。"),
            ["ZoomReset"] = ("Zoom reset.", "拡大表示をリセットしました。"),
            ["MediaOpenFailed"] = ("Could not open media: {0}, {1}", "メディアを開けませんでした: {0}, {1}"),
            ["ReselectRequired"] = ("Selection required", "再選択が必要"),
            ["ScalerPreparationFailed"] = ("Could not prepare VideoScaler: {0}", "VideoScaler を準備できませんでした: {0}"),
            ["ScalerUnavailable"] = ("VideoScaler is unavailable on this system: {0}", "このシステムでは VideoScaler を利用できません: {0}"),
            ["ScalerAvailable"] = ("VideoScaler is available. {0} Copilot+ PCs automatically use the NPU; other supported PCs use the CPU.", "VideoScaler は利用可能です。{0} Copilot+ PC では NPU、その他の対応 PC では CPU が自動選択されます。"),
            ["ResolutionUnavailable"] = ("Could not get the video resolution.", "動画の解像度を取得できませんでした。"),
            ["ResolutionUnsupported"] = ("The input resolution {0} x {1} is outside the recommended VSR range (240p-1440p).", "入力解像度 {0} × {1} は VSR の目安 (240p～1440p) 外です。"),
            ["FrameRateUnsupported"] = ("The input frame rate {0:F2} fps is outside the supported VSR range (15-60 fps).", "入力フレームレート {0:F2} fps は VSR の対応範囲 (15～60 fps) 外です。"),
            ["CompositionExportFailed"] = ("MediaComposition export failed: {0}", "MediaComposition の書き出しに失敗しました: {0}"),
            ["ScaleOutOfRange"] = ("Output scale must be between 1.25x and 4x.", "出力倍率は 1.25 倍から 4 倍の範囲で指定してください。"),
            ["CpuRecommended"] = ("The CPU meets the recommendation ({0} cores, {1} MHz, L3 {2} MB).", "CPU は推奨仕様を満たします ({0} コア、{1} MHz、L3 {2} MB)。"),
            ["CpuBelowRecommended"] = ("The CPU may be below the recommendation ({0} cores, {1} MHz, L3 {2} MB). Processing may be slower.", "CPU は推奨仕様未満の可能性があります ({0} コア、{1} MHz、L3 {2} MB)。処理速度が低下する場合があります。"),
            ["CpuCheckSkipped"] = ("CPU recommendation check was skipped ({0}).", "CPU 推奨仕様の確認を省略しました ({0})。")
        };

    public static AppLanguage CurrentLanguage { get; private set; } =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals(
            "ja",
            StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.Japanese
            : AppLanguage.English;

    public static void SetLanguage(AppLanguage language) => CurrentLanguage = language;

    public static string Get(string key, params object?[] arguments)
    {
        var value = Strings.TryGetValue(key, out var translations)
            ? CurrentLanguage == AppLanguage.Japanese
                ? translations.Japanese
                : translations.English
            : key;
        return arguments.Length == 0
            ? value
            : string.Format(CultureInfo.CurrentCulture, value, arguments);
    }
}