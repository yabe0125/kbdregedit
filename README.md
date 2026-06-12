# KbdLayoutTool — キーボードレイアウト オーバーライド ツール

Windows 11 で、**キーボードごと（per-device）に配列を上書き**する WPF GUI ツールです。
「日本語配列ノートPC と US 配列キーボードを共存させる」運用を、レジストリを手で触らずに行えます。

このツールは、Keychron J9 を有線/Bluetooth とも JIS 配列に手作業で設定した経験をコード化したものです。

## できること

- `HKLM\SYSTEM\CurrentControlSet\Enum\HID` を走査して**キーボード HID コレクションを一覧表示**
  （Service=`kbdhid` / キーボードクラス `{4d36e96b-...}` で判定）
- 製品名（例 `Keychron J9`）・接続バス（USB/Bluetooth）・**接続状態**・現在のレイアウトを表示
- 選択したデバイス（複数可）に対して、ワンクリックで配列を上書き:
  - **日本語 JIS (106/109)** … `KeyboardTypeOverride=7`, `KeyboardSubtypeOverride=2`
  - **英語 US (101/104)** … `KeyboardTypeOverride=4`, `KeyboardSubtypeOverride=0`
  - **override 解除**（システム既定に戻す）
- 同じ設定を `.reg` ファイル（UTF-16 LE）として出力（バックアップ・他PC展開用）

## 重要な技術ポイント

per-device の上書きで使う値名は **`KeyboardTypeOverride` / `KeyboardSubtypeOverride`**（Override が**末尾**）。
`OverrideKeyboardType` / `OverrideKeyboardSubtype`（Override が先頭）は **i8042prt のシステム全体用**で、
デバイスの `Device Parameters` に置いても**無視される**。本ツールは前者を書き、見つけた後者は掃除する。

設定の反映には **USB の抜き差し / Bluetooth の再接続**、または再起動が必要。

> Bluetooth デバイスを削除して再ペアリングすると、デバイスインスタンスパスが変わる。
> その場合は「再スキャン」で新しいノードを拾い直して再適用する。製品名は MAC から解決しているので一覧上は同じ名前で出る。

## 構成

```
KeyboardLayoutTool.slnx
├─ src/KbdLayoutTool.Core/      … 純ロジック（GUI 非依存・テスト可能）
│   ├─ Models/                  … KeyboardDevice, KeyboardLayoutPreset, BusKind
│   ├─ Native/ConfigManager.cs  … CfgMgr32 で接続状態を判定
│   └─ Services/                … Scanner / OverrideService / ProductNameResolver / RegFileExporter
├─ src/KbdLayoutTool.App/       … WPF GUI（app.manifest で requireAdministrator）
└─ tools/Smoke/                 … レジストリ読み取り専用の動作確認コンソール
```

## ビルド & 実行

VS2022 で `KeyboardLayoutTool.slnx` を開くか、CLI で:

```powershell
dotnet build KeyboardLayoutTool.slnx
# GUI（HKLM 書き込みのため UAC 昇格プロンプトが出ます）
dotnet run --project src/KbdLayoutTool.App/KbdLayoutTool.App.csproj
# 読み取り専用の確認（昇格不要）
dotnet run --project tools/Smoke/KbdLayoutTool.Smoke.csproj
```

要件: .NET 9 SDK + Windows Desktop ランタイム（VS2022 同梱）。

## 既知の制約 / 今後

- USB 接続キーボードの製品名は USB 列挙ノードに依存し、`USB Composite Device` のような汎用名で出ることがある
  （識別子列に VID/PID を併記して区別可能）。
- カスタム Type/Subtype の手入力 UI、プロファイルのインポート/エクスポート、設定変更の自動反映（デバイス再起動）などは未実装。
