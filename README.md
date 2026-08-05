<div align="center">

<img src="Assets/Square150x150Logo.png" alt="Windows AI VSR Demo logo" width="96" />

# Windows AI Video Super Resolution Demo

Windows AI の Video Super Resolution を使って動画をリアルタイム比較し、AI 超解像度処理した MP4 を保存する WinUI 3 デスクトップデモです。

[![Windows 11](https://img.shields.io/badge/Windows_11-25H2-0078D4?logo=windows11&logoColor=white)](https://www.microsoft.com/windows/windows-11)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![WinUI 3](https://img.shields.io/badge/WinUI-3-0078D4)](https://learn.microsoft.com/windows/apps/winui/winui3/)
[![Windows App SDK](https://img.shields.io/badge/Windows_App_SDK-2.3.1-0078D4)](https://learn.microsoft.com/windows/apps/windows-app-sdk/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

[English](README-en.md) | **日本語**

</div>

> [!IMPORTANT]
> CPU / NPU を選択する UI や、Copilot+ PC 上で CPU を強制選択する API はありません。Windows が実行ハードウェアを自動選択します。

## 実行ハードウェアについて

CPU/NPU の選択 UI はありません。公式仕様どおり Windows が自動選択します。

- **Copilot+ PC:** NPU を使用
- **非 Copilot+ PC:** CPU を使用
- **GPU:** Video Super Resolution では非対応

Copilot+ PC 上で CPU を強制選択する API はありません。このため、このデモは「CPU 強制」ではなく、非 Copilot+ PC で自動的に CPU 経路を使うことを確認するデモです。

## 主な機能

- MP4 / MOV / M4V / WMV の選択とプレビュー
- 原画と VSR 適用映像のリアルタイム同期比較
- 専用タブでウィンドウ横幅いっぱいに左右比較
- 1.0～4.0 倍の拡大率調整
- クリック位置を中心にした左右同期ズーム
- VSR の対応状態と CPU 推奨仕様の確認
- 1.25～4.0 倍まで 0.25 倍刻みで出力倍率を選択し、リアルタイム比較と MP4 書き出しの両方へ適用（480p～最大 4K の範囲へ自動調整）
- フレームサーバーモードによるリアルタイム VSR と、`MediaComposition` による MP4 書き出し
- H.264 + AAC の MP4 保存
- 進捗表示、キャンセル、出力プレビュー、出力フォルダー表示

## 構成

```text
windows-ai-vsr-cpu-demo/
├─ WindowsAiVsrCpuDemo.csproj       WinUI 3 アプリ
├─ MainWindow.xaml(.cs)             UI、ファイル選択、進捗、プレビュー
├─ Services/
│  └─ VideoProcessingService.cs     VSR 準備確認、MediaComposition 書き出し
└─ VsrVideoEffect/
   ├─ VsrVideoEffect.csproj         C#/WinRT コンポーネント
   └─ VideoSuperResolutionEffect.cs IBasicVideoEffect + VideoScaler.Scale
```

`VideoEffectDefinition` は Windows Runtime activation を使うため、`Package.appxmanifest` で `WinRT.Host.dll` と `VsrVideoEffect.VideoSuperResolutionEffect` を登録しています。
Windows AI capability が古い互換対象として無効化されないよう、manifest の `MaxVersionTested` は `10.0.26226.0` に固定し、MSBuild による置換を無効化しています。

## 前提条件

- Windows 11 version 25H2、**build 10.0.26200.7309 以降**
- Developer Mode
- Visual Studio 2022/2026 の Desktop development with C++ と Windows application development 関連コンポーネント
- .NET 8 SDK 以降
- `winapp` CLI
- VSR 対応 CPU または Copilot+ PC

CPU の推奨仕様:

- 物理 4 コア以上
- ベースクロック 3 GHz 以上
- L3 キャッシュ 32 MB 以上

推奨仕様はハード最低要件ではありませんが、未満の CPU では処理速度や実時間性能が低下します。

## Windows App SDK

`Microsoft.WindowsAppSDK` **2.3.1 stable** を固定しています。

- VSR API 自体は Windows App SDK 2.2 で stable になりました。
- 2.3.1 で NPU 検出修正、CPU 対応、VSR 性能改善が追加されたため、本デモは 2.3.1 を使用します。
- Experimental パッケージや Limited Access Feature token は不要です。
- ただし、`IBasicVideoEffect` を WinUI 3 からホストするために使う C#/WinRT コンポーネント authoring tooling は Microsoft のドキュメント上 preview 扱いです。

## ビルドと実行

リポジトリを取得した後、**リポジトリルート**で次のコマンドを実行します。すべて相対パスのため、配置先に依存しません。

```powershell
dotnet restore .\WindowsAiVsrCpuDemo.csproj -p:Platform=x64
.\BuildAndRun.ps1 .\WindowsAiVsrCpuDemo.csproj
```

ARM64 マシンでは `BuildAndRun.ps1` がプラットフォームを自動検出します。明示的に指定する場合:

```powershell
.\BuildAndRun.ps1 .\WindowsAiVsrCpuDemo.csproj /p:Platform=ARM64
```

ビルドのみ:

```powershell
.\BuildAndRun.ps1 .\WindowsAiVsrCpuDemo.csproj -SkipRun
```

アプリ実行後:

1. **動画を選択**
2. **比較を再生** で原画と VSR のリアルタイム変化を確認
3. 必要に応じて **保存先を選択**
4. **VSR 処理を開始**
5. 完了後、プレビューと出力フォルダーで結果を確認

## リアルタイム比較モード

- 左側は入力動画をそのまま再生
- `MediaPlayer.IsVideoFrameServerEnabled` で入力動画をネイティブ解像度の NV12 フレームとして取得
- 同じ入力フレームから原画表示と `VideoScaler.Scale` の BGRA 出力を生成するため、左右はフレーム単位で一致
- `MediaTimelineController` で再生、一時停止、先頭移動、ループを制御
- 音声はフレームサーバー用の単一プレイヤーから出力
- リアルタイム出力は公式対応範囲の最大 1440p に制限
- 画面下部に入力・AI 出力解像度、成功フレーム数、処理待ちで省略したフレーム数を表示
- 拡大率スライダーで 1.0～4.0 倍を 0.25 刻みで調整
- 左右どちらかの映像をクリックすると、その座標を共通の中心として両映像を同期ズーム
- 1.0 倍で映像をクリックした場合は、自動的に 2.0 倍へ拡大
- CPU 性能や入力解像度によってはフレーム落ちが発生する場合があります
- 比較表示はデモ用です。最終品質の確認には保存した MP4 を使用してください

## API とパイプライン

1. `ExecutionProviderCatalog.EnsureAndRegisterCertifiedAsync` で認定済み実行プロバイダーを登録
2. `VideoScaler.GetReadyState` / `EnsureReadyAsync` で利用可否を確認
3. `MediaClip` から入力解像度、FPS、長さを取得
4. リアルタイム比較では `VideoFrameAvailable` からネイティブ解像度の NV12 サーフェスを取得
5. 入力サイズの原画 BGRA フレームと、拡大先サイズの VSR BGRA フレームを同じ入力から生成して描画
6. MP4 書き出しでは `MediaComposition` と C#/WinRT `IBasicVideoEffect` を使用
7. `ProcessFrame` 内で Direct3D サーフェスに `VideoScaler.Scale` を適用
8. `RenderToFileAsync` で H.264 + AAC MP4 を保存

## 公式の対応範囲と制限

- 入力解像度: 240p～1440p
- 出力解像度: 480p～1440p、オフライン処理では最大 4K
- FPS: 15～60
- Direct3D 入力: NV12
- Direct3D 出力: BGRA または NV12
- VSR は人物の会話、ビデオ通話、ソーシャル/短尺動画向けに最適化
- CPU 処理は動画時間より大幅に長くなる場合があります
- 1440p を超えるリアルタイム処理は高負荷で、フレーム落ちが増える場合があります
- H.264/AAC コーデックが OS に存在しない場合、書き出しは失敗します
- 回転メタデータ、HDR、字幕、複数音声トラック、DRM 保護動画はこのデモの対象外です
- メディアパイプラインが対応形式へデコードできないファイルは処理できません
- AI 超解像度は存在しなかった細部を推定するため、医療、法科学、本人確認など忠実性が必須の用途には使用しないでください

## 公式資料

- [Get Started with AI Video Super Resolution](https://learn.microsoft.com/windows/ai/apis/video-super-resolution)
- [Windows App SDK 2.0 release notes](https://learn.microsoft.com/windows/apps/windows-app-sdk/release-notes/windows-app-sdk-2-0)
- [Custom video effects for WinUI 3](https://learn.microsoft.com/windows/apps/develop/media-authoring-processing/custom-video-effects)
- [Media compositions and editing](https://learn.microsoft.com/windows/apps/develop/media-authoring-processing/media-compositions-and-editing)

このプロジェクトは [MIT License](LICENSE) のもとで提供されています。Copyright (c) 2026 tokawa-ms.
