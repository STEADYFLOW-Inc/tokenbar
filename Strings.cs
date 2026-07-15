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
        public static readonly string ResetFmt =
            ja ? "リセット {0}" : "resets {0}";

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
        public static readonly string SettingsLayoutGroup =
            ja ? "レイアウト" : "Layout";
        public static readonly string SettingsWidth =
            ja ? "幅" : "Width";
        public static readonly string SettingsOffsetX =
            ja ? "水平オフセット" : "Horizontal offset";
        public static readonly string SettingsPosition =
            ja ? "位置" : "Position";
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
        public static readonly string TipSourceApi =
            ja ? "データ: /usage API" : "Source: /usage API";
        public static readonly string TipSourceLocal =
            ja ? "データ: ローカル推計" : "Source: local estimate";

        // Tooltip lines (local estimate / no data).
        public static readonly string TipNoActiveBlock =
            ja ? "アクティブなブロックなし（ローカル推計）" : "No active block (local estimate)";
        public static readonly string TipNoData =
            ja ? "アクティブなブロックなし" : "No active block";
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
