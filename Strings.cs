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
