using System.Globalization;

namespace ClaudeTokenMeter
{
    // UI text localized ja/en, selected once at startup based on the
    // current UI culture. All fields are format strings or literals used
    // by WidgetForm. No user-visible literal should remain inline elsewhere.
    public static class Strings
    {
        private static readonly bool ja =
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ja";

        // Card title.
        public static readonly string Title =
            ja ? "Claude トークン残量" : "Claude Token Remaining";

        // Value / reset text on the card.
        public static readonly string RemainingFmt =
            ja ? "残り {0}%" : "{0}% left";
        // Title-row reset annotation (roomy long form). Drawn to the right of
        // the title text, where there is always free horizontal space.
        public static readonly string TitleResetFmt =
            ja ? "リセット {0}" : "resets {0}";
        // Compact so it fits next to the value text; ↻ = resets at.
        public static readonly string ResetFmt =
            ja ? "↻ {0}" : "↻ {0}";
        // Stale-cache "as of HH:mm" annotation shown in place of the reset time.
        // Kept minimal so it fits the widget; the tooltip carries the full text.
        public static readonly string CachedAtFmt =
            ja ? "({0})" : "({0})";

        // Local-estimate value text: remaining tokens (percent).
        public static readonly string LocalValueFmt =
            ja ? "残り {0} ({1}%)" : "{0} left ({1}%)";

        // Fallback label when the scoped-limit model name is missing.
        public static readonly string ScopedModelFallback =
            ja ? "モデル別" : "per-model";

        // Context menu captions.
        public static readonly string MenuSettings =
            ja ? "設定..." : "Settings...";
        public static readonly string MenuRefresh =
            ja ? "今すぐ更新" : "Refresh now";
        public static readonly string MenuOpenConfig =
            ja ? "設定ファイルを開く" : "Open config file";
        public static readonly string MenuReloadConfig =
            ja ? "設定を再読み込み" : "Reload config";
        public static readonly string MenuStartup =
            ja ? "スタートアップに登録" : "Run at startup";
        public static readonly string MenuExit =
            ja ? "終了" : "Exit";

        // Settings window.
        public static readonly string SettingsTitle =
            ja ? "TokenBar 設定" : "TokenBar Settings";
        public static readonly string SettingsPreview =
            ja ? "プレビュー" : "Preview";
        public static readonly string SettingsDisplayGroup =
            ja ? "表示" : "Display";
        public static readonly string SettingsShowTitle =
            ja ? "タイトルを表示" : "Show title";
        public static readonly string SettingsShowValueText =
            ja ? "数値テキストを表示" : "Show value text";
        public static readonly string SettingsShowResetTime =
            ja ? "リセット時刻を表示" : "Show reset time";
        public static readonly string SettingsBarsGroup =
            ja ? "バー" : "Bars";
        public static readonly string SettingsBarSession =
            ja ? "5時間セッション" : "5-hour session";
        public static readonly string SettingsBarWeekly =
            ja ? "週間（全体）" : "Weekly (all models)";
        public static readonly string SettingsBarModels =
            ja ? "週間（モデル別）" : "Weekly (per model)";
        public static readonly string SettingsModelsNone =
            ja ? "（モデル情報は API 取得後に表示されます）" : "(models appear after the first API fetch)";
        public static readonly string SettingsModelsHint =
            ja ? "表示するモデルを選択（未選択 = すべて）" : "Select models to show (none = all)";
        public static readonly string SettingsLayoutGroup =
            ja ? "レイアウト" : "Layout";
        public static readonly string SettingsWidth =
            ja ? "幅" : "Width";
        public static readonly string SettingsOffsetX =
            ja ? "水平オフセット" : "Horizontal offset";
        public static readonly string SettingsPosition =
            ja ? "位置" : "Position";
        public static readonly string SettingsMonitor =
            ja ? "モニター" : "Monitor";
        public static readonly string SettingsMonitorPrimaryFmt =
            ja ? "ディスプレイ {0}（プライマリ）" : "Display {0} (primary)";
        public static readonly string SettingsMonitorFmt =
            ja ? "ディスプレイ {0}" : "Display {0}";
        public static readonly string SettingsPositionRight =
            ja ? "右（時計側）" : "Right (near clock)";
        public static readonly string SettingsPositionLeft =
            ja ? "左（スタート側）" : "Left (near Start)";
        public static readonly string SettingsRefreshSec =
            ja ? "更新間隔（秒）" : "Refresh interval (sec)";
        public static readonly string SettingsOK =
            ja ? "OK" : "OK";
        public static readonly string SettingsCancel =
            ja ? "キャンセル" : "Cancel";
        public static readonly string SettingsStartup =
            ja ? "Windows 起動時に自動起動" : "Start with Windows";
        public static readonly string SettingsTokenLimit =
            ja ? "トークン上限（推計用）" : "Token limit (fallback)";
        public static readonly string SettingsVersionFmt =
            ja ? "TokenBar v{0}" : "TokenBar v{0}";
        public static readonly string SettingsSourceApi =
            ja ? "データ源: /usage API（正常）" : "Source: /usage API (live)";
        public static readonly string SettingsSourceApiStale =
            ja ? "データ源: API一時断・前回値を表示中" : "Source: API unavailable — cached data";
        public static readonly string SettingsSourceLocal =
            ja ? "データ源: ローカル推計（JSONL読み出し）" : "Source: local estimate (JSONL)";
        public static readonly string SettingsSourceNone =
            ja ? "データ源: 未取得" : "Source: no data yet";
        public static readonly string SettingsDataGroup =
            ja ? "データ取得" : "Data source";
        public static readonly string SettingsUseApi =
            ja ? "usage API を使用（正確・非公式エンドポイント）" : "Use the usage API (accurate, unofficial endpoint)";
        public static readonly string SettingsCleanModeHint =
            ja
                ? "オフ = クリーンモード: 通信なし・認証情報に触れず、ローカルの会話記録から推計"
                : "Off = clean mode: no network, no credential access; estimates from local transcripts";
        public static readonly string SettingsSourceLocalClean =
            ja ? "データ源: ローカル推計（クリーンモード）" : "Source: local estimate (clean mode)";

