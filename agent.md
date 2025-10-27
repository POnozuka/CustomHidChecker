# Agent.md — 要件定義書

## プロジェクト概要
自作USB HIDカスタムデバイスの動作確認を行うための、Windows用GUIアプリケーションを開発する。エンジニア以外の利用者（評価担当・サポート担当など）も安全かつ容易に使用できることを目的とする。

---

## 開発方針
### 技術スタック
- 言語: **C#**
- フレームワーク: **.NET 8 (WPF)**
- HID通信方式: **Windows API (Win32 HIDクラスドライバ経由)**
  - 使用関数群: `CreateFile`, `ReadFile`, `WriteFile`, `HidD_GetFeature`, `HidD_SetFeature`, `DeviceIoControl`
- 外部依存ライブラリ: **なし**（hidapiなどの外部DLLを利用しない）
- 配布形態: 単一exe（self-containedビルド対応）

---

## システム要件
### 動作環境
- OS: Windows 10 / 11 (64bit)
- 権限: 標準ユーザー権限で実行可能（ドライバ署名済みHIDクラスデバイス想定）
- 対応デバイス: USB HID Custom Device（VendorID / ProductID指定で識別）

### 必要機能
1. **デバイス列挙機能**
   - HIDクラスデバイスを列挙し、VID/PID/製品名を取得表示。
   - 複数デバイスがある場合、選択式（ComboBoxなど）で接続対象を選択可能にする。

2. **デバイス接続/切断管理**
   - `CreateFile`でハンドルを開き、通信可能状態を維持。
   - 切断時は確実に`CloseHandle`を実行し、再接続可能にする。

3. **レポート送受信**
   - **Output Report送信**: `WriteFile`経由で任意のバイト列を送信。
   - **Input Report受信**: `ReadFile`でIN転送を受信し、UI上にHEXで表示。
   - **Feature Report送受信**: `HidD_GetFeature` / `HidD_SetFeature` に対応。
   - Report IDを先頭バイトとして扱う（単一レポート構成でもID=0x00を明示）。

4. **リアルタイムログ表示**
   - 送受信データをHEXダンプ形式でテキスト表示。
   - タイムスタンプ付きで記録し、エクスポート（CSVまたはTXT）可能にする。

5. **UI構成（WPF）**
   - メイン画面：
     - デバイス選択ComboBox
     - 接続／切断ボタン
     - レポート送信領域（入力欄＋送信ボタン）
     - 受信データ表示エリア（スクロール＋自動更新）
     - ステータスバー（接続状態／エラーメッセージ）

6. **ログ・エラーハンドリング**
   - `GetLastWin32Error()` を利用した詳細なエラー出力。
   - 例外処理：USB切断・アクセス拒否・タイムアウトに対応。

---

## 設計方針
### レイヤー構造
1. **Presentation層（WPF UI）**
   - View (XAML) と ViewModel (MVVM) 構成を基本とする。
   - UIスレッドとI/Oスレッドを明確に分離（非同期処理：`Task` / `async/await`）。

2. **Device Access層**
   - `IHidDevice` インターフェイスを定義し、実装クラスとして `WinHidDevice` を作成。
   - 将来的に `HidApiDevice` など別実装に差し替え可能にする（拡張性確保）。

3. **Utility層**
   - HIDデバイス情報列挙用クラス (`HidEnumerator`)
   - バイト列⇔HEX変換ユーティリティ (`HexConverter`)

### 例: インターフェイス設計案
```csharp
public interface IHidDevice : IDisposable
{
    bool Open(string devicePath);
    void Close();
    bool Write(byte[] data);
    byte[] Read(int length, int timeoutMs);
    bool GetFeature(byte[] buffer);
    bool SetFeature(byte[] buffer);
}
```

---

## スケジュール（初期版）
| フェーズ | 内容 | 期間 |
|------------|-------|-------|
| 設計 | 要件確定・画面設計・API確認 | 1日 |
| 実装 | Deviceアクセス層・WPF画面 | 2〜3日 |
| テスト | 実機接続・例外動作確認 | 1日 |
| ドキュメント | 操作マニュアル／ログ仕様書 | 1日 |

---

## 今後の拡張余地
- hidapi版（クロスプラットフォーム化）バックエンド実装
- 自動テストスクリプト（シーケンステスト用）機能追加
- JSONベースのレポート定義読み込み機能
- ファームウェアアップデートツール統合（DFU / Bootloader対応）

---

## 備考（エリナからご主人様へ）
> ご主人様、この方針なら非エンジニアにも優しく、配布も容易です。  
>  ただし、`CreateFile`で取得したハンドルを閉じ忘れないように。  
>  メモリリークを出すと、また「動かない」と怒られますからね……。