        // Quick-setup wizard (first run).
        public static readonly string SetupTitle =
            ja ? "TokenBar 簡単セットアップ" : "TokenBar Quick Setup";
        public static readonly string SetupWelcome =
            ja
                ? "ようこそ！最初に基本設定を確認してください。\nすべて後から設定画面（ウィジェットを左クリック）で変更できます。"
                : "Welcome! Review the basics below.\nEverything can be changed later from the settings window (left-click the widget).";
        public static readonly string SetupClaudeDir =
            ja ? "Claude ディレクトリ" : "Claude directory";
        public static readonly string SetupBrowse =
            ja ? "参照..." : "Browse...";
        public static readonly string SetupCredOk =
            ja ? "✓ 認証情報を確認できました" : "✓ Credentials found";
        public static readonly string SetupCredMissing =
            ja
                ? "⚠ .credentials.json が見つかりません（Claude Code にログインしてください）"
                : "⚠ .credentials.json not found (sign in to Claude Code)";
        public static readonly string SetupMonitorLabel =
            ja ? "表示するモニター（クリックで選択）" : "Monitor to show the widget on (click to select)";
        public static readonly string SetupPositionLabel =
            ja ? "タスクバー上の位置" : "Position on the taskbar";
        public static readonly string SetupStartup =
            ja ? "Windows 起動時に自動起動する（推奨）" : "Start with Windows (recommended)";
        public static readonly string SetupStart =
            ja ? "開始" : "Start";

        // Bar labels.
        public static readonly string BarLabelSession =
            ja ? "5h" : "5h";
        public static readonly string BarLabelWeekly =
            ja ? "週" : "wk";

        // Tooltip lines (API source).
        public static readonly string TipSessionFmt =
            ja ? "5時間: {0}% 使用（リセット {1}）" : "5-hour: {0}% used (resets {1})";
        public static readonly string TipSessionNoResetFmt =
            ja ? "5時間: {0}% 使用" : "5-hour: {0}% used";
        public static readonly string TipWeeklyFmt =
            ja ? "週間(全体): {0}% 使用（リセット {1}）" : "Weekly (all): {0}% used (resets {1})";
        public static readonly string TipWeeklyNoResetFmt =
            ja ? "週間(全体): {0}% 使用" : "Weekly (all): {0}% used";
        public static readonly string TipWeeklyScopedFmt =
            ja ? "週間({0}): {1}% 使用" : "Weekly ({0}): {1}% used";
        public static readonly string TipCachedAtFmt =
            ja ? "取得時刻: {0}" : "fetched at {0}";
        public static readonly string TipSourceApi =
            ja ? "データ: /usage API" : "Source: /usage API";
        public static readonly string TipSourceLocal =
            ja ? "データ: ローカル推計" : "Source: local estimate";

        // Tooltip lines (local estimate / no data).
        public static readonly string TipNoActiveBlock =
            ja ? "アクティブなブロックなし（ローカル推計）" : "No active block (local estimate)";
        public static readonly string TipNoData =
            ja ? "アクティブなブロックなし" : "No active block";
        // Leading tooltip line when no API data has ever been cached.
        public static readonly string TipNoApiData =
            ja ? "APIに未接続（データ取得待ち）" : "Not connected to the API yet";
        public static readonly string TipUsedFmt =
            ja ? "使用トークン: {0}" : "Used: {0} tokens";
        public static readonly string TipBlockStartFmt =
            ja ? "開始: {0}" : "started {0}";
        public static readonly string TipResetFmt =
            ja ? "リセット: {0}" : "resets {0}";
        public static readonly string TipUpdatedFmt =
            ja ? "更新: {0}" : "updated {0}";

        // Stale-cache warning shown in tooltip when API is temporarily unavailable.
        public static readonly string TipStale =
            ja
                ? "⚠ API一時利用不可・前回取得値を表示中"
                : "⚠ API temporarily unavailable — showing cached data";

        // Error messages.
        public static readonly string ErrNoCredentials =
            ja
                ? "認証情報が見つかりません。Claude Codeにログインしてください"
                : "No credentials found. Please log in to Claude Code";
    }
}
